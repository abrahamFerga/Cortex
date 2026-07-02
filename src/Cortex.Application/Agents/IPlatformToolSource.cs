using Cortex.Modules.Sdk;

namespace Cortex.Application.Agents;

/// <summary>
/// Supplies platform-wide tools that every module's agent receives (documents, files, …), in
/// addition to the module's own <see cref="IModuleToolSource"/> tools. Same contract: built inside
/// the request scope, each tool gated by its permission before the model ever sees the schema.
/// </summary>
public interface IPlatformToolSource
{
    /// <summary>Build the platform tools using services resolved from the current scope.</summary>
    public IReadOnlyList<ModuleTool> GetTools(IServiceProvider scopedServices);
}
