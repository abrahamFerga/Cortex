namespace Cortex.Application.Usage;

/// <summary>Reads accumulated token usage from the audit store (for budget enforcement and reporting).</summary>
public interface ITokenUsageReader
{
    /// <summary>Total tokens consumed across all completed turns of a conversation (tenant-scoped).</summary>
    Task<long> GetConversationTotalAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
