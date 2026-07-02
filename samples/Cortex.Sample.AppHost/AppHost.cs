using Aspire.Hosting.ApplicationModel;

// Cortex sample-app orchestration — the one-command way to run the FULL stack locally:
//   • Postgres (platform + audit databases) and Redis (SignalR backplane / cache)
//   • the sample API (Cortex.Sample.Host with the Finance + Nutrition + Legal modules)
//   • both front-ends as Vite dev servers: the end-user workspace (@cortex/ui) and the
//     admin console (@cortex/admin-ui)
//
// The chat assistant runs on the dependency-free "Mock" provider, so everything works with
// zero configuration. Supply a real provider + key without editing this file:
//   dotnet user-secrets --project samples/Cortex.Sample.AppHost set "Parameters:ai-provider" "OpenAI"
//   dotnet user-secrets --project samples/Cortex.Sample.AppHost set "Parameters:ai-api-key"  "sk-..."
//
// Prerequisites: a container runtime (Docker/Podman) for the DB + Redis, and the front-end deps
// installed once — `corepack enable && pnpm --dir frontend install`.
//
// Run with: dotnet run --project samples/Cortex.Sample.AppHost   (or `aspire run`)

var builder = DistributedApplication.CreateBuilder(args);

// ── Backing services (run as containers locally) ─────────────────────────────
var postgres = builder.AddPostgres("cortex-pg")
    .WithDataVolume()
    .WithPgAdmin();

var platformDb = postgres.AddDatabase("cortex-platform");
var auditDb = postgres.AddDatabase("cortex-audit");

var redis = builder.AddRedis("cortex-redis");

// ── Parameters — everything Cortex needs to run, overridable per environment ──
// Defaults keep the stack zero-config (Mock chat provider); override any of these via
// `Parameters:<name>` in user-secrets/env. The API key is a secret (never published/committed).
var aiProvider = builder.AddParameter("ai-provider", "Mock", publishValueAsDefault: true);
var aiModel = builder.AddParameter("ai-model", "gpt-4o-mini", publishValueAsDefault: true);
var aiEndpoint = builder.AddParameter("ai-endpoint", "", publishValueAsDefault: true);
var aiApiKey = builder.AddParameter("ai-api-key", "", secret: true);

// ── API ──────────────────────────────────────────────────────────────────────
var api = builder.AddProject<Projects.Cortex_Sample_Host>("cortex-sample")
    .WithReference(platformDb)
    .WithReference(auditDb)
    .WithReference(redis)
    .WaitFor(platformDb)
    .WaitFor(auditDb)
    .WithEnvironment("Ai__Provider", aiProvider)
    .WithEnvironment("Ai__Model", aiModel)
    .WithEnvironment("Ai__Endpoint", aiEndpoint)
    .WithEnvironment("Ai__ApiKey", aiApiKey)
    .WithExternalHttpEndpoints();

// ── Front-ends (Vite dev servers; pnpm workspace deps installed once, so no per-app install) ──
// A missing pnpm or a skipped `pnpm install` otherwise surfaces as an opaque resource failure
// deep in the dashboard, so check the two prerequisites up front and say exactly how to fix them.
if (builder.ExecutionContext.IsRunMode)
{
    if (!ToolExistsOnPath("pnpm"))
    {
        throw new DistributedApplicationException(
            "pnpm was not found on PATH, so the cortex-ui / cortex-admin-ui resources cannot start. " +
            "Run `corepack enable` (needs admin rights on Windows — or use `npm install -g pnpm`), " +
            "then `pnpm --dir frontend install`, and start the AppHost again.");
    }

    if (!Directory.Exists(Path.Combine(builder.AppHostDirectory, "../../frontend/node_modules")))
    {
        throw new DistributedApplicationException(
            "Front-end dependencies are not installed (frontend/node_modules is missing). " +
            "Run `pnpm --dir frontend install` once, then start the AppHost again.");
    }
}

var workspace = builder.AddViteApp("cortex-ui", "../../frontend/cortex-ui")
    .WithPnpm(install: false)
    .WaitFor(api)
    .WithEnvironment("VITE_API_BASE", api.GetEndpoint("http"))
    .WithExternalHttpEndpoints();

var admin = builder.AddViteApp("cortex-admin-ui", "../../frontend/admin-ui")
    .WithPnpm(install: false)
    .WaitFor(api)
    .WithEnvironment("VITE_API_BASE", api.GetEndpoint("http"))
    .WithEnvironment("VITE_WORKSPACE_URL", workspace.GetEndpoint("http"))
    .WithExternalHttpEndpoints();

// The workspace's "Admin" link targets the admin console (Vite serves it under its /admin base).
workspace.WithEnvironment(
    "VITE_ADMIN_URL",
    ReferenceExpression.Create($"{admin.GetEndpoint("http")}/admin"));

// Teach the API's CORS policy the front-end origins. Aspire assigns the UI ports dynamically, so
// reference their endpoints; the fixed localhost ports cover running `pnpm dev` outside Aspire.
api.WithEnvironment("Cors__Origins__0", workspace.GetEndpoint("http"))
   .WithEnvironment("Cors__Origins__1", admin.GetEndpoint("http"))
   .WithEnvironment("Cors__Origins__2", "http://localhost:5173")
   .WithEnvironment("Cors__Origins__3", "http://localhost:5174");

builder.Build().Run();

// True when `tool` resolves on PATH (Windows launchers included — pnpm installs as pnpm.cmd).
static bool ToolExistsOnPath(string tool)
{
    var extensions = OperatingSystem.IsWindows() ? new[] { ".cmd", ".exe", ".bat", "" } : new[] { "" };
    return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .SelectMany(_ => extensions, (dir, ext) => Path.Combine(dir.Trim('"'), tool + ext))
        .Any(File.Exists);
}
