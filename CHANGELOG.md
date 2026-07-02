# Changelog

All notable changes to Cortex are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); Cortex will adopt
[Semantic Versioning](https://semver.org/spec/v2.0.0.html) once it leaves alpha.

Releases are cut from a tagged GitHub Release (`v*`), which triggers the publish workflow
(`.github/workflows/publish.yml`) to push the `Cortex.*` NuGet packages and the `@cortex/ui`
npm package. Until then, everything lives under **Unreleased**.

## [Unreleased] — toward 0.1.0-alpha

The first alpha: the base platform, an admin/security dashboard, and three sample verticals,
all runnable with no AI key via a built-in Mock provider. See [README.md](README.md) and
[GETTING_STARTED.md](GETTING_STARTED.md).

### Added

**Platform (backend NuGet packages)**
- **Module SDK** — a vertical implements `IModule` and declares a `ModuleManifest` (tools, tabs,
  roles, agent instructions); the host discovers and installs it with `AddCortexModule<T>()`.
  See [BUILDING_A_MODULE.md](BUILDING_A_MODULE.md).
- **Chat-first agent pipeline** on Microsoft Agent Framework over `Microsoft.Extensions.AI`, streamed
  over SignalR (Redis backplane) and the open **AG-UI** protocol.
- **Tool security before the model call** — the agent runner filters tools by the caller's permissions
  before building the request, so the LLM never sees a tool the user may not call.
- **Human-in-the-loop approvals** — side-effecting tools are blocked pending explicit approval.
- **Layered RBAC** (system roles → dotted permissions with wildcards → per-resource ACLs), an
  append-only **audit log**, and per-turn **token-usage** tracking.
- **Multi-tenant by default** — row-level isolation via EF Core global query filters on `TenantId`.
- **Provider-swappable AI** (OpenAI / Azure OpenAI / Ollama) plus a dependency-free **Mock** provider so
  chat — and real, audited tool calls plus the approval gate — work with zero configuration.
- **Admin/security dashboard API** — the full permission map, users & roles, token usage, and audit log.

**Frontend (`@cortex/ui`)**
- React 18 + Vite library: the chat shell, module switcher, server-driven data tabs, and the admin
  dashboard. Ships ESM + UMD bundles with bundled TypeScript declarations.

**Samples**
- Three demo verticals — **Finance** (rule-based categorizer + LLM fallback, budgets, seeded demo ledger),
  **Nutrition**, **Legal** — plus a minimal **Tasks** template that backs the build-a-module tutorial.

**Tooling & ops**
- **.NET Aspire** AppHost (Postgres + Redis + API + a live telemetry dashboard) and `docker compose`
  for the quickstart.
- **Terraform** (Azure Container Apps, Postgres, Redis, Key Vault, Entra External ID) and **GitHub
  Actions** (CI, deploy, publish to GitHub Packages) + Trivy scanning + Dependabot.
- **Tests** — 112 .NET (unit + Testcontainers integration) and vitest unit tests on the frontend.
- **Docs** — README, GETTING_STARTED, BUILDING_A_MODULE, ARCHITECTURE, CONTRIBUTING, SECURITY.
