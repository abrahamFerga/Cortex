using System.Security.Claims;

namespace Cortex.Application.Authorization;

/// <summary>
/// Resolves the full set of permission strings a principal holds, merging permissions carried as
/// claims in the token with per-tenant grants stored in the platform database. Implemented in the
/// infrastructure layer; cached per request.
/// </summary>
public interface IPermissionResolver
{
    Task<IReadOnlySet<string>> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
