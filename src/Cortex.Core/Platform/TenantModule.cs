using Cortex.Core.Entities;
using Cortex.Core.Multitenancy;

namespace Cortex.Core.Platform;

/// <summary>Records that a module is enabled for a tenant. Absence means the module is hidden for them.</summary>
public sealed class TenantModule : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }
    public required string ModuleId { get; set; }
    public bool IsEnabled { get; set; } = true;
}
