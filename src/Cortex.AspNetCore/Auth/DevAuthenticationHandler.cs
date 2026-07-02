using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Cortex.AspNetCore.Auth;

/// <summary>
/// Development-only authentication: turns optional <c>X-Dev-*</c> headers (or sensible defaults) into an
/// authenticated principal so the platform is fully exercisable without standing up an identity provider.
/// Registered ONLY when no real authority is configured and the environment is Development.
/// </summary>
public sealed class DevAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Dev";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var subject = Header("X-Dev-Subject", "dev-user");
        var email = Header("X-Dev-Email", "dev@cortex.local");
        var name = Header("X-Dev-Name", "Dev User");
        var tenant = Header("X-Dev-Tenant", "dev");
        var roles = Header("X-Dev-Roles", "system_admin").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new("sub", subject),
            new(ClaimTypes.Email, email),
            new("name", name),
            new("tenant", tenant),
        };
        claims.AddRange(roles.Select(r => new Claim("roles", r)));

        var identity = new ClaimsIdentity(claims, SchemeName, "name", "roles");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string Header(string key, string fallback) =>
        Request.Headers.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : fallback;
}
