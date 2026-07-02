using Cortex.Core.Platform;

namespace Cortex.Application.Jobs;

/// <summary>
/// Enqueues background jobs under the current caller's tenant and user — the platform's primitive
/// for work that outlives a request (bulk document review, batch imports). Handlers are registered
/// per kind via <see cref="IJobHandler"/>; the hosted processor executes them with the enqueuer's
/// identity restored, so RBAC, tenant filters, and audit hold inside jobs.
/// </summary>
public interface IJobQueue
{
    /// <summary>Enqueues a job and returns its id (pollable at /api/jobs/{id}).</summary>
    public Task<Guid> EnqueueAsync(string moduleId, string kind, object arguments, CancellationToken cancellationToken = default);

    /// <summary>The job row, tenant-scoped. Null when the id doesn't exist in this tenant.</summary>
    public Task<BackgroundJob?> FindAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Cancels a job that is still queued. Returns false when it already started (or doesn't exist).</summary>
    public Task<bool> TryCancelAsync(Guid jobId, CancellationToken cancellationToken = default);
}
