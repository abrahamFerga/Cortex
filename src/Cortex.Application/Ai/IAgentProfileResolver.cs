using Cortex.Core.Platform;

namespace Cortex.Application.Ai;

/// <summary>Resolves the tenant's active (default) agent profile for a module, if any.</summary>
public interface IAgentProfileResolver
{
    public Task<AgentProfile?> ResolveActiveAsync(string moduleId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Composes the effective agent instructions for a turn: tenant system prompt, then the module
/// manifest's instructions, then the active profile per its mode. Pure — the merge semantics are
/// the contract admins rely on when they pick Append vs Replace, so they are unit-tested.
/// </summary>
public static class InstructionComposer
{
    public static string Compose(string systemPrompt, string? manifestInstructions, AgentProfile? profile)
    {
        var parts = new List<string>(3) { systemPrompt };

        if (profile is { Mode: AgentProfileMode.Replace })
        {
            parts.Add(profile.Instructions);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(manifestInstructions))
            {
                parts.Add(manifestInstructions);
            }

            if (profile is not null)
            {
                parts.Add(profile.Instructions);
            }
        }

        return string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
