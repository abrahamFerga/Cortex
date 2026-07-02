import { useQuery } from "@tanstack/react-query";
import { api, type PermissionInfo } from "@cortex/ui";

function PermissionRow({ p }: { p: PermissionInfo }) {
  return (
    <tr>
      <td className="px-4 py-2 font-mono text-xs text-brand-700 dark:text-brand-300">
        {p.permission}
      </td>
      <td className="px-4 py-2 text-slate-700 dark:text-slate-300">{p.description}</td>
      <td className="px-4 py-2">
        <div className="flex flex-wrap gap-1">
          {p.requiresApproval && (
            <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs text-amber-800 dark:bg-amber-900/40 dark:text-amber-200">
              approval
            </span>
          )}
          {p.audited && (
            <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-xs text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200">
              audited
            </span>
          )}
        </div>
      </td>
    </tr>
  );
}

function PermTable({ rows }: { rows: PermissionInfo[] }) {
  return (
    <div className="overflow-hidden rounded-lg border border-slate-200 dark:border-slate-700">
      <table className="w-full text-left text-sm">
        <thead className="bg-slate-50 text-slate-500 dark:bg-slate-800 dark:text-slate-400">
          <tr>
            <th className="px-4 py-2 font-medium">Permission</th>
            <th className="px-4 py-2 font-medium">Description</th>
            <th className="px-4 py-2 font-medium">Flags</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
          {rows.map((p) => (
            <PermissionRow key={p.permission} p={p} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

/**
 * The security map: every permission the RBAC system can grant — platform
 * permissions plus, for each installed module, the tools the agent can call and
 * the permission each one requires. This is the inspectable "what can the agent
 * do, and who's allowed to invoke it" view (Cortex's analogue of OpenClaw's
 * explicit tool-permission map), alongside the role → permission baseline.
 */
export function SecurityCatalogView() {
  const catalog = useQuery({ queryKey: ["admin", "security"], queryFn: api.admin.securityCatalog });

  if (catalog.isLoading) {
    return <p className="text-sm text-slate-500">Loading security configuration…</p>;
  }
  if (catalog.isError) {
    return <p className="text-sm text-red-600">{(catalog.error as Error).message}</p>;
  }

  return (
    <div className="space-y-8">
      <header>
        <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Security</h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          The complete permission map. The agent never receives the schema of a tool the user lacks
          permission to call. Configure which roles grant these on the <strong>Roles</strong> tab.
        </p>
      </header>

      <section className="space-y-3">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-400">
          Platform permissions
        </h2>
        <PermTable rows={catalog.data!.platform} />
      </section>

      {catalog.data!.modules.map((m) => (
        <section key={m.id} className="space-y-3">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-400">
            {m.displayName} — agent tools
          </h2>
          {m.tools.length === 0 ? (
            <p className="text-sm text-slate-400">This module exposes no agent tools.</p>
          ) : (
            <PermTable rows={m.tools} />
          )}
        </section>
      ))}
    </div>
  );
}
