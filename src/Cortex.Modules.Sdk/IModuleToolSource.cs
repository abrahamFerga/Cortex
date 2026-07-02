using Microsoft.Extensions.AI;

namespace Cortex.Modules.Sdk;

/// <summary>
/// Supplies a module's executable tools. Invoked inside the active request scope so the produced
/// <see cref="AIFunction"/>s may close over scoped services (DbContext, current user, etc.).
/// A module registers exactly one source for itself.
/// </summary>
public interface IModuleToolSource
{
    /// <summary>The module these tools belong to. Must match the module's manifest id.</summary>
    string ModuleId { get; }

    /// <summary>Build the module's tools using services resolved from the current scope.</summary>
    IReadOnlyList<ModuleTool> GetTools(IServiceProvider scopedServices);
}
