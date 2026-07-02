using Cortex.Core.Entities;
using Cortex.Core.Multitenancy;

namespace Cortex.Core.Platform;

public enum MessageRole
{
    User = 0,
    Assistant = 1,
}

/// <summary>A persisted message in a <see cref="Conversation"/>, kept for display and history.</summary>
public sealed class ConversationMessage : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid ConversationId { get; set; }

    public required MessageRole Role { get; set; }
    public required string Content { get; set; }
}
