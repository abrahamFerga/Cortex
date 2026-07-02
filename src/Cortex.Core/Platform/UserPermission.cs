using Cortex.Core.Entities;
using Cortex.Core.Multitenancy;

namespace Cortex.Core.Platform;

/// <summary>A direct permission grant to a user within a tenant (Layer 2 of the RBAC model).</summary>
public sealed class UserPermission : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Permission string, possibly a wildcard such as <c>tools.finance.*</c>.</summary>
    public required string Permission { get; set; }
}
