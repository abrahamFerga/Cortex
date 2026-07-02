using Cortex.Application.Jobs;
using Cortex.Core.Platform;
using Cortex.Infrastructure.Context;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cortex.Infrastructure.Jobs;

/// <summary>
/// Executes queued <see cref="BackgroundJob"/>s. For each job it builds a fresh scope and restores
/// the enqueuer's identity the same way the request pipeline would — tenant first (query filters),
/// then user, then permissions resolved from the user's DB roles/grants — so everything a handler
/// touches behaves exactly as it does in a chat turn. Jobs run one at a time per instance
/// (deliberate: bulk work is throughput-insensitive and this keeps claiming trivially correct;
/// multi-instance deployments should add SKIP LOCKED claiming before scaling out workers).
/// </summary>
public sealed class JobProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<JobProcessor> logger) : BackgroundService
{
    /// <summary>Poll cadence for new work. Short enough for UI polling to feel live.</summary>
    public static TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ranOne = await RunNextQueuedJobAsync(stoppingToken);
                if (!ranOne)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The processor itself must never die to one bad job/claim; the job's own failure
                // handling below records per-job errors.
                logger.LogError(ex, "Job processor loop error; continuing.");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task<bool> RunNextQueuedJobAsync(CancellationToken cancellationToken)
    {
        using var claimScope = scopeFactory.CreateScope();
        var claimDb = claimScope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        // Claim the oldest queued job (cross-tenant: the processor serves every tenant).
        var job = await claimDb.BackgroundJobs.IgnoreQueryFilters()
            .Where(j => j.Status == JobStatus.Queued)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            return false;
        }

        job.Status = JobStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;
        await claimDb.SaveChangesAsync(cancellationToken);

        await ExecuteJobAsync(job.Id, cancellationToken);
        return true;
    }

    private async Task ExecuteJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<PlatformDbContext>();

        var job = await db.BackgroundJobs.IgnoreQueryFilters().FirstAsync(j => j.Id == jobId, cancellationToken);

        try
        {
            // Restore the enqueuer's identity in this scope (the WhatsApp channel does the same).
            var context = services.GetRequiredService<RequestContext>();
            context.SetTenant(job.TenantId);

            var user = await db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == job.UserId, cancellationToken)
                ?? throw new InvalidOperationException($"Job {job.Id}: enqueuing user {job.UserId} no longer exists.");
            if (!user.IsActive)
            {
                throw new InvalidOperationException($"Job {job.Id}: enqueuing user is deactivated.");
            }

            context.SetUser(user.Id, user.Subject, user.DisplayName);

            // Restore the authority captured at enqueue time. Token-asserted roles have no DB rows
            // (deliberately — see RequestEnricher), so re-resolving here would under-authorize; the
            // snapshot is both sufficient and the honest audit record of the job's allowed powers.
            var permissions = System.Text.Json.JsonSerializer.Deserialize<string[]>(job.PermissionsSnapshotJson) ?? [];
            context.SetPermissions(permissions);

            var handler = services.GetServices<IJobHandler>()
                .FirstOrDefault(h => string.Equals(h.Kind, job.Kind, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"No job handler is registered for kind '{job.Kind}'.");

            var execution = new JobExecutionContext
            {
                JobId = job.Id,
                TenantId = job.TenantId,
                UserId = job.UserId,
                ModuleId = job.ModuleId,
                ArgumentsJson = job.ArgumentsJson,
                ScopedServices = services,
                ReportProgressAsync = async (percent, note, ct) =>
                {
                    job.Progress = Math.Clamp(percent, 0, 100);
                    job.ProgressNote = note;
                    await db.SaveChangesAsync(ct);
                },
            };

            job.ResultJson = await handler.ExecuteAsync(execution, cancellationToken);
            job.Status = JobStatus.Succeeded;
            job.Progress = 100;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            job.Status = JobStatus.Failed;
            job.Error = "The host shut down while the job was running.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background job {JobId} ({Kind}) failed.", job.Id, job.Kind);
            job.Status = JobStatus.Failed;
            job.Error = ex.Message;
        }
        finally
        {
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
