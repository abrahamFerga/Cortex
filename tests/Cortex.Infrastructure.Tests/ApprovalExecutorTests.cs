using Cortex.Core.Platform;
using Cortex.Infrastructure.Agents;
using Cortex.Infrastructure.Approvals;
using Cortex.Modules.Sdk;
using Microsoft.Extensions.AI;

namespace Cortex.Infrastructure.Tests;

/// <summary>
/// Verifies the approval round-trip's execution half: approving a pending tool call resolves the tool
/// from its module's registry, coerces the recorded JSON arguments back to typed parameters, and runs it.
/// </summary>
public sealed class ApprovalExecutorTests
{
    [Fact]
    public async Task Execute_ResolvesTool_CoercesArgs_AndRuns()
    {
        (string Description, decimal Amount) captured = default;
        var fn = AIFunctionFactory.Create(
            (string description, decimal amount) => { captured = (description, amount); return "recorded"; },
            name: "record_transaction");

        var registry = new ToolRegistry([new FakeToolSource(new ModuleTool
        {
            ModuleId = "demo",
            Name = "record_transaction",
            Permission = "tools.demo.record_transaction",
            Function = fn,
        })]);

        var approval = new PendingApproval
        {
            TenantId = Guid.NewGuid(),
            ModuleId = "demo",
            ToolName = "record_transaction",
            ArgumentsJson = """{"description":"OXXO groceries","amount":42.50}""",
        };

        var result = await new ApprovalExecutor(registry).ExecuteAsync(approval, new EmptyServiceProvider(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("OXXO groceries", captured.Description);
        Assert.Equal(42.50m, captured.Amount);
        Assert.Equal("recorded", result.Result);
    }

    [Fact]
    public async Task Execute_UnknownTool_Fails()
    {
        var registry = new ToolRegistry([]);

        var approval = new PendingApproval
        {
            TenantId = Guid.NewGuid(),
            ModuleId = "demo",
            ToolName = "ghost_tool",
            ArgumentsJson = "{}",
        };

        var result = await new ApprovalExecutor(registry).ExecuteAsync(approval, new EmptyServiceProvider(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ghost_tool", result.Error!, StringComparison.Ordinal);
    }

    private sealed class FakeToolSource(ModuleTool tool) : IModuleToolSource
    {
        public string ModuleId => "demo";
        public IReadOnlyList<ModuleTool> GetTools(IServiceProvider scopedServices) => [tool];
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
