using Cortex.Application.Authorization;
using Cortex.Application.Commerce;
using Cortex.Sample.Host;
using Cortex.AspNetCore.Connectors;
using Cortex.AspNetCore.Hosting;
using Cortex.AspNetCore.Modules;
using Cortex.Connectors.AzureBlob;
using Cortex.Connectors.LocalFolder;
using Cortex.Connectors.GoogleDrive;
using Cortex.Connectors.MsGraph;
using Cortex.Connectors.Peer;
using Cortex.Connectors.S3;
using Cortex.Modules.Finance;
using Cortex.Modules.Legal;
using Cortex.Modules.Nutrition;

// ─────────────────────────────────────────────────────────────────────────────
// Sample application built on the Cortex base platform.
//
// This is what a real product's host looks like: one AddCortexPlatform() call brings
// the whole platform — RBAC, auditing, token usage, the admin dashboard API, AG-UI
// chat — then you install the domain modules you want. Nothing else is required.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.AddCortexPlatform();

// Install the domain modules this product ships with.
builder.AddCortexModule<FinanceModule>();
builder.AddCortexModule<NutritionModule>();
builder.AddCortexModule<LegalModule>();

// Install the data-source connectors this product ships with. Installation only makes them
// available — a tenant admin enables and configures each one under Integrations (default-off).
builder.AddCortexConnector<LocalFolderConnector>();
builder.AddCortexConnector<AzureBlobConnector>();
builder.AddCortexConnector<CortexPeerConnector>(); // verticals are separate systems; this is how they talk
builder.AddCortexConnector<MsGraphConnector>();
builder.AddCortexConnector<GoogleDriveConnector>();
builder.AddCortexConnector<S3Connector>();

// What this host SELLS (docs/COMMERCIALIZATION.md): the plan — not checkout metadata — decides
// what a purchase grants. The sample sells the Legal vertical in the three standard tiers.
// A product role: paralegals chat and work the docket/tasks but never file, bill, or
// close. Seeded into every tenant's editable baseline alongside the built-ins.
builder.Services.AddCortexRole("paralegal",
[
    "chat.use", "chat.conversations.view", "files.upload", "files.read",
    "tools.documents.read_document", "tools.documents.list_documents",
    "tools.legal.list_matters", "tools.legal.list_deadlines", "tools.legal.add_deadline",
    "tools.legal.complete_deadline", "tools.legal.list_tasks", "tools.legal.add_task",
    "tools.legal.complete_task", "legal.matters.view",
]);

// Both wave-1 host seams together: after any tenant is provisioned (operator call or a
// billing webhook), email the new admin their sign-in details. Best-effort by design.
builder.Services.AddCortexTenantProvisionedHook<WelcomeEmailHook>();

builder.Services.AddCortexProduct(new ProductOffering
{
    ProductId = "the-lawyer",
    Plans =
    [
        new ProductPlan { Id = "solo", Modules = ["legal"], DefaultSeats = 1, MonthlyTokenBudget = 200_000 },
        new ProductPlan { Id = "team", Modules = ["legal"], DefaultSeats = 5, MonthlyTokenBudget = 500_000 },
        new ProductPlan { Id = "dedicated", Dedicated = true },
    ],
});

var app = builder.Build();

await app.RunCortexPlatformAsync();

/// <summary>Exposed so integration tests can host this app via WebApplicationFactory&lt;Program&gt;.</summary>
public partial class Program;
