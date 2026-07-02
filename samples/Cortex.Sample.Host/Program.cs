using Cortex.AspNetCore.Hosting;
using Cortex.AspNetCore.Modules;
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

var app = builder.Build();

await app.RunCortexPlatformAsync();

/// <summary>Exposed so integration tests can host this app via WebApplicationFactory&lt;Program&gt;.</summary>
public partial class Program;
