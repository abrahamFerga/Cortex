using Cortex.Core.Entities;

namespace Cortex.Core.Platform;

/// <summary>An isolation boundary: an organization using the platform. Root of all tenant-owned data.</summary>
public sealed class Tenant : EntityBase
{
    public required string Name { get; set; }

    /// <summary>URL-safe unique key used in routing / invitations.</summary>
    public required string Slug { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<TenantModule> Modules { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
}
