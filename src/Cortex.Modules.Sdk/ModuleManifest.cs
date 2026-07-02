namespace Cortex.Modules.Sdk;

/// <summary>
/// The manifest-first declaration of everything a domain module offers: its identity, the tools it
/// exposes to the agent, the dashboard tabs it contributes, the roles that unlock it, and the system
/// instructions that steer its chat. Declared statically so the platform can enumerate capabilities,
/// build the navigation, and enforce security without executing module code.
/// </summary>
public sealed record ModuleManifest
{
    /// <summary>Stable lowercase identifier, e.g. "finance", "legal", "nutrition".</summary>
    public required string Id { get; init; }

    /// <summary>Human-facing name shown in the module switcher.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Semantic version of the module.</summary>
    public required string Version { get; init; }

    public string? Description { get; init; }

    /// <summary>Optional icon name for the module switcher.</summary>
    public string? Icon { get; init; }

    /// <summary>Tools this module exposes to the agent (metadata; executables registered separately).</summary>
    public IReadOnlyList<ToolDescriptor> Tools { get; init; } = [];

    /// <summary>Dashboard tabs this module contributes.</summary>
    public IReadOnlyList<TabDescriptor> Tabs { get; init; } = [];

    /// <summary>Roles that, when assigned to a user, grant access to this module.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// System instructions injected into the agent when chatting within this module's context.
    /// Lets each domain steer tone, guardrails, and tool usage independently.
    /// </summary>
    public string? AgentInstructions { get; init; }

    /// <summary>
    /// A few example prompts the chat surfaces as one-click starters, so a newcomer can immediately
    /// exercise the module's tools (e.g. "Summarize my spending") without knowing what to type. Optional.
    /// </summary>
    public IReadOnlyList<string> SuggestedPrompts { get; init; } = [];
}
