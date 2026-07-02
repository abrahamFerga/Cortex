using Microsoft.Extensions.AI;

namespace Cortex.Modules.Sdk;

/// <summary>
/// The executable counterpart of a <see cref="ToolDescriptor"/>: a concrete <see cref="AIFunction"/>
/// coupled to the permission required to call it and the module that owns it. The agent runner builds
/// the per-request tool set from these, filtering by permission before the model ever sees a schema.
/// </summary>
public sealed class ModuleTool
{
    public required string ModuleId { get; init; }

    /// <summary>Must equal the underlying <see cref="AIFunction"/> name and the declaring descriptor's name.</summary>
    public required string Name { get; init; }

    /// <summary>Permission gating this tool. Checked pre-model-call.</summary>
    public required string Permission { get; init; }

    /// <summary>The invocable function handed to the agent.</summary>
    public required AIFunction Function { get; init; }

    /// <summary>Side-effecting tool that should be wrapped for human approval.</summary>
    public bool RequiresApproval { get; init; }

    /// <summary>Whether invocations are audited. Defaults to on.</summary>
    public bool Audit { get; init; } = true;
}
