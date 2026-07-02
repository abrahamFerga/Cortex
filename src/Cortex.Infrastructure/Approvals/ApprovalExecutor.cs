using System.Text.Json;
using Cortex.Application.Agents;
using Cortex.Core.Platform;
using Microsoft.Extensions.AI;

namespace Cortex.Infrastructure.Approvals;

/// <summary>The outcome of executing an approved tool call.</summary>
public readonly record struct ApprovalExecutionResult(bool Success, string? Result, string? Error);

/// <summary>
/// Re-executes an approved, side-effecting tool call with its recorded arguments. Resolves the tool from
/// the module's registered tool source within the request scope (so the tool's scoped services — its
/// DbContext, the current tenant — are wired), then invokes it. Argument coercion from the stored JSON
/// back into the tool's typed parameters is handled by the <see cref="AIFunction"/> itself.
/// </summary>
public sealed class ApprovalExecutor(IToolRegistry toolRegistry)
{
    public async Task<ApprovalExecutionResult> ExecuteAsync(
        PendingApproval approval, IServiceProvider scopedServices, CancellationToken cancellationToken = default)
    {
        var tool = toolRegistry.GetModuleTools(approval.ModuleId, scopedServices)
            .FirstOrDefault(t => string.Equals(t.Name, approval.ToolName, StringComparison.Ordinal));

        if (tool is null)
        {
            return new ApprovalExecutionResult(false, null, $"Tool '{approval.ToolName}' is no longer available.");
        }

        try
        {
            var arguments = DeserializeArguments(approval.ArgumentsJson);
            var result = await tool.Function.InvokeAsync(arguments, cancellationToken);
            return new ApprovalExecutionResult(true, result?.ToString(), null);
        }
        catch (Exception ex)
        {
            return new ApprovalExecutionResult(false, null, ex.Message);
        }
    }

    private static AIFunctionArguments DeserializeArguments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AIFunctionArguments();
        }

        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
        return new AIFunctionArguments(parsed.ToDictionary(kv => kv.Key, kv => (object?)kv.Value));
    }
}
