using Cortex.Application.Usage;
using Cortex.Core.Identity;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Usage;

/// <summary>
/// Reads accumulated token usage from the append-only audit store. The audit context has no global
/// tenant filter, so the tenant is applied explicitly here.
/// </summary>
public sealed class TokenUsageReader(AuditDbContext db, ICurrentUser currentUser) : ITokenUsageReader
{
    public async Task<long> GetConversationTotalAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.TenantId ?? Guid.Empty;
        return await db.TokenUsage
            .Where(u => u.TenantId == tenantId && u.ConversationId == conversationId)
            .SumAsync(u => (long?)u.TotalTokens, cancellationToken) ?? 0L;
    }
}
