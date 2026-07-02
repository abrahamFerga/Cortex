using Cortex.Application.Agents;
using Cortex.Application.Ai;
using Cortex.Application.Approvals;
using Cortex.Application.Auditing;
using Cortex.Application.Authorization;
using Cortex.Application.Conversations;
using Cortex.Application.Modules;
using Cortex.Application.Usage;
using Cortex.Core.Identity;
using Cortex.Core.Multitenancy;
using Cortex.Infrastructure.Agents;
using Cortex.Infrastructure.Ai;
using Cortex.Infrastructure.Approvals;
using Cortex.Infrastructure.Auditing;
using Cortex.Infrastructure.Authorization;
using Cortex.Infrastructure.Context;
using Cortex.Infrastructure.Conversations;
using Cortex.Infrastructure.Modules;
using Cortex.Infrastructure.Persistence;
using Cortex.Infrastructure.Persistence.Interceptors;
using Cortex.Infrastructure.Usage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cortex.Infrastructure;

/// <summary>Wires the platform's infrastructure: persistence, multi-tenancy, RBAC, auditing, and the agent stack.</summary>
public static class InfrastructureSetup
{
    public static IHostApplicationBuilder AddCortexInfrastructure(this IHostApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var services = builder.Services;

        AddRequestContext(services);
        AddPersistence(builder);
        AddAuthorization(services);
        AddAuditing(services);
        AddAgentStack(builder);

        return builder;
    }

    private static void AddRequestContext(IServiceCollection services)
    {
        // One scoped object backs both the current-user and tenant abstractions.
        services.AddScoped<RequestContext>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<RequestContext>());
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<RequestContext>());
    }

    private static void AddPersistence(IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddScoped<AuditInterceptor>();

        // Platform DB: registered explicitly so the scoped audit interceptor can be injected, then
        // enriched with Aspire health checks + telemetry.
        services.AddDbContext<PlatformDbContext>((sp, options) =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString(PlatformDbContext.ConnectionName));
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });
        builder.EnrichNpgsqlDbContext<PlatformDbContext>();

        // Audit DB: append-only, no interceptor — wired through the Aspire integration directly.
        builder.AddNpgsqlDbContext<AuditDbContext>(AuditDbContext.ConnectionName);

        // Durable audit outbox: a minimal, interceptor-free context over the platform DB. PlatformDbContext's
        // migration creates the audit_outbox table; this context only reads/writes it.
        services.AddDbContext<OutboxDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString(OutboxDbContext.ConnectionName)));
    }

    private static void AddAuthorization(IServiceCollection services)
    {
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    }

    private static void AddAuditing(IServiceCollection services)
    {
        services.AddScoped<IAuditLog, AuditLog>();
        // Flushes any audit records the durable outbox captured during an audit-DB outage.
        services.AddHostedService<AuditOutboxProcessor>();
    }

    private static void AddAgentStack(IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
        var aiOptions = builder.Configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        // Fail fast at startup on a misconfigured provider (e.g. OpenAI without a key) rather than on the
        // first chat, where the IChatClient is otherwise built lazily.
        AiOptionsValidator.ThrowIfInvalid(aiOptions);
        if (aiOptions.IsEnabled)
        {
            services.AddSingleton<IChatClient>(_ => ChatClientFactory.Create(aiOptions));
        }

        services.AddSingleton<IModuleCatalog, ModuleCatalog>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddScoped<ITenantModuleStore, TenantModuleStore>();
        services.AddScoped<ITenantAiSettings, TenantAiSettingsResolver>();
        services.AddScoped<IConversationStore, ConversationStore>();
        services.AddScoped<ITokenUsageReader, TokenUsageReader>();
        services.AddScoped<IApprovalStore, ApprovalStore>();
        services.AddScoped<ApprovalExecutor>();
        services.AddScoped<IAuthorizedAgentRunner, AuthorizedAgentRunner>();
    }
}
