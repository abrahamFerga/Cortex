<!-- Research pass 2026-07: how enterprise AI products model data-source connectors.
     Feeds docs/PLATFORM_CONNECTORS_RAG_PLAN.md. -->

# Data-Source Connectors in Enterprise AI Products

## 1a. Harvey (legal AI)

- **Connector model — hybrid, two mechanisms:**
  - **On-demand pull (primary):** iManage/SharePoint/NetDocuments docs are *not* auto-synced; "Harvey only processes documents that users explicitly select and upload" via an embedded picker into Assistant/Workflows/Vault. Direct OAuth, no middleware.
  - **One-way scheduled sync (Vault):** a Vault project can be bound to **at most one folder or matter** from SharePoint/iManage/Box/Google Drive; syncs **once daily** + manual refresh; synced files are **read-only** in the vault; 100k-doc cap per vault.
  - **Connector Library (June 2026, Early Access):** two tiers — **native API integrations** (Gmail, Google Drive, Outlook, SharePoint) and **MCP connectors** (iManage, NetDocuments, Box, PitchBook, Intralinks, Datasite) for faster catalog scaling.
- **ACLs:** per-user OAuth to the DMS, so iManage permissions + ethical walls apply at fetch time; admins can additionally block Harvey use on specific client-matter tags. Connector Library adds "granular admin configuration for which tool actions are permitted per workspace."
- **Admin enable/disable:** two-stage — admin toggles the integration in **Settings > Integrations**, then registers/configures the OAuth app in iManage Control Center (Cloud: prebuilt "Harvey AI – File Syncing" app; On-Prem: upload a Harvey-supplied package, mind refresh-token ≥1yr). Each user then authenticates individually on first use. **Disabling revokes access tokens**; re-enable forces re-auth. Upload activity is auditable in Workspace History.
- Sources: [iManage enablement](https://help.harvey.ai/articles/dms-integrations-imanage), [Folder uploads & one-way sync](https://help.harvey.ai/release-notes/folder-uploads), [Connector Library](https://www.harvey.ai/blog/connector-library), [iManage integration blog](https://www.harvey.ai/blog/harveys-imanage-integration), [Integrations index](https://help.harvey.ai/topics/integrations)

## 1b. Legora

- **Connector model:** query-at-runtime via direct iManage APIs and NetDocuments' **ndConnect** partner program. ndConnect flow is on-demand + round-trip: user selects files (or uses natural-language prompts), NetDocuments transfers selected content to Legora, Legora runs the prompt, and **AI output is saved back into NetDocuments** at the right location — no continuous sync.
- **ACLs:** ndConnect is "identity-aware": every connection respects user authentication and access rights, document-level security, and audit trails. Public docs are thin on iManage specifics (partnership announcements, not technical docs).
- **Admin enable/disable:** not publicly documented; enablement appears to be tenant-level configuration via the DMS partner program.
- Sources: [Legora–iManage partnership](https://legora.com/newsroom/legora-and-imanage-expand-partnership-to-power-a-faster-more-connected-workflow-for-legal-teams), [ndConnect announcement](https://www.netdocuments.com/company-news/netdocuments-partners-with-legora-harvey-ndconnect/), [Legora–NetDocuments](https://legora.com/newsroom/legora-netdocuments-secure-seamless-ai-powered-legal-work)

## 1c. CoCounsel (Thomson Reuters)

- **Connector model — two distinct surfaces:**
  - **DMS connectors (fetch-on-demand):** ~10 connectors (Box, Dropbox, Google Drive, HighQ, iManage On-Prem/Cloud, Litify, NetDocuments, SharePoint, Smokeball) exposed as a "Files from external DMS" upload source. Brokered through a third party, **Syncly**.
  - **Knowledge Search (indexed/synced):** multi-repository AI search across HighQ, iManage, NetDocuments, SharePoint, OneDrive + Westlaw/Practical Law; Syncly "synchronises data across repositories"; data stays in the customer's domain; license entitlements enforced (e.g., no Westlaw content without a Westlaw subscription).
- **ACLs:** each end user must individually authorize each connector before fetching, so DMS-side permissions apply per user.
- **Admin enable/disable:** admin (CoCounsel Application Admin role) goes to **Admin settings > Integrations > DMS integrations via Syncly > New Integration**, supplies instance details, authorizes, and the connector becomes visible **to all users** in their DMS-connection list (active state on creation).
- Sources: [Connect DMS connectors](https://www.thomsonreuters.com/en-us/help/cocounsel/legal/integrations/dms-integration/connect-dms-connectors-to-cocounsel), [NetDocuments connector](https://www.thomsonreuters.com/en-us/help/cocounsel/integrations/dms-integration/netdocuments-connector), [SharePoint connector](https://www.thomsonreuters.com/en-us/help/cocounsel/integrations/dms-integration/sharepoint-connector.html), [Knowledge Search](https://legaltechnology.com/2025/07/09/thomson-reuters-launches-cocounsel-knowledge-search-to-surface-multi-repository-information-in-the-workflow/)

## 2. Microsoft 365 Copilot connectors (Graph connectors)

- **Connector model — explicit dual taxonomy (best-documented reference model):**
  - **Synced connectors:** crawl + index external content into the Microsoft Graph index; org-level; continuous sync with admin-configurable frequency and on-demand full crawls; 100+ prebuilt connectors in an admin-center gallery; on-prem sources via a **Graph connector agent**.
  - **Federated connectors:** MCP-based live fetch, **no indexing**; user-level auth (admin enables, users authenticate); read-only; positioned for "sensitive, dynamic, or live" sources; custom federated connectors not supported (yet).
- **ACLs:** every ingested `externalItem` carries content + metadata + **an ACL**; search/Copilot do permission-based filtering so users only see items they can access in the source; non-Entra identities are handled via external group mapping. Federated path defers to source auth (OAuth 2.0) at query time.
- **Admin enable/disable:** requires the **AI administrator** role in the Microsoft 365 admin center; custom synced connectors require an Entra app registration + admin consent to Graph permissions; prebuilt connectors are point-and-configure from the gallery; default federated connectors show as "Ready" in the connections list. Admins also control which connectors each Copilot agent may use.
- Sources: [Connectors overview](https://learn.microsoft.com/en-us/microsoft-365/copilot/connectors/overview), [Extensibility overview](https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/overview-copilot-connector), [Prerequisites](https://learn.microsoft.com/en-us/microsoft-365/copilot/connectors/prerequisites), [Connector agent](https://learn.microsoft.com/en-us/microsoft-365/copilot/connectors/connector-agent), [Connectors gallery](https://learn.microsoft.com/en-us/microsoftsearch/connectors-gallery)

## 3. Glean (enterprise search)

- **Connector model:** pull-sync by default (native connectors call source APIs), **push** (Indexing API) for firewalled/self-hosted/custom sources. Three crawl types per connector: **content crawl** (docs, comments, attachments), **identity crawl** (users/groups/roles, cross-system identity resolution), **activity crawl** (views/edits/shares for ranking). Initial full sync, then steady state via **webhooks + incremental crawls** (minutes-scale watermark crawls, e.g. Salesforce `SystemModstamp`) so "the index approaches real time without every query hitting the source."
- **ACLs ("permission trimming"):** connectors sync the **permissions map** (ACLs, groups, roles) alongside content; enforcement at **query time** — results filtered to what the current user can open in the source; supports per-document, per-field, even per-sentence ACL logic.
- **Admin enable/disable:** admin console **Platform > Connectors** hub: connector catalog, per-datasource setup, **Sync Progress** (initial + incremental + troubleshooting metrics), **Settings & Visibility** (staged rollout — control which users see indexed content before broad enablement), **Health & Alerts**. Agent-era addition: per-connector **tool** enable/disable and "always allow" vs "needs approval" execution policies.
- Sources: [About connectors](https://docs.glean.com/connectors/about), [How connectors power Glean](https://docs.glean.com/connectors/connectors-power-glean), [Managing connectors](https://docs.glean.com/connectors/monitoring), [Crawling FAQ](https://docs.glean.com/connectors/crawling-faq), [Salesforce connector](https://docs.glean.com/connectors/native/salesforce/about)

## 4. OpenClaw (open-source agent gateway) — install-wizard UX

- **Wizard (`openclaw onboard`):** shows a **timeline of steps upfront**; optional steps are skippable and revisitable. Local-mode sequence: (1) detect existing config `~/.openclaw/openclaw.json` — offer keep/review/reset; (2) model/auth (API key or OAuth per provider); (3) workspace (`~/.openclaw/workspace`, seeded with bootstrap files); (4) gateway (port 18789, bind, token/password auth); (5) **channels** — multi-select of WhatsApp (QR login), Telegram/Discord/Mattermost (bot token), Google Chat (service-account JSON), Signal, iMessage, etc.; (6) web-search provider or skip; (7) daemon install (LaunchAgent / systemd / Scheduled Task); (8) health check against the gateway; (9) **skills** — installs recommended skills + optional deps, picks node manager (npm/pnpm/bun); (10) finish/launch.
- **Two modes:** **QuickStart** (sensible defaults) vs **Advanced** (exposes every step). Fully **non-interactive** variant for scripting: `--non-interactive` + flags (`--auth-choice`, `--gateway-port`, `--install-daemon`, `--skip-skills`, `--skip-search`, `--json`, secret-by-reference `--gateway-token-ref-env`).
- **Plugin channels:** some channels ship as plugins — when selected in the wizard, it **prompts to install the plugin (npm or local path) first**, then runs that channel's config prompts. Catalog = built-ins + installable plugins.
- **Enable/disable afterward:** everything lands in one JSON config (`~/.openclaw/openclaw.json`: `channels.*`, per-channel allowlists, `skills.install.nodeManager`, `gateway.*`, plus `wizard.lastRunAt/lastRunVersion` provenance stamps). Post-install changes via `openclaw configure` (section-scoped, e.g. `--section web`) or direct config edits; re-running onboarding is **non-destructive by default** — nothing is wiped unless `--reset` is passed (`--reset-scope full` to include the workspace). Credentials stored outside the main config (`~/.openclaw/credentials/...`).
- Sources: [Onboarding (CLI)](https://docs.openclaw.ai/start/wizard), [Onboarding reference](https://docs.openclaw.ai/reference/wizard), [CLI setup reference](https://docs.openclaw.ai/start/wizard-cli-reference), [onboard command](https://docs.openclaw.ai/cli/onboard)

## Cross-cutting patterns (relevant to a connector design)

1. **Two-lane taxonomy is converging industry-wide:** indexed/synced (org-level, admin-owned, ACL ingested with content) vs. federated/on-demand (user-level OAuth, source enforces ACLs at query time, often MCP). Microsoft formalizes it; Harvey (native API vs MCP tiers) and CoCounsel (DMS fetch vs Knowledge Search) mirror it.
2. **ACL honoring has exactly two implementations:** sync the permissions map and trim at query time (Glean, MS synced), or make every fetch ride the end user's own token (Harvey, CoCounsel, Legora, MS federated). Per-user auth on first use is the standard UX for the latter.
3. **Admin enablement is consistently two-stage:** tenant admin registers/enables the connector (app registration in the source + toggle in an Integrations/Connectors admin page), then each user authenticates individually. Disable = token revocation (Harvey states this explicitly).
4. **Scoped binding:** Harvey binds one synced folder/matter per Vault project — a per-project connector binding rather than global indexing; a useful pattern for matter-centric or tenant-scoped products.
5. **Wizard UX (OpenClaw):** timeline-first, skippable optional steps, quickstart/advanced split, plugin-install-on-select for catalog items, single declarative config file as the source of truth, idempotent re-runs with explicit `--reset`, and a non-interactive flag surface mirroring every prompt — a clean template for a CLI installer that asks "which connectors/channels do you want?"
