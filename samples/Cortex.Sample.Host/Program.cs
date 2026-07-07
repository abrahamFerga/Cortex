using Cortex.Application.Commerce;
using Cortex.AspNetCore.Connectors;
using Cortex.AspNetCore.Hosting;
using Cortex.AspNetCore.Modules;
using Cortex.Connectors.AzureBlob;
using Cortex.Connectors.LocalFolder;
using Cortex.Connectors.MsGraph;
using Cortex.Connectors.Peer;
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

// What this host SELLS (docs/COMMERCIALIZATION.md): the plan — not checkout metadata — decides
// what a purchase grants. The sample sells the Legal vertical in the three standard tiers.
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
