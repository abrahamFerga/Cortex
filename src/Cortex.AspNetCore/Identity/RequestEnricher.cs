using System.Security.Claims;
using Cortex.Application.Auditing;
using Cortex.Application.Authorization;
using Cortex.AspNetCore.Auth;
using Cortex.Core.Platform;
using Cortex.Infrastructure.Context;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cortex.AspNetCore.Identity;

/// <summary>
/// Populates the scoped <see cref="RequestContext"/> from an authenticated principal: resolve tenant →
/// JIT-provision the user → resolve permissions. Shared by the HTTP middleware and the SignalR hub
/// filter so chat-over-WebSocket gets the same identity treatment as REST calls.
/// </summary>
public interface IRequestEnricher
{
    /// <summary>
    /// Populates the request context from the principal. Returns <c>false</c> when the resolved user is
    /// <b>deactivated</b> — the caller must then deny the request (the user keeps a valid token but no access).
    /// </summary>
    public Task<bool> EnrichAsync(ClaimsPrincipal principal, string? ipAddress, CancellationToken cancellationToken);
}

public sealed class RequestEnricher(
    RequestContext requestContext,
    PlatformDbContext db,
    IPermissionResolver permissionResolver,
    IAuditLog auditLog,
    IOptions<AuthOptions> authOptions) : IRequestEnricher
{
    public async Task<bool> EnrichAsync(ClaimsPrincipal principal, string? ipAddress, CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (subject is null)
        {
            return true;
        }

        var name = principal.FindFirstValue("name") ?? principal.Identity?.Name;
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email")
            ?? principal.FindFirstValue("preferred_username")
            ?? subject;

        requestContext.SetIdentity(subject, name);

        var tenant = await ResolveTenantAsync(principal.FindFirstValue(authOptions.Value.TenantClaim), cancellationToken);
        if (tenant is null)
        {
            return true;
        }

        // A deactivated tenant denies every one of its users — a tenant-wide kill switch for an operator.
        if (!tenant.IsActive)
        {
            return false;
        }

        requestContext.SetTenant(tenant.Id);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Subject == subject, cancellationToken);

        // A deactivated user keeps a valid token but no access: deny before resolving any identity or
        // permissions. (A just-provisioned user is active by default, so this only affects existing users.)
        if (user is { IsActive: false })
        {
            return false;
        }

        if (user is null)
        {
            user = new User
            {
                TenantId = tenant.Id,
                Subject = subject,
                Email = email ?? subject,
                DisplayName = name,
            };
            user.Roles.Add(new UserRole { TenantId = tenant.Id, UserId = user.Id, Role = Roles.User });
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);

            await auditLog.RecordAuthEventAsync(new AuthAuditEntry
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                Subject = subject,
                UserDisplay = name,
                EventType = AuthAuditEventType.UserProvisioned,
                IpAddress = ipAddress,
            }, cancellationToken);
        }
        else
        {
            user.LastSeenAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        requestContext.SetUser(user.Id, subject, name);
        requestContext.SetPermissions(await permissionResolver.ResolveAsync(principal, cancellationToken));
        return true;
    }

    private async Task<Tenant?> ResolveTenantAsync(string? slug, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(slug))
        {
            return await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
        }

        return await db.Tenants.CountAsync(cancellationToken) == 1
            ? await db.Tenants.FirstAsync(cancellationToken)
            : null;
    }
}
