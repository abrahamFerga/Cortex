using Cortex.Modules.Sdk;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Api.Tests;

/// <summary>
/// A minimal in-test module so the chat pipeline has a valid, dev-seeded-enabled module to run a turn against.
/// It declares no tools or endpoints — just enough to exercise the agent path end to end (auth → the authorized
/// agent runner → the Mock chat client → a streamed reply → conversation persistence).
/// </summary>
internal sealed class TestModule : IModule
{
    /// <summary>The permission gating the module's <c>echo</c> tool (the conventional tools.&lt;module&gt;.&lt;tool&gt;).</summary>
    public const string EchoPermission = "tools.test.echo";

    /// <summary>The permission gating the module's side-effecting <c>record</c> tool.</summary>
    public const string RecordPermission = "tools.test.record";

    /// <summary>The permission gating the module's admin-console extension page.</summary>
    public const string AdminPagePermission = "test.admin";

    public ModuleManifest Manifest { get; } = new()
    {
        Id = "test",
        DisplayName = "Test Module",
        Version = "1.0.0",
        AgentInstructions = "You are a test assistant.",
        AdminTabs =
        [
            new TabDescriptor
            {
                Id = "widgets", Label = "Widget registry", Route = "/ext/test/widgets",
                Permission = AdminPagePermission,
                DataEndpoint = "/api/test/widgets",
                Columns = [new("name", "Name"), new("status", "Status")],
            },
        ],
        Tools =
        [
            new ToolDescriptor
            {
                Name = "echo",
                Description = "Echoes the given input back to the caller.",
                Permission = EchoPermission,
                RequiresApproval = false,
            },
            new ToolDescriptor
            {
                Name = "record",
                Description = "Records a value (side-effecting — requires human approval).",
                Permission = RecordPermission,
                RequiresApproval = true,
            },
        ],
    };

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // No module services needed for the pipeline test.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // No module endpoints needed for the pipeline test.
    }
}
