using Cortex.Application.Usage;

namespace Cortex.Application.Auditing;

/// <summary>
/// Writes audit records to the dedicated append-only audit store. Implementations enqueue through the
/// outbox so audit writes never block or fail the user-facing operation.
/// </summary>
public interface IAuditLog
{
    Task RecordToolCallAsync(ToolCallAuditEntry entry, CancellationToken cancellationToken = default);

    Task RecordAuthEventAsync(AuthAuditEntry entry, CancellationToken cancellationToken = default);

    Task RecordEntityChangesAsync(IReadOnlyCollection<EntityChangeAuditEntry> entries, CancellationToken cancellationToken = default);

    Task RecordTokenUsageAsync(TokenUsageRecord record, CancellationToken cancellationToken = default);
}
