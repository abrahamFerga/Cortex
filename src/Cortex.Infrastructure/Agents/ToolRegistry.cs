using Cortex.Application.Agents;
using Cortex.Modules.Sdk;

namespace Cortex.Infrastructure.Agents;

/// <summary>
/// Aggregates module-contributed tool sources and resolves a module's tools within the caller's scope,
/// so the produced functions can use scoped services (DbContext, current user, …).
/// </summary>
public sealed class ToolRegistry(IEnumerable<IModuleToolSource> sources) : IToolRegistry
{
    public IReadOnlyList<ModuleTool> GetModuleTools(string moduleId, IServiceProvider scopedServices)
    {
        foreach (var source in sources)
        {
            if (string.Equals(source.ModuleId, moduleId, StringComparison.Ordinal))
            {
                return source.GetTools(scopedServices);
            }
        }

        return [];
    }
}
