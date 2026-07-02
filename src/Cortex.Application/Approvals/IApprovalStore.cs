using Cortex.Core.Platform;

namespace Cortex.Application.Approvals;

/// <summary>Persists and resolves <see cref="PendingApproval"/> records for the human-in-the-loop flow.</summary>
public interface IApprovalStore
{
    Task RecordPendingAsync(PendingApproval pending, CancellationToken cancellationToken = default);

    /// <summary>Pending approvals for the current tenant, newest first.</summary>
    Task<IReadOnlyList<PendingApproval>> ListPendingAsync(CancellationToken cancellationToken = default);

    Task<PendingApproval?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task ResolveAsync(Guid id, ApprovalStatus status, string? result, string? error, CancellationToken cancellationToken = default);
}
