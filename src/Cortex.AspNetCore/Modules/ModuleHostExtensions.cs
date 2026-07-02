using Cortex.Application.Modules;
using Cortex.AspNetCore.Setup;
using Cortex.Core.Multitenancy;
using Cortex.Infrastructure.Context;
using Cortex.Infrastructure.Persistence;
using Cortex.Modules.Sdk;
using Microsoft.EntityFrameworkCore;

namespace Cortex.AspNetCore.Modules;

/// <summary>
/// Host-side module wiring. Each installed module is instantiated once, given a chance to register its
/// services (manifest-first), and exposed as an <see cref="IModule"/> singleton so the catalog and
/// endpoint mapping can enumerate them.
/// </summary>
public static class ModuleHostExtensions
{
    /// <summary>
    /// Registers a Cortex module in the host. Call once per module in <c>Program.cs</c>.
    /// The module's services are added to the DI container and its manifest is exposed
    /// to the catalog. Modules can be shipped as NuGet packages.
    /// </summary>
    public static IHostApplicationBuilder AddCortexModule<TModule>(this IHostApplicationBuilder builder)
        where TModule : class, IModule, new()
    {
        var module = new TModule();
        module.RegisterServices(builder.Services, builder.Configuration);
        builder.Services.AddSingleton<IModule>(module);
        return builder;
    }

    public static void MapCortexModules(this IEndpointRouteBuilder app)
    {
        // Build the module catalog now so an invalid registration (duplicate ids, colliding tab routes)
        // fails fast at startup with a clear message, instead of on the first request that resolves it.
        app.ServiceProvider.GetRequiredService<IModuleCatalog>();

        foreach (var module in app.ServiceProvider.GetServices<IModule>())
        {
            // Wrap each module's endpoints in a group whose filter 404s every route when the module is
            // disabled for the caller's tenant — a tenant-scoped kill switch, consistent with the workspace
            // catalog (which hides it) and the agent runner (which refuses chat to it).
            var moduleEndpoints = app.MapGroup("").AddEndpointFilter(new ModuleEnabledFilter(module.Manifest.Id));
            module.MapEndpoints(moduleEndpoints);
        }
    }

    public static async Task MigrateCortexModulesAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        foreach (var module in scope.ServiceProvider.GetServices<IModule>())
        {
            await module.MigrateAsync(scope.ServiceProvider, cancellationToken);
        }
    }

    public static async Task SeedCortexModulesAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        // In Development, run module seeding inside the dev tenant's context so a module can seed
        // tenant-owned demo data against ITenantContext exactly as its request handlers do. In other
        // environments there is no ambient tenant here, so tenant-scoped demo seeds correctly no-op.
        await EstablishDevTenantContextAsync(services, cancellationToken);

        foreach (var module in services.GetServices<IModule>())
        {
            await module.SeedAsync(services, cancellationToken);
        }
    }

    private static async Task EstablishDevTenantContextAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var environment = services.GetService<IHostEnvironment>();
        if (environment is null || !environment.IsDevelopment())
        {
            return;
        }

        // RequestContext is the mutable ITenantContext; without it (or the dev tenant) there is nothing to set.
        if (services.GetService<RequestContext>() is not { } context)
        {
            return;
        }

        var platform = services.GetRequiredService<PlatformDbContext>();
        var devTenant = await platform.Tenants
            .FirstOrDefaultAsync(t => t.Slug == DatabaseInitializer.DevTenantSlug, cancellationToken);
        if (devTenant is not null)
        {
            context.SetTenant(devTenant.Id);
        }
    }
}
