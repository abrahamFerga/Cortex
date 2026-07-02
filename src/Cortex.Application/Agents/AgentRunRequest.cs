namespace Cortex.Application.Agents;

/// <summary>A single user turn against a module's agent.</summary>
public sealed record AgentRunRequest
{
    /// <summary>The module whose tools and instructions scope this conversation.</summary>
    public required string ModuleId { get; init; }

    /// <summary>Existing conversation to continue, or <c>null</c> to start a new one.</summary>
    public Guid? ConversationId { get; init; }

    /// <summary>The user's message.</summary>
    public required string Message { get; init; }
}
