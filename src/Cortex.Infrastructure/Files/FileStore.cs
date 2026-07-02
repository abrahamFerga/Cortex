using System.Security.Cryptography;
using Cortex.Application.Files;
using Cortex.Core.Identity;
using Cortex.Core.Platform;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Files;

/// <summary>
/// EF-backed <see cref="IFileStore"/>: metadata rows in the platform database (tenant query filter
/// applies to every read) with content delegated to the configured <see cref="IFileBlobStorage"/>.
/// </summary>
public sealed class FileStore(
    PlatformDbContext db,
    IFileBlobStorage blobs,
    ICurrentUser currentUser) : IFileStore
{
    public async Task<StoredFile> SaveAsync(
        string fileName, string contentType, Stream content, string source, CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.TenantId
            ?? throw new InvalidOperationException("Cannot store a file without a tenant.");
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Cannot store a file without a user.");

        // Buffer once to hash and measure; uploads are size-capped at the endpoint.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var file = new StoredFile
        {
            TenantId = tenantId,
            UserId = userId,
            FileName = Path.GetFileName(fileName), // never trust a client path
            ContentType = contentType,
            SizeBytes = buffer.Length,
            Sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(buffer, cancellationToken)),
            Source = source,
        };
        buffer.Position = 0;

        await blobs.WriteAsync(tenantId, file.Id, buffer, cancellationToken);

        db.StoredFiles.Add(file);
        await db.SaveChangesAsync(cancellationToken);
        return file;
    }

    public async Task<StoredFile?> FindAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        await db.StoredFiles.FirstOrDefaultAsync(f => f.Id == fileId, cancellationToken);

    public async Task<Stream?> OpenReadAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        // Metadata lookup first: it carries the tenant filter, so a foreign tenant's id resolves to
        // null here and the blob layer is never consulted.
        var file = await FindAsync(fileId, cancellationToken);
        if (file is null)
        {
            return null;
        }

        return await blobs.OpenReadAsync(file.TenantId, file.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<StoredFile>> ListMineAsync(int take = 20, CancellationToken cancellationToken = default) =>
        await db.StoredFiles
            .Where(f => f.UserId == currentUser.UserId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);
}
