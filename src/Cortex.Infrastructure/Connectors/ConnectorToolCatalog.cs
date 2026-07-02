using Cortex.Application.Connectors;
using Cortex.Connectors.Sdk;
using Cortex.Modules.Sdk;

namespace Cortex.Infrastructure.Connectors;

/// <summary>
/// The agent runner's connector-tool feed: aggregates every registered
/// <see cref="IConnectorToolSource"/> whose connector the current tenant has enabled. A disabled
/// connector's tools are never even built — hidden and uninvocable, the same guarantee module
/// enablement gives — and each surviving tool still passes the runner's per-permission filter.
/// </summary>
public sealed class ConnectorToolCatalog(
    IEnumerable<IConnectorToolSource> sources,
    ITenantConnectorStore store) : IConnectorToolCatalog
{
    public async Task<IReadOnlyList<ModuleTool>> GetEnabledToolsAsync(
        IServiceProvider scopedServices, CancellationToken cancellationToken = default)
    {
        var enabled = await store.GetEnabledConnectorIdsAsync(cancellationToken);
        if (enabled.Count == 0)
        {
            return [];
        }

        var tools = new List<ModuleTool>();
        foreach (var source in sources)
        {
            if (enabled.Contains(source.ConnectorId))
            {
                tools.AddRange(source.GetTools(scopedServices));
            }
        }

        return tools;
    }
}
