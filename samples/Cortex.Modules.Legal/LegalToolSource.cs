using Cortex.Application.Authorization;
using Cortex.Modules.Sdk;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Modules.Legal;

/// <summary>Supplies the Legal module's executable tools.</summary>
public sealed class LegalToolSource : IModuleToolSource
{
    public string ModuleId => LegalModule.Id;

    public IReadOnlyList<ModuleTool> GetTools(IServiceProvider scopedServices)
    {
        var tools = scopedServices.GetRequiredService<LegalTools>();

        return
        [
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "search_clauses",
                Permission = Permissions.ForTool(ModuleId, "search_clauses"),
                Function = AIFunctionFactory.Create(tools.SearchClauses, name: "search_clauses"),
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "draft_clause",
                Permission = Permissions.ForTool(ModuleId, "draft_clause"),
                Function = AIFunctionFactory.Create(tools.DraftClause, name: "draft_clause"),
            },
        ];
    }
}
