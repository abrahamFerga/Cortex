namespace Cortex.Core.Entities;

/// <summary>Entities that are never hard-deleted; the audit trail must survive removal.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}
