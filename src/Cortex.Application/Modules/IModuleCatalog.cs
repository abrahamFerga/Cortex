using Cortex.Modules.Sdk;

namespace Cortex.Application.Modules;

/// <summary>
/// The set of modules loaded into this host. Built once at startup from discovered <see cref="IModule"/>
/// implementations. Drives the dashboard's navigation, the agent's per-module instructions, and the
/// platform modules endpoint.
/// </summary>
public interface IModuleCatalog
{
    IReadOnlyList<ModuleManifest> Manifests { get; }

    bool TryGetManifest(string moduleId, out ModuleManifest? manifest);
}
