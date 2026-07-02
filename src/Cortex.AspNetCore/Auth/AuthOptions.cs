namespace Cortex.AspNetCore.Auth;

/// <summary>JWT / OIDC settings, bound from the "Auth" section. In production these point at Entra External ID.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>OIDC authority (e.g. https://&lt;tenant&gt;.ciamlogin.com/&lt;tenant-id&gt;/v2.0). Empty disables JWT validation.</summary>
    public string? Authority { get; set; }

    /// <summary>Expected audience (the API's application/client id).</summary>
    public string? Audience { get; set; }

    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Claim whose value identifies the Cortex tenant (matched against <c>Tenant.Slug</c>).</summary>
    public string TenantClaim { get; set; } = "tenant";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Authority);
}
