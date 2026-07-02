using Cortex.Application.Approvals;
using Cortex.Application.Authorization;
using Cortex.Core.Platform;
using Cortex.Infrastructure.Approvals;

namespace Cortex.AspNetCore.Endpoints;

/// <summary>
/// The human-in-the-loop approval surface. When the agent tries to call a side-effecting tool it is
/// blocked and recorded as a pending approval (see <c>ToolInvocationMiddleware</c>). These endpoints let
/// an authorized human review the pending action, then either approve it — which re-executes that exact
/// tool call with its recorded arguments — or reject it.
/// </summary>
public static class ApprovalEndpoints
{
    public static void MapApprovalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat/approvals").WithTags("Approvals").RequireAuthorization();

        group.MapGet("/", async (IApprovalStore store, CancellationToken ct) =>
            {
                var pending = await store.ListPendingAsync(ct);
                return Results.Ok(pending.Select(ToDto).ToArray());
            })
            .RequireAuthorization(PermissionRequirement.PolicyName(Permissions.ManageApprovals))
            .WithName("Approvals_ListPending");

        group.MapPost("/{id:guid}/approve", async (
                Guid id, IApprovalStore store, ApprovalExecutor executor, IServiceProvider services, CancellationToken ct) =>
            {
                var pending = await store.GetAsync(id, ct);
                if (pending is null || pending.Status != ApprovalStatus.Pending)
                {
                    return Results.NotFound();
                }

                var outcome = await executor.ExecuteAsync(pending, services, ct);
                await store.ResolveAsync(
                    id,
                    outcome.Success ? ApprovalStatus.Executed : ApprovalStatus.Failed,
                    outcome.Result,
                    outcome.Error,
                    ct);

                return outcome.Success
                    ? Results.Ok(new { id, status = nameof(ApprovalStatus.Executed), result = outcome.Result })
                    : Results.Problem(detail: outcome.Error, statusCode: 422);
            })
            .RequireAuthorization(PermissionRequirement.PolicyName(Permissions.ManageApprovals))
            .WithName("Approvals_Approve");

        group.MapPost("/{id:guid}/reject", async (Guid id, IApprovalStore store, CancellationToken ct) =>
            {
                var pending = await store.GetAsync(id, ct);
                if (pending is null || pending.Status != ApprovalStatus.Pending)
                {
                    return Results.NotFound();
                }

                await store.ResolveAsync(id, ApprovalStatus.Rejected, result: null, error: null, ct);
                return Results.Ok(new { id, status = nameof(ApprovalStatus.Rejected) });
            })
            .RequireAuthorization(PermissionRequirement.PolicyName(Permissions.ManageApprovals))
            .WithName("Approvals_Reject");
    }

    private static ApprovalDto ToDto(PendingApproval p) =>
        new(p.Id, p.ConversationId, p.ModuleId, p.ToolName, p.ArgumentsJson, p.UserDisplay, p.CreatedAt);

    private sealed record ApprovalDto(
        Guid Id, Guid ConversationId, string ModuleId, string ToolName, string? ArgumentsJson, string? UserDisplay, DateTimeOffset CreatedAt);
}
