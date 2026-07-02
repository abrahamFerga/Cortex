import { useQuery } from "@tanstack/react-query";
import { apiGet, type ModuleTab, type TabColumn } from "../lib/api";

interface GenericTabProps {
  tab: ModuleTab;
  children?: React.ReactNode;
}

/** Renders a tab's `dataEndpoint` (a JSON array) as a generic table — no domain-specific UI needed. */
function DataTable({ endpoint, columns }: { endpoint: string; columns: TabColumn[] }) {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ["tab-data", endpoint],
    queryFn: () => apiGet<Record<string, unknown>[]>(endpoint),
  });

  if (isLoading) {
    return <p className="text-sm text-slate-500">Loading…</p>;
  }
  if (isError) {
    return <p className="text-sm text-red-600">{(error as Error).message}</p>;
  }

  const rows = data ?? [];
  // Fall back to the row's own fields (minus id) if the tab declared no columns.
  const cols: TabColumn[] =
    columns.length > 0
      ? columns
      : Object.keys(rows[0] ?? {})
          .filter((k) => k !== "id")
          .map((k) => ({ field: k, header: k }));

  return (
    <div className="overflow-hidden rounded-lg border border-slate-200 dark:border-slate-700">
      <table className="w-full text-left text-sm">
        <thead className="bg-slate-50 text-slate-500 dark:bg-slate-800 dark:text-slate-400">
          <tr>
            {cols.map((c) => (
              <th key={c.field} className="px-4 py-2 font-medium">
                {c.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
          {rows.length === 0 && (
            <tr>
              <td colSpan={cols.length} className="px-4 py-6 text-center text-slate-400">
                No data yet.
              </td>
            </tr>
          )}
          {rows.map((row, i) => (
            <tr key={i}>
              {cols.map((c) => (
                <td key={c.field} className="px-4 py-2 text-slate-700 dark:text-slate-200">
                  {row[c.field] == null ? "" : String(row[c.field])}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/**
 * Server-driven tab content. If the tab declares a `dataEndpoint`, its data renders as a table; otherwise
 * the consuming app may supply content as children, or a placeholder is shown. The base library has no
 * knowledge of any particular vertical.
 */
export function GenericTab({ tab, children }: GenericTabProps) {
  return (
    <section>
      <h1 className="mb-1 text-xl font-semibold text-slate-900 dark:text-slate-100">{tab.label}</h1>
      <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
        Route: <code className="font-mono">{tab.route}</code>
      </p>

      {children ??
        (tab.dataEndpoint ? (
          <DataTable endpoint={tab.dataEndpoint} columns={tab.columns ?? []} />
        ) : (
          <div className="rounded-lg border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">
            {tab.placeholder ?? "Nothing to show here yet."}
          </div>
        ))}
    </section>
  );
}
