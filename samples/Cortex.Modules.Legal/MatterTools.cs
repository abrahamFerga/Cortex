using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Cortex.Application.Connectors;
using Cortex.Application.Files;
using Cortex.Application.Jobs;
using Cortex.Application.Rag;
using Cortex.Core.Identity;
using Cortex.Core.Multitenancy;
using Cortex.Modules.Legal.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Modules.Legal;

/// <summary>
/// Matter-workspace tools — the module's stateful core. Attaching platform files to a matter is the
/// documented "store this PDF as part of the case of Julia Assange" flow: the file id arrives via the
/// chat-attachment convention (any channel), and from then on the matter is the unit the agent reads,
/// drafts, and reports against. Creation and attachment are side-effecting and approval-gated.
///
/// Every matter lookup honors the ethical wall (<see cref="Matter.RestrictedUserIdsJson"/>): a
/// walled matter is indistinguishable from a missing one to anyone outside the wall. The optional
/// <see cref="IRagService"/> (null when Rag:Enabled is false) backs index_matter_documents.
/// </summary>
public sealed class MatterTools(
    LegalDbContext db,
    IFileStore files,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IJobQueue jobs,
    IConnectorBindingService bindings,
    IRagService? rag = null)
{
    [Description("Bind a matter to a folder in a connected data source (e.g. the local-folder or azure-blob connector) and start syncing it: new and changed files are attached to the matter and indexed for search_knowledge. One folder per matter; rebinding replaces it. Side-effecting and requires approval.")]
    public async Task<string> ConnectMatterFolder(
        [Description("The matter name to bind.")] string matterName,
        [Description("The folder/prefix within the connector (e.g. 'contracts/acme').")] string folderRef,
        [Description("The connector id (default 'local-folder'; e.g. 'azure-blob').")] string connector = "local-folder",
        CancellationToken cancellationToken = default)
    {
        var matter = await FindMatterAsync(matterName, cancellationToken);
        if (matter is null)
        {
            return $"No matter named '{matterName}' exists. Use list_matters to find the right name.";
        }

        var bindingId = await bindings.BindAsync(
            connector, LegalModule.Id, MatterRagGate.MatterResourceType, matter.Id, folderRef.Trim(), cancellationToken);
        var jobId = await jobs.EnqueueAsync(
            LegalModule.Id, ConnectorSyncJob.Kind, new ConnectorSyncArgs(bindingId), cancellationToken);

        return $"Bound matter '{matter.Name}' to '{folderRef}' on the '{connector}' connector and started the first sync. " +
               $"Job id: {jobId} (progress at /api/jobs/{jobId}). Synced files are attached to the matter and indexed; re-run with sync_matter_folder.";
    }

    [Description("Re-sync a matter's bound folder: new and changed files are attached and indexed; unchanged files are skipped. Side-effecting and requires approval.")]
    public async Task<string> SyncMatterFolder(
        [Description("The matter name whose bound folder to sync.")] string matterName,
        CancellationToken cancellationToken = default)
    {
        var matter = await FindMatterAsync(matterName, cancellationToken);
        if (matter is null)
        {
            return $"No matter named '{matterName}' exists. Use list_matters to find the right name.";
        }

        var bindingId = await bindings.FindAsync(
            LegalModule.Id, MatterRagGate.MatterResourceType, matter.Id, cancellationToken);
        if (bindingId is null)
        {
            return $"Matter '{matter.Name}' has no bound folder. Bind one first with connect_matter_folder.";
        }

        var jobId = await jobs.EnqueueAsync(
            LegalModule.Id, ConnectorSyncJob.Kind, new ConnectorSyncArgs(bindingId.Value), cancellationToken);
        return $"Started syncing matter '{matter.Name}' from its bound folder. Job id: {jobId} (progress at /api/jobs/{jobId}).";
    }

    [Description("Start a bulk review of ALL documents on a matter: every document is checked against every question, and the finished review table is filed on the matter as a PDF. Runs in the background.")]
    public async Task<string> StartBulkReview(
        [Description("The matter name whose documents to review.")] string matterName,
        [Description("The questions to answer per document, separated by semicolons or newlines.")] string questions,
        CancellationToken cancellationToken = default)
    {
        var matter = await FindMatterAsync(matterName, cancellationToken);
        if (matter is null)
        {
            return $"No matter named '{matterName}' exists. Use list_matters to find the right name.";
        }

        var parsed = questions
            .Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(q => q.Trim())
            .Where(q => q.Length > 0)
            .ToList();
        if (parsed.Count == 0)
        {
            return "Provide at least one question to answer per document.";
        }

        var documentCount = await db.MatterDocuments.CountAsync(d => d.MatterId == matter.Id, cancellationToken);
        if (documentCount == 0)
        {
            return $"Matter '{matter.Name}' has no documents to review. Attach documents first.";
        }

        var jobId = await jobs.EnqueueAsync(
            LegalModule.Id, BulkReviewJobHandler.JobKind,
            new BulkReviewArgs(matter.Id, matter.Name, parsed), cancellationToken);

        return $"Started a bulk review of {documentCount} document(s) on matter '{matter.Name}' against {parsed.Count} question(s). " +
               $"Job id: {jobId} (progress at /api/jobs/{jobId}). The review table will be filed on the matter as a PDF when it completes — check list_matter_documents.";
    }

    [Description("Create a new legal matter (an engagement workspace documents and work product attach to).")]
    public async Task<string> CreateMatter(
        [Description("The matter name, e.g. 'Julia Assange defense' or 'Acme / Initech NDA'.")] string name,
        [Description("Optional client name the matter is for.")] string? clientName = null,
        CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return "A matter needs a name.";
        }

        var existing = await FindMatterAsync(trimmed, cancellationToken);
        if (existing is not null)
        {
            return $"A matter named '{existing.Name}' already exists (id {existing.Id}). Use it, or pick a different name.";
        }

        var matter = new Matter
        {
            TenantId = tenant.RequireTenantId(),
            Name = trimmed,
            ClientName = string.IsNullOrWhiteSpace(clientName) ? null : clientName.Trim(),
        };
        db.Matters.Add(matter);
        await db.SaveChangesAsync(cancellationToken);

        return $"Created matter '{matter.Name}'{(matter.ClientName is null ? "" : $" for client {matter.ClientName}")} (id {matter.Id}).";
    }

    [Description("List the tenant's legal matters with their status and document counts.")]
    public async Task<string> ListMatters(CancellationToken cancellationToken = default)
    {
        // Walls are per-user identity, so filter in memory after the tenant-scoped fetch.
        var matters = (await db.Matters
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Name, m.ClientName, m.Status, m.RestrictedUserIdsJson, DocumentCount = m.Documents.Count })
                .Take(200)
                .ToListAsync(cancellationToken))
            .Where(m => Matter.WallAllows(m.RestrictedUserIdsJson, currentUser.UserId))
            .Take(50)
            .ToList();

        if (matters.Count == 0)
        {
            return "No matters yet. Create one with create_matter.";
        }

        var sb = new StringBuilder("Matters (newest first):\n");
        foreach (var m in matters)
        {
            sb.AppendLine($"- {m.Name}{(m.ClientName is null ? "" : $" (client: {m.ClientName})")} — {m.Status}, {m.DocumentCount} document(s)");
        }

        return sb.ToString();
    }

    [Description("Restrict a matter behind an ethical wall: afterwards ONLY you (and users you later add via the admin surface) can see or use the matter, its documents, and its knowledge collection. Side-effecting and requires approval.")]
    public async Task<string> RestrictMatterAccess(
        [Description("The matter name to restrict.")] string matterName,
        CancellationToken cancellationToken = default)
    {
        var matter = await FindMatterAsync(matterName, cancellationToken);
        if (matter is null)
        {
            return $"No matter named '{matterName}' exists. Use list_matters to find the right name.";
        }

        var userId = currentUser.UserId;
        if (userId is null)
        {
            return "Cannot restrict a matter without an authenticated user.";
        }

        matter.RestrictedUserIdsJson = JsonSerializer.Serialize(new[] { userId.Value });
        await db.SaveChangesAsync(cancellationToken);

        return $"Matter '{matter.Name}' is now behind an ethical wall: only you can see or use it. " +
               "Lift it with open_matter_access.";
    }

    [Description("Lift a matter's ethical wall so the whole tenant can see it again. Only someone inside the wall can lift it. Side-effecting and requires approval.")]
    public async Task<string> OpenMatterAccess(
        [Description("The matter name to open up.")] string matterName,
        CancellationToken cancellationToken = default)
    {
        // FindMatterAsync already applies the wall, so an outsider can't even name the matter.
        var matter = await FindMatterAsync(matterName, cancellationToken);
        if (matter is null)
        {
            return $"No matter named '{matterName}' exists. Use list_matters to find the right name.";
        }

        if (matter.RestrictedUserIdsJson is null)
        {
            return $"Matter '{matter.Name}' is not restricted.";
        }

        matter.RestrictedUserIdsJson = null;
        await db.SaveChangesAsync(cancellationToken);
        return $"Matter '{matter.Name}' is open to the whole tenant again.";
    }

    [Description("Index ALL documents on a matter into its searchable knowledge collection, so search_knowledge can answer questions across them with citations. Runs in the background; re-running refreshes the index. Side-effecting and requires approval.")]
    public async Task<string> IndexMatterDocuments(
        [Description("The matter name whose documents to index.")] string matterName,
        CancellationToken cancellationToken = default)
    {
        if (rag is null)
        {
            return "The knowledge pipeline is not enabled on this deployment (Rag:Enabled is false).";
        }

        var matter = await FindMatterAsync(matterName, cancellationToken);
        if (matter is null)
        {
            return $"No matter named '{matterName}' exists. Use list_matters to find the right name.";
        }

        var fileIds = await db.MatterDocuments
            .Where(d => d.MatterId == matter.Id)
            .OrderBy(d => d.FileName)
            .Select(d => d.FileId)
            .ToListAsync(cancellationToken);
        if (fileIds.Count == 0)
        {
            return $"Matter '{matter.Name}' has no documents to index. Attach documents first.";
        }

        var collectionId = await rag.GetOrCreateCollectionAsync(
            LegalModule.Id, MatterRagGate.MatterResourceType, matter.Id, $"matter: {matter.Name}", cancellationToken);
        var jobId = await jobs.EnqueueAsync(
            LegalModule.Id, RagIngestJob.Kind, new RagIngestArgs(collectionId, fileIds), cancellationToken);

        return $"Started indexing {fileIds.Count} document(s) on matter '{matter.Name}' into collection 'matter: {matter.Name}'. " +
               $"Job id: {jobId} (progress at /api/jobs/{jobId}). Once it completes, search_knowledge can answer questions across the matter with citations.";
    }

    [Description("Attach a stored file to a legal matter by matter name. Use the file id from the message's attachment reference or from list_documents.")]
    public async Task<string> AttachDocumentToMatter(
        [Description("The stored file id (a GUID) to attach.")] string fileId,
        [Description("The matter name to attach it to (must exist — create_matter first if not).")] string matterName,
        [Description("Optional note, e.g. 'signed original' or 'client draft'.")] string? note = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(fileId, out var id))
        {
            return $"'{fileId}' is not a valid file id. Use the id from the attachment reference or list_documents.";
        }

        // Tenant-scoped file lookup: a foreign tenant's id is indistinguishable from a missing one.
        var file = await files.FindAsync(id, cancellationToken);
        if (file is null)
        {
            return $"No stored file with id {id} exists. Use list_documents to see available files.";
        }

        var matter = await FindMatterAsync(matterName, cancellationToken);
        if (matter is null)
        {
            return $"No matter named '{matterName}' exists. Create it first with create_matter, or use list_matters to find the right name.";
        }

        var alreadyAttached = await db.MatterDocuments
            .AnyAsync(d => d.MatterId == matter.Id && d.FileId == file.Id, cancellationToken);
        if (alreadyAttached)
        {
            return $"'{file.FileName}' is already attached to matter '{matter.Name}'.";
        }

        db.MatterDocuments.Add(new MatterDocument
        {
            TenantId = tenant.RequireTenantId(),
            MatterId = matter.Id,
            FileId = file.Id,
            FileName = file.FileName,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        });
        await db.SaveChangesAsync(cancellationToken);

        return $"Attached '{file.FileName}' (file id: {file.Id}) to matter '{matter.Name}'.";
    }

    [Description("List the documents attached to a matter, with the file ids read_document consumes.")]
    public async Task<string> ListMatterDocuments(
        [Description("The matter name.")] string matterName,
        CancellationToken cancellationToken = default)
    {
        var matter = await FindMatterAsync(matterName, cancellationToken);
        if (matter is null)
        {
            return $"No matter named '{matterName}' exists. Use list_matters to see the tenant's matters.";
        }

        var documents = await db.MatterDocuments
            .Where(d => d.MatterId == matter.Id)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        if (documents.Count == 0)
        {
            return $"Matter '{matter.Name}' has no documents yet. Attach one with attach_document_to_matter.";
        }

        var sb = new StringBuilder($"Documents on matter '{matter.Name}':\n");
        foreach (var d in documents)
        {
            sb.AppendLine($"- {d.FileName} (file id: {d.FileId}){(d.Note is null ? "" : $" — {d.Note}")}");
        }

        return sb.ToString();
    }

    private async Task<Matter?> FindMatterAsync(string name, CancellationToken cancellationToken)
    {
        var normalized = name.Trim();
        var matter = await db.Matters.FirstOrDefaultAsync(
            m => EF.Functions.ILike(m.Name, normalized), cancellationToken);

        // The ethical wall: outside it, a walled matter is indistinguishable from a missing one —
        // the same no-existence-leak stance the platform takes for cross-tenant ids.
        return matter is not null && matter.IsAccessibleTo(currentUser.UserId) ? matter : null;
    }
}
