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
        var clauses = scopedServices.GetRequiredService<LegalTools>();
        var matters = scopedServices.GetRequiredService<MatterTools>();

        return
        [
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "search_clauses",
                Permission = Permissions.ForTool(ModuleId, "search_clauses"),
                Function = AIFunctionFactory.Create(clauses.SearchClauses, name: "search_clauses"),
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "draft_clause",
                Permission = Permissions.ForTool(ModuleId, "draft_clause"),
                Function = AIFunctionFactory.Create(clauses.DraftClause, name: "draft_clause"),
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "create_matter",
                Permission = Permissions.ForTool(ModuleId, "create_matter"),
                Function = AIFunctionFactory.Create(matters.CreateMatter, name: "create_matter"),
                RequiresApproval = true,
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "list_matters",
                Permission = Permissions.ForTool(ModuleId, "list_matters"),
                Function = AIFunctionFactory.Create(matters.ListMatters, name: "list_matters"),
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "attach_document_to_matter",
                Permission = Permissions.ForTool(ModuleId, "attach_document_to_matter"),
                Function = AIFunctionFactory.Create(matters.AttachDocumentToMatter, name: "attach_document_to_matter"),
                RequiresApproval = true,
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "list_matter_documents",
                Permission = Permissions.ForTool(ModuleId, "list_matter_documents"),
                Function = AIFunctionFactory.Create(matters.ListMatterDocuments, name: "list_matter_documents"),
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "get_playbook",
                Permission = Permissions.ForTool(ModuleId, "get_playbook"),
                Function = AIFunctionFactory.Create(clauses.GetPlaybook, name: "get_playbook"),
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "start_bulk_review",
                Permission = Permissions.ForTool(ModuleId, "start_bulk_review"),
                Function = AIFunctionFactory.Create(matters.StartBulkReview, name: "start_bulk_review"),
                RequiresApproval = true,
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "index_matter_documents",
                Permission = Permissions.ForTool(ModuleId, "index_matter_documents"),
                Function = AIFunctionFactory.Create(matters.IndexMatterDocuments, name: "index_matter_documents"),
                RequiresApproval = true,
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "restrict_matter_access",
                Permission = Permissions.ForTool(ModuleId, "restrict_matter_access"),
                Function = AIFunctionFactory.Create(matters.RestrictMatterAccess, name: "restrict_matter_access"),
                RequiresApproval = true,
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "open_matter_access",
                Permission = Permissions.ForTool(ModuleId, "open_matter_access"),
                Function = AIFunctionFactory.Create(matters.OpenMatterAccess, name: "open_matter_access"),
                RequiresApproval = true,
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "connect_matter_folder",
                Permission = Permissions.ForTool(ModuleId, "connect_matter_folder"),
                Function = AIFunctionFactory.Create(matters.ConnectMatterFolder, name: "connect_matter_folder"),
                RequiresApproval = true,
            },
            new ModuleTool
            {
                ModuleId = ModuleId,
                Name = "sync_matter_folder",
                Permission = Permissions.ForTool(ModuleId, "sync_matter_folder"),
                Function = AIFunctionFactory.Create(matters.SyncMatterFolder, name: "sync_matter_folder"),
                RequiresApproval = true,
            },
        ];
    }
}
