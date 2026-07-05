import { useQuery } from "@tanstack/react-query";
import { api } from "@cortex/ui";

function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900">
      <h3 className="mb-3 text-sm font-semibold text-slate-900 dark:text-slate-100">{title}</h3>
      {children}
    </section>
  );
}

function Stat({ label, value, alarm = false }: { label: string; value: string; alarm?: boolean }) {
  return (
    <div>
      <dt className="text-xs text-slate-500 dark:text-slate-400">{label}</dt>
      <dd
        className={`text-lg font-semibold ${
          alarm ? "text-red-600 dark:text-red-400" : "text-slate-900 dark:text-slate-100"
        }`}
      >
        {value}
      </dd>
    </div>
  );
}

function ago(iso?: string): string {
  if (!iso) return "never";
  const seconds = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
  return `${Math.floor(seconds / 86400)}d ago`;
}

/**
 * Operational health at a glance — the server aggregates everything into one tenant-scoped call
 * (GET /api/admin/ops): job queue, connector sync recency, knowledge-index freshness, delivery
 * config, and budget posture. Requires platform.audit.view.
 */
export function OpsView() {
  const ops = useQuery({
    queryKey: ["admin", "ops"],
    queryFn: api.admin.ops,
    refetchInterval: 15_000,
  });

  if (ops.isPending) {
    return <p className="text-sm text-slate-500 dark:text-slate-400">Loading operational snapshot…</p>;
  }

  if (ops.isError || !ops.data) {
    return <p className="text-sm text-red-600 dark:text-red-400">Could not load the ops snapshot.</p>;
  }

  const d = ops.data;
  const budgetUsedPct =
    d.ai.maxMonthlyTokens > 0 ? Math.min(100, Math.round((d.ai.monthTokens / d.ai.maxMonthlyTokens) * 100)) : null;

  return (
    <div className="max-w-4xl">
      <h2 className="mb-1 text-lg font-semibold text-slate-900 dark:text-slate-100">Operations</h2>
      <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
        Live tenant health — refreshes every 15 seconds.
      </p>

      <div className="grid gap-4 sm:grid-cols-2">
        <Card title="Background jobs">
          <dl className="grid grid-cols-3 gap-3">
            <Stat label="Queued" value={String(d.jobs.queued)} />
            <Stat label="Running" value={String(d.jobs.running)} />
            <Stat label="Failed (24h)" value={String(d.jobs.failed24h)} alarm={d.jobs.failed24h > 0} />
          </dl>
          {d.jobs.oldestQueuedAgeSeconds != null && d.jobs.oldestQueuedAgeSeconds > 300 && (
            <p className="mt-2 text-xs text-amber-600 dark:text-amber-400">
              Oldest queued job has waited {Math.floor(d.jobs.oldestQueuedAgeSeconds / 60)} minutes — the
              processor may be behind.
            </p>
          )}
        </Card>

        <Card title="Knowledge index">
          <dl className="grid grid-cols-3 gap-3">
            <Stat label="Collections" value={String(d.rag.collections)} />
            <Stat label="Chunks" value={String(d.rag.chunks)} />
            <Stat label="Last ingest" value={ago(d.rag.lastIngestAt)} />
          </dl>
        </Card>

        <Card title="Connectors">
          {d.connectors.length === 0 ? (
            <p className="text-sm text-slate-500 dark:text-slate-400">No connectors enabled.</p>
          ) : (
            <ul className="space-y-1.5 text-sm">
              {d.connectors.map((c) => (
                <li key={c.connectorId} className="flex items-center justify-between">
                  <span className="font-medium text-slate-900 dark:text-slate-100">{c.connectorId}</span>
                  <span className="text-xs text-slate-500 dark:text-slate-400">
                    {c.bindingCount} binding{c.bindingCount === 1 ? "" : "s"} · synced {ago(c.lastSyncedAt)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card title="AI & budget">
          <dl className="grid grid-cols-2 gap-3">
            <Stat label="Provider" value={`${d.ai.provider} / ${d.ai.model}`} />
            <Stat
              label="Month tokens"
              value={
                budgetUsedPct == null
                  ? d.ai.monthTokens.toLocaleString()
                  : `${d.ai.monthTokens.toLocaleString()} (${budgetUsedPct}%)`
              }
              alarm={budgetUsedPct != null && budgetUsedPct >= 80}
            />
          </dl>
          <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">
            {d.ai.maxMonthlyTokens > 0
              ? `Monthly budget: ${d.ai.maxMonthlyTokens.toLocaleString()} tokens.`
              : "No monthly budget set (unlimited)."}
            {" "}
            Webhook delivery: {d.notifications.webhookConfigured ? "configured" : "not configured"}.
          </p>
        </Card>
      </div>
    </div>
  );
}
