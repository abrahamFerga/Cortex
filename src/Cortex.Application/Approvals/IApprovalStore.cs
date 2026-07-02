using Cortex.Core.Platform;

namespace Cortex.Application.Approvals;

/// <summary>Persists and resolves <see cref="PendingApproval"/> records for the human-in-the-loop flow.</summary>
public interface IApprovalStore
{
    public Task RecordPendingAsync(PendingApproval pending, CancellationToken cancellationToken = default);

    /// <summary>Pending approvals for the current tenant, newest first.</summary>
    public Task<IReadOnlyList<PendingApproval>> ListPendingAsync(CancellationToken cancellationToken = default);

    public Task<PendingApproval?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    public Task ResolveAsync(Guid id, ApprovalStatus status, string? result, string? error, CancellationToken cancellationToken = default);
}
