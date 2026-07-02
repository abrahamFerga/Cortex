using Cortex.Application.Authorization;
using Cortex.Core.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Cortex.Infrastructure.Authorization;

/// <summary>
/// Bridges ASP.NET Core authorization to the resolved permission set: succeeds a
/// <see cref="PermissionRequirement"/> when the current user holds the required permission (honouring
/// wildcards and system_admin via <see cref="PermissionMatcher"/>).
/// </summary>
public sealed class PermissionAuthorizationHandler(ICurrentUser currentUser)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (currentUser.HasPermission(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
