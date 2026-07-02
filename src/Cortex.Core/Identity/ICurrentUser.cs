namespace Cortex.Core.Identity;

/// <summary>
/// The authenticated user behind the current operation. Backed by the request principal in the API
/// and resolvable from background workers via the operation's captured context.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Stable platform user id (our row), or <c>null</c> when unauthenticated.</summary>
    Guid? UserId { get; }

    /// <summary>The external identity provider subject (OIDC <c>sub</c>) for this user.</summary>
    string? Subject { get; }

    /// <summary>Display name / email, best-effort, for audit attribution.</summary>
    string? DisplayName { get; }

    /// <summary>Tenant the user is acting within.</summary>
    Guid? TenantId { get; }

    bool IsAuthenticated { get; }

    /// <summary>Permission strings granted for this request (claims + tenant grants merged).</summary>
    IReadOnlySet<string> Permissions { get; }

    bool HasPermission(string permission);
}
