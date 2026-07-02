using Cortex.Core.Entities;
using Cortex.Core.Multitenancy;

namespace Cortex.Core.Platform;

/// <summary>A chat thread between a user and a module's agent. Its messages are persisted and replayed
/// to rebuild the agent's context on the next turn — that is how a conversation resumes across processes.</summary>
public sealed class Conversation : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The module context this conversation runs in.</summary>
    public required string ModuleId { get; set; }

    public string? Title { get; set; }

    /// <summary>
    /// Reserved for a serialized MAF <c>AgentSession</c>. Currently unused and always null: the agent runner
    /// runs without a MAF session and resumes a conversation by replaying its persisted <see cref="Messages"/>.
    /// </summary>
    public string? SessionState { get; set; }

    public ICollection<ConversationMessage> Messages { get; set; } = [];
}
