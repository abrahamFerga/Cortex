using System.Text.Json;
using Cortex.Application.Connectors;
using Cortex.Application.Secrets;
using Cortex.Core.Identity;
using Cortex.Core.Platform;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Connectors;

/// <summary>
/// Per-user delegated-connector sessions: tokens live in the configured secret vault (referenced
/// from <see cref="UserConnectorLogin"/> rows), are refreshed transparently when expired (and a
/// refresh token exists), and are deleted wholesale when an admin disables the connector —
/// disable revokes, re-enable forces re-auth.
/// </summary>
public sealed class ConnectorUserLoginService(
    PlatformDbContext db,
    ICurrentUser currentUser,
    ConnectorSettingsService settings,
    IOAuthTokenClient oauth,
    ISecretVault vault) : IConnectorUserLogins
{
    private const string SecretScope = "Cortex.Connectors.UserTokens";

    public async Task<string?> GetAccessTokenAsync(string connectorId, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not Guid userId)
        {
            return null;
        }

        var login = await db.UserConnectorLogins
            .FirstOrDefaultAsync(l => l.UserId == userId && l.ConnectorId == connectorId, cancellationToken);
        if (login is null)
        {
            return null;
        }

        var tokens = JsonSerializer.Deserialize<OAuthTokens>(
            await vault.RevealAsync(SecretScope, login.ProtectedTokensJson, cancellationToken));
        if (tokens is null)
        {
            return null;
        }

        if (tokens.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return tokens.AccessToken;
        }

        // Expired: refresh when possible, otherwise the session is over (fail closed → reconnect).
        if (tokens.RefreshToken is null)
        {
            return null;
        }

        var config = await ResolveOAuthConfigAsync(connectorId, cancellationToken);
        if (config is null)
        {
            return null;
        }

        var refreshed = await oauth.RefreshAsync(config, tokens.RefreshToken, cancellationToken);
        if (refreshed is null)
        {
            return null;
        }

        await SaveAsync(login, refreshed, cancellationToken);
        return refreshed.AccessToken;
    }

    /// <summary>Stores (or replaces) the current user's session after an OAuth callback.</summary>
    public async Task StoreAsync(string connectorId, OAuthTokens tokens, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");
        var tenantId = currentUser.TenantId ?? throw new InvalidOperationException("No tenant.");

        var login = await db.UserConnectorLogins
            .FirstOrDefaultAsync(l => l.UserId == userId && l.ConnectorId == connectorId, cancellationToken);
        if (login is null)
        {
            login = new UserConnectorLogin
            {
                TenantId = tenantId,
                UserId = userId,
                ConnectorId = connectorId,
                ProtectedTokensJson = "",
            };
            db.UserConnectorLogins.Add(login);
        }

        await SaveAsync(login, tokens, cancellationToken);
    }

    /// <summary>The OAuth client config from the connector's tenant settings (delegated connectors).</summary>
    public async Task<OAuthClientConfig?> ResolveOAuthConfigAsync(string connectorId, CancellationToken cancellationToken = default)
    {
        var values = await ((Cortex.Connectors.Sdk.IConnectorSettings)settings).GetAsync(connectorId, cancellationToken);
        if (values is null ||
            !values.TryGetValue("Authority", out var authority) ||
            !values.TryGetValue("ClientId", out var clientId) ||
            string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        values.TryGetValue("ClientSecret", out var clientSecret);
        var scopes = values.TryGetValue("Scopes", out var s) && !string.IsNullOrWhiteSpace(s)
            ? s
            : "offline_access";
        return new OAuthClientConfig($"{authority.TrimEnd('/')}/oauth2/v2.0/token", clientId, clientSecret, scopes);
    }

    private async Task SaveAsync(
        UserConnectorLogin login, OAuthTokens tokens, CancellationToken cancellationToken)
    {
        var previous = login.ProtectedTokensJson;
        login.ProtectedTokensJson = await vault.StoreAsync(SecretScope, JsonSerializer.Serialize(tokens), cancellationToken);
        login.ExpiresAt = tokens.ExpiresAt;
        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(previous))
        {
            await vault.ForgetAsync(previous, cancellationToken);
        }
    }
}
