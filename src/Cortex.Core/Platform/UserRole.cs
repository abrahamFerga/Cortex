using Cortex.Core.Entities;
using Cortex.Core.Multitenancy;

namespace Cortex.Core.Platform;

/// <summary>Assignment of a system role to a user (Layer 1 of the RBAC model).</summary>
public sealed class UserRole : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public required string Role { get; set; }
}
