namespace Cortex.Core.Entities;

/// <summary>
/// Entities that carry creation / modification provenance. Populated automatically by the
/// persistence layer's audit interceptor — application code never sets these by hand.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    string? CreatedBy { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    string? UpdatedBy { get; set; }
}
