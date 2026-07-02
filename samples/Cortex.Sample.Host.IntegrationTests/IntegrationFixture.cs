using System.Collections.Concurrent;
using Cortex.Application.Auditing;
using Cortex.Application.Channels;
using Cortex.Core.Platform;
using Cortex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Cortex.Sample.Host.IntegrationTests;

/// <summary>
/// Boots the real sample API (Finance + Nutrition + Legal) over a throwaway Postgres container via
/// <see cref="WebApplicationFactory{TEntryPoint}"/>, so endpoint behaviour — auth, RBAC gating, the
/// approval gate, modules — is exercised through the full pipeline. The platform and audit contexts
/// share one container database (distinct schemas), and the chat assistant uses the Mock provider.
/// </summary>
public sealed class IntegrationFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = default!;

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public const string WhatsAppVerifyToken = "it-verify-token";
    public const string WhatsAppAppSecret = "it-whatsapp-app-secret";

    /// <summary>The same app with the WhatsApp channel enabled and the Cloud API sender replaced by a
    /// capturing fake — the channel is E2E-testable with no Meta account, credentials, or network.</summary>
    public WebApplicationFactory<Program> WhatsAppFactory { get; private set; } = default!;

    /// <summary>Outbound WhatsApp messages captured from <see cref="WhatsAppFactory"/>.</summary>
    public CapturingWhatsAppSender WhatsAppOutbox { get; } = new();

    public async Task InitializeAsync()
    {
        // Skip the resource-reaper sidecar, which can be flaky on Docker Desktop.
        Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");

        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase("cortex_platform")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await _postgres.StartAsync();

        // Point the host at the container. Environment variables override appsettings.Development.json
        // (which targets the local docker-compose Postgres) — the same mechanism Aspire uses.
        Environment.SetEnvironmentVariable("ConnectionStrings__cortex-platform", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__cortex-audit", _postgres.GetConnectionString());

        Factory = new CortexAppFactory();

        WhatsAppFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Channels:WhatsApp:Enabled", "true");
            builder.UseSetting("Channels:WhatsApp:VerifyToken", WhatsAppVerifyToken);
            builder.UseSetting("Channels:WhatsApp:AppSecret", WhatsAppAppSecret);
            builder.UseSetting("Channels:WhatsApp:AccessToken", "it-access-token");
            builder.UseSetting("Channels:WhatsApp:PhoneNumberId", "10000000001");
            builder.UseSetting("Channels:WhatsApp:ModuleId", "finance");
            builder.UseSetting("Channels:WhatsApp:TenantSlug", "dev");
            builder.ConfigureTestServices(services => services.AddSingleton<IWhatsAppSender>(WhatsAppOutbox));
        });

        // First client build starts the host, which runs migrations + seeding.
        using var warmup = Factory.CreateClient();
        using var alive = await warmup.GetAsync("/alive");
        alive.EnsureSuccessStatusCode();
    }

    /// <summary>An HTTP client authenticated (via dev-auth headers) as the given role in the dev tenant.</summary>
    public HttpClient ClientFor(string role)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Subject", $"it-{role}");
        client.DefaultRequestHeaders.Add("X-Dev-Tenant", "dev");
        client.DefaultRequestHeaders.Add("X-Dev-Roles", role);
        return client;
    }

    /// <summary>An HTTP client authenticated as the given role in an arbitrary tenant — for multi-tenant
    /// isolation tests. The tenant must already exist (see <see cref="EnsureTenantAsync"/>).</summary>
    public HttpClient ClientForTenant(string role, string tenant)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Subject", $"it-{role}-{tenant}");
        client.DefaultRequestHeaders.Add("X-Dev-Tenant", tenant);
        client.DefaultRequestHeaders.Add("X-Dev-Roles", role);
        return client;
    }

    /// <summary>Ensures a tenant with the given slug exists (only "dev" is seeded by default), so cross-tenant
    /// isolation can be exercised. Tenants are not tenant-owned, so no query filter applies.</summary>
    public async Task EnsureTenantAsync(string slug)
    {
        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        if (!await platform.Tenants.AnyAsync(t => t.Slug == slug))
        {
            platform.Tenants.Add(new Tenant { Name = $"{slug} tenant", Slug = slug });
            await platform.SaveChangesAsync();
        }
    }

    /// <summary>Loads a conversation with its messages straight from the database, bypassing tenant filters —
    /// for tests that assert on persisted conversation state (the message history).</summary>
    public async Task<Conversation> GetConversationAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await platform.Conversations
            .IgnoreQueryFilters()
            .Include(c => c.Messages)
            .FirstAsync(c => c.Id == id);
    }

    /// <summary>Reads auth-audit events of a given type for a tenant straight from the audit database — for
    /// tests that assert security-relevant actions were recorded in the append-only trail.</summary>
    public async Task<IReadOnlyList<AuthAuditEntry>> AuthEventsForTenantAsync(string slug, AuthAuditEventType type)
    {
        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tenant = await platform.Tenants.FirstAsync(t => t.Slug == slug);

        var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        return await audit.AuthEvents
            .Where(e => e.TenantId == tenant.Id && e.EventType == type)
            .ToListAsync();
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__cortex-platform", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__cortex-audit", null);

        if (WhatsAppFactory is not null)
        {
            await WhatsAppFactory.DisposeAsync();
        }

        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    private sealed class CortexAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<IntegrationFixture>;

/// <summary>An <see cref="IWhatsAppSender"/> that records what would have been sent to Meta.</summary>
public sealed class CapturingWhatsAppSender : IWhatsAppSender
{
    public ConcurrentQueue<(string To, string Text)> Sent { get; } = new();

    public Task SendTextAsync(string to, string text, CancellationToken cancellationToken = default)
    {
        Sent.Enqueue((to, text));
        return Task.CompletedTask;
    }
}
