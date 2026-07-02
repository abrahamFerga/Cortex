using Cortex.Application.Authorization;
using Cortex.Modules.Sdk;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Modules.Legal;

/// <summary>
/// The Legal vertical (the-lawyer) — a third domain module, proving Cortex spans very different
/// industries from one codebase: Finance (stateful + rule-based categorizing), Nutrition (stateless reference data),
/// and now Legal (stateless clause library + drafting). The same SDK, RBAC, audit, token-usage, and
/// AG-UI chat all apply with no platform changes.
/// </summary>
public sealed class LegalModule : IModule
{
    public const string Id = "legal";

    public const string ViewClauses = "legal.clauses.view";
    public const string ViewMatters = "legal.matters.view";

    public ModuleManifest Manifest { get; } = new()
    {
        Id = Id,
        DisplayName = "Legal",
        Version = "1.0.0",
        Description = "Legal assistant. Search a standard clause library and draft contract clauses for review.",
        Icon = "scale",
        AgentInstructions =
            "You are Cortex's legal assistant. Use search_clauses to find standard clauses and draft_clause to " +
            "produce a templated clause for the named parties. Always make clear that output is a starting template, " +
            "not legal advice, and recommend review by a licensed attorney. Never invent statutes, case citations, or " +
            "jurisdiction-specific rules; if asked for those, say a qualified lawyer must confirm them.",
        SuggestedPrompts =
        [
            "Draft a confidentiality clause",
            "Search the clause library for indemnification",
        ],
        Roles = ["legal:user", "legal:admin"],
        Tools =
        [
            new ToolDescriptor
            {
                Name = "search_clauses",
                Description = "Search the standard clause library by keyword; returns clause titles, categories, and summaries.",
                Permission = Permissions.ForTool(Id, "search_clauses"),
            },
            new ToolDescriptor
            {
                Name = "draft_clause",
                Description = "Draft a standard contract clause filled in with the two party names.",
                Permission = Permissions.ForTool(Id, "draft_clause"),
            },
        ],
        Tabs =
        [
            new TabDescriptor { Id = "chat", Label = "Chat", Route = "/legal/chat", Icon = "message-circle", Order = 0 },
            new TabDescriptor
            {
                Id = "matters", Label = "Matters", Route = "/legal/matters", Icon = "folder", Order = 1, Permission = ViewMatters,
                Placeholder = "Your legal matters and their associated documents will appear here. Use the assistant to draft clauses for a matter.",
            },
            new TabDescriptor
            {
                Id = "clauses", Label = "Clauses", Route = "/legal/clauses", Icon = "file-text", Order = 2,
                Permission = ViewClauses,
                DataEndpoint = "/api/legal/clauses",
                Columns = [new("title", "Clause"), new("category", "Category"), new("summary", "Summary")],
            },
        ],
    };

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<LegalTools>();
        services.AddSingleton<IModuleToolSource, LegalToolSource>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/legal").WithTags("Legal").RequireAuthorization();

        // The clause library (reference data, not tenant-scoped).
        group.MapGet("/clauses", (string? query) =>
            {
                var clauses = string.IsNullOrWhiteSpace(query)
                    ? LegalCatalog.Clauses
                    : [.. LegalCatalog.Search(query)];
                return Results.Ok(clauses);
            })
            .RequireAuthorization(PermissionRequirement.PolicyName(ViewClauses))
            .WithName("Legal_GetClauses");
    }
}
