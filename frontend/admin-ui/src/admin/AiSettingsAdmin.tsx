import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@cortex/ui";

/**
 * Per-tenant AI settings: override the assistant's base system prompt and the per-conversation token budget
 * for this tenant. A blank field falls back to the deployment default. The agent runner applies these each
 * turn (see `TenantAiSettingsResolver`). Requires platform.ai.manage.
 */
export function AiSettingsAdmin() {
  const settings = useQuery({ queryKey: ["admin", "ai-settings"], queryFn: api.admin.aiSettings });
  const queryClient = useQueryClient();
  const [prompt, setPrompt] = useState("");
  const [maxTokens, setMaxTokens] = useState("");
  const [monthlyTokens, setMonthlyTokens] = useState("");

  useEffect(() => {
    if (settings.data) {
      setPrompt(settings.data.systemPromptOverride ?? "");
      setMaxTokens(settings.data.maxConversationTokensOverride?.toString() ?? "");
      setMonthlyTokens(settings.data.maxMonthlyTokensOverride?.toString() ?? "");
    }
  }, [settings.data]);

  const save = useMutation({
    mutationFn: (next: { prompt: string; maxTokens: string; monthlyTokens: string }) =>
      api.admin.setAiSettings({
        systemPrompt: next.prompt.trim() || null,
        maxConversationTokens: next.maxTokens.trim() === "" ? null : Number(next.maxTokens),
        maxMonthlyTokens: next.monthlyTokens.trim() === "" ? null : Number(next.monthlyTokens),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["admin", "ai-settings"] }),
  });

  if (settings.isLoading) {
    return <p className="text-sm text-slate-500">Loading AI settings…</p>;
  }
  if (settings.isError) {
    return (
      <p className="text-sm text-red-600">
        Could not load AI settings — this view requires the platform.ai.manage permission.
      </p>
    );
  }

  const data = settings.data!;
  const tokensInvalid = maxTokens.trim() !== "" && (!/^\d+$/.test(maxTokens.trim()) || Number(maxTokens) < 0);
  const monthlyInvalid =
    monthlyTokens.trim() !== "" && (!/^\d+$/.test(monthlyTokens.trim()) || Number(monthlyTokens) < 0);

  return (
    <div className="max-w-2xl space-y-5">
      <header>
        <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">AI Settings</h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Customize the assistant for this tenant. Leave a field blank to use the deployment default. Changes
          take effect on the next chat turn and are recorded in the audit trail.
        </p>
      </header>

      <form
        className="space-y-5"
        onSubmit={(e) => {
          e.preventDefault();
          if (!tokensInvalid && !monthlyInvalid) save.mutate({ prompt, maxTokens, monthlyTokens });
        }}
      >
        <div className="space-y-1">
          <label htmlFor="system-prompt" className="block text-sm font-medium text-slate-700 dark:text-slate-200">
            System prompt
          </label>
          <textarea
            id="system-prompt"
            value={prompt}
            onChange={(e) => setPrompt(e.target.value)}
            rows={5}
            placeholder={`Default: ${data.defaultSystemPrompt}`}
            className="w-full rounded border border-slate-300 bg-white px-2 py-1 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-slate-600 dark:bg-slate-800"
          />
          <p className="text-xs text-slate-400">Blank uses the deployment default. Module instructions are still appended per conversation.</p>
        </div>

        <div className="space-y-1">
          <label htmlFor="max-tokens" className="block text-sm font-medium text-slate-700 dark:text-slate-200">
            Conversation token budget
          </label>
          <input
            id="max-tokens"
            value={maxTokens}
            onChange={(e) => setMaxTokens(e.target.value)}
            inputMode="numeric"
            placeholder={`Default: ${data.defaultMaxConversationTokens === 0 ? "unlimited" : data.defaultMaxConversationTokens}`}
            className="w-48 rounded border border-slate-300 bg-white px-2 py-1 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-slate-600 dark:bg-slate-800"
          />
          <p className="text-xs text-slate-400">
            Max tokens a single conversation may consume before further turns are refused. 0 = unlimited; blank = default.
          </p>
          {tokensInvalid && <p className="text-xs text-red-600">Enter a non-negative whole number.</p>}
        </div>

        <div className="space-y-1">
          <label htmlFor="monthly-tokens" className="block text-sm font-medium text-slate-700 dark:text-slate-200">
            Monthly token budget (organization-wide)
          </label>
          <input
            id="monthly-tokens"
            value={monthlyTokens}
            onChange={(e) => setMonthlyTokens(e.target.value)}
            inputMode="numeric"
            placeholder={`Default: ${data.defaultMaxMonthlyTokens === 0 ? "unlimited" : data.defaultMaxMonthlyTokens}`}
            className="w-48 rounded border border-slate-300 bg-white px-2 py-1 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-slate-600 dark:bg-slate-800"
          />
          <p className="text-xs text-slate-400">
            Total tokens the whole tenant may consume per calendar month (UTC). Chat is refused once reached;
            admins are notified at 80% and at exhaustion. 0 = unlimited; blank = default.
          </p>
          {monthlyInvalid && <p className="text-xs text-red-600">Enter a non-negative whole number.</p>}
        </div>

        {save.isError && <p className="text-xs text-red-600">{(save.error as Error).message}</p>}

        <div className="flex items-center gap-2">
          <button
            type="submit"
            disabled={tokensInvalid || monthlyInvalid || save.isPending}
            className="focus-ring rounded bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-500 disabled:opacity-40"
          >
            {save.isPending ? "Saving…" : "Save"}
          </button>
          <button
            type="button"
            disabled={save.isPending || (prompt === "" && maxTokens === "" && monthlyTokens === "")}
            onClick={() => {
              setPrompt("");
              setMaxTokens("");
              setMonthlyTokens("");
              save.mutate({ prompt: "", maxTokens: "", monthlyTokens: "" });
            }}
            className="focus-ring rounded border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-600 disabled:opacity-40 dark:border-slate-600 dark:text-slate-300"
          >
            Reset to defaults
          </button>
        </div>
      </form>
    </div>
  );
}
