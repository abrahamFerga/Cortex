using System.Security.Claims;
using Cortex.Application.Authorization;
using Cortex.Core.Identity;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Authorization;

/// <summary>
/// Computes a user's effective permission set by merging three sources: the roles the principal holds
/// (from token claims and/or DB assignments), the user's explicit DB grants, and any fine-grained
/// permission claims. Roles are expanded to permissions via the tenant's <em>configured</em> role →
/// permission rows (editable in the admin console), falling back to <see cref="RolePermissions.Defaults"/>
/// for a tenant that has none. See <see cref="RolePermissionResolution"/> for the exact rules.
/// </summary>
public sealed class PermissionResolver(PlatformDbContext db, ICurrentUser currentUser) : IPermissionResolver
{
    public async Task<IReadOnlySet<string>> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var permissions = new HashSet<string>(StringComparer.Ordinal);

        // 1. Collect every role the principal holds — asserted by the IdP and/or assigned in the DB.
        var roles = new HashSet<string>(StringComparer.Ordinal);
        roles.UnionWith(principal.FindAll(ClaimTypes.Role).Concat(principal.FindAll("roles")).Select(c => c.Value));

        if (currentUser.UserId is Guid userId)
        {
            var dbRoles = await db.UserRoles
                .Where(r => r.UserId == userId)
                .Select(r => r.Role)
                .ToListAsync(cancellationToken);
            roles.UnionWith(dbRoles);
        }

        // 2. Expand roles → permissions using the tenant's configured baseline (query filter scopes the
        //    rows to the ambient tenant), falling back to built-in defaults when the tenant has none.
        var configuredByRole = (await db.RolePermissions
                .Select(r => new { r.Role, r.Permission })
                .ToListAsync(cancellationToken))
            .GroupBy(r => r.Role, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.Permission).ToList(),
                StringComparer.Ordinal);

        permissions.UnionWith(RolePermissionResolution.PermissionsForRoles(roles, configuredByRole));

        // 3. Explicit per-user grants for the provisioned user (tenant-filtered).
        if (currentUser.UserId is Guid uid)
        {
            var dbPermissions = await db.UserPermissions
                .Where(p => p.UserId == uid)
                .Select(p => p.Permission)
                .ToListAsync(cancellationToken);
            permissions.UnionWith(dbPermissions);
        }

        // 4. Fine-grained permission claims minted directly into the token.
        permissions.UnionWith(principal.FindAll("permissions").Select(c => c.Value));

        return permissions;
    }
}
