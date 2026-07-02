import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type PendingApproval } from "../lib/api";
import { useMe } from "../hooks/useMe";
import { hasPermission } from "../lib/permissions";

/** Render the recorded tool arguments compactly (best-effort JSON parse). */
function formatArgs(argumentsJson?: string): string {
  if (!argumentsJson) {
    return "";
  }
  try {
    const obj = JSON.parse(argumentsJson) as Record<string, unknown>;
    return Object.entries(obj)
      .map(([k, v]) => `${k}: ${String(v)}`)
      .join(", ");
  } catch {
    return argumentsJson;
  }
}

/**
 * The human-in-the-loop surface inside the chat: lists side-effecting tool calls the agent was blocked
 * from auto-running for this module, and lets the user Approve (which re-executes that exact call on the
 * server) or Reject. Renders nothing when there's nothing pending.
 */
export function PendingApprovals({ moduleId }: { moduleId: string }) {
  const qc = useQueryClient();
  const { data: me } = useMe();
  const canManage = hasPermission(me?.permissions ?? [], "chat.approvals.manage");
  const { data } = useQuery({
    queryKey: ["approvals"],
    queryFn: api.approvals.list,
    enabled: canManage, // only users who can approve fetch the list (the API enforces this too)
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: ["approvals"] });
  const approve = useMutation({ mutationFn: (id: string) => api.approvals.approve(id), onSuccess: invalidate });
  const reject = useMutation({ mutationFn: (id: string) => api.approvals.reject(id), onSuccess: invalidate });

  const pending = (data ?? []).filter((a: PendingApproval) => a.moduleId === moduleId);
  if (pending.length === 0) {
    return null;
  }

  const busy = approve.isPending || reject.isPending;

  return (
    <div className="mb-3 space-y-2">
      {pending.map((a) => {
        const args = formatArgs(a.argumentsJson);
        return (
        <div
          key={a.id}
          className="rounded-lg border border-amber-300 bg-amber-50 p-3 dark:border-amber-700/60 dark:bg-amber-900/20"
        >
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="text-sm font-medium text-amber-900 dark:text-amber-200">
                Approval required: <span className="font-mono">{a.toolName}</span>
              </p>
              {args && (
                <p className="mt-0.5 truncate text-xs text-amber-800/80 dark:text-amber-300/80">
                  {args}
                </p>
              )}
            </div>
            <div className="flex shrink-0 gap-2">
              <button
                type="button"
                disabled={busy}
                onClick={() => approve.mutate(a.id)}
                className="rounded-md bg-emerald-600 px-3 py-1 text-xs font-medium text-white hover:bg-emerald-500 disabled:opacity-50"
              >
                Approve
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => reject.mutate(a.id)}
                className="rounded-md border border-slate-300 px-3 py-1 text-xs font-medium text-slate-600 hover:bg-slate-100 disabled:opacity-50 dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                Reject
              </button>
            </div>
          </div>
        </div>
        );
      })}
    </div>
  );
}
