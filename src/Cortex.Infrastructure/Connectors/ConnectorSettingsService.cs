using System.Text.Json;
using Cortex.Application.Connectors;
using Cortex.Connectors.Sdk;
using Cortex.Core.Platform;
using Cortex.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Connectors;

/// <summary>
/// Reads and writes a tenant's connector settings. Values for manifest-declared secret fields are
/// data-protected before they touch the database and unprotected only here, on the server, for
/// connector code — the admin API never echoes a secret back (it reports "a value exists").
/// </summary>
public sealed class ConnectorSettingsService(
    PlatformDbContext db,
    IConnectorCatalog catalog,
    IDataProtectionProvider dataProtection) : IConnectorSettings
{
    private const string ProtectorPurpose = "Cortex.Connectors.Settings";

    public async Task<IReadOnlyDictionary<string, string>?> GetAsync(
        string connectorId, CancellationToken cancellationToken = default)
    {
        var row = await db.TenantConnectors
            .FirstOrDefaultAsync(c => c.ConnectorId == connectorId && c.Enabled, cancellationToken);
        if (row is null)
        {
            return null; // not enabled for this tenant — callers answer honestly, never guess
        }

        if (!catalog.TryGetManifest(connectorId, out var manifest) || manifest is null)
        {
            return null; // enabled row for a connector this host no longer installs
        }

        var stored = Deserialize(row.SettingsJson);
        var protector = dataProtection.CreateProtector(ProtectorPurpose);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in stored)
        {
            var descriptor = manifest.Settings.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));
            result[key] = descriptor is { IsSecret: true } ? protector.Unprotect(value) : value;
        }

        return result;
    }

    /// <summary>
    /// Merges admin-submitted values into the stored settings: secrets are protected on the way in,
    /// and an omitted secret keeps its existing value (the admin UI can't echo it back to resubmit).
    /// </summary>
    public async Task SaveAsync(
        TenantConnector row, ConnectorManifest manifest, IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default)
    {
        var stored = Deserialize(row.SettingsJson);
        var protector = dataProtection.CreateProtector(ProtectorPurpose);

        foreach (var descriptor in manifest.Settings)
        {
            if (!values.TryGetValue(descriptor.Key, out var value) || value is null)
            {
                continue; // untouched field keeps its stored value
            }

            if (string.IsNullOrEmpty(value))
            {
                stored.Remove(descriptor.Key);
            }
            else
            {
                stored[descriptor.Key] = descriptor.IsSecret ? protector.Protect(value) : value;
            }
        }

        row.SettingsJson = JsonSerializer.Serialize(stored);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Which settings currently have a value — what the admin API may reveal about secrets.</summary>
    public IReadOnlySet<string> KeysWithValues(TenantConnector? row) =>
        row is null ? new HashSet<string>() : Deserialize(row.SettingsJson).Keys.ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(StringComparer.Ordinal);
}
