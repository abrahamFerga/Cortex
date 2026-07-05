import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ConfirmDialog, type AgentProfile } from "@cortex/ui";

const inputClass =
  "w-full rounded border border-slate-300 bg-white px-2 py-1 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-slate-600 dark:bg-slate-800";

interface EditorState {
  moduleId: string;
  name: string;
  instructions: string;
  mode: "Append" | "Replace";
  isDefault: boolean;
}

const emptyEditor = (moduleId: string): EditorState => ({
  moduleId,
  name: "",
  instructions: "",
  mode: "Append",
  isDefault: true,
});

/**
 * Agent profiles: named, per-module chatbot configurations. The DEFAULT profile for a module is
 * what the agent runner applies on every chat turn — Append layers the instructions after the
 * module's built-in ones; Replace swaps them out entirely (the platform system prompt always
 * stays). Requires platform.ai.manage.
 */
export function AgentProfilesAdmin() {
  const qc = useQueryClient();
  const modules = useQuery({ queryKey: ["modules"], queryFn: api.modules });
  const profiles = useQuery({ queryKey: ["admin", "agent-profiles"], queryFn: () => api.admin.agentProfiles() });
  const [editor, setEditor] = useState<EditorState | null>(null);
  const [deleting, setDeleting] = useState<AgentProfile | null>(null);

  const invalidate = () => qc.invalidateQueries({ queryKey: ["admin", "agent-profiles"] });
  const save = useMutation({
    mutationFn: (p: EditorState) => api.admin.upsertAgentProfile(p),
    onSuccess: () => {
      setEditor(null);
      invalidate();
    },
  });
  const remove = useMutation({
    mutationFn: (id: string) => api.admin.deleteAgentProfile(id),
    onSuccess: invalidate,
  });

  if (profiles.isLoading || modules.isLoading) {
    return <p className="text-sm text-slate-500">Loading agent profiles…</p>;
  }
  if (profiles.isError) {
    return (
      <p className="text-sm text-red-600">
        Could not load agent profiles — this view requires the platform.ai.manage permission.
      </p>
    );
  }

  const moduleIds = (modules.data ?? []).map((m) => m.id);
  const rows = profiles.data ?? [];

  return (
    <div className="max-w-3xl space-y-4">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Agent Profiles</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Give each module's chatbot its own voice and policy without a code change. The{" "}
            <span className="font-medium">default</span> profile is applied on every chat turn; Append layers
            onto the module's built-in instructions, Replace swaps them out.
          </p>
        </div>
        <button
          type="button"
          onClick={() => setEditor(emptyEditor(moduleIds[0] ?? ""))}
          className="focus-ring shrink-0 rounded bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-500"
        >
          New profile
        </button>
      </header>

      {rows.length === 0 && !editor && (
        <p className="rounded-lg border border-dashed border-slate-300 p-8 text-center text-sm text-slate-400 dark:border-slate-700">
          No profiles yet — every module uses its built-in instructions. Create one to retask or specialize a
          chatbot for this tenant.
        </p>
      )}

      <div className="space-y-2">
        {rows.map((p) => (
          <div
            key={p.id}
            className="rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900"
          >
            <div className="flex items-center justify-between gap-3">
              <p className="flex flex-wrap items-center gap-2 font-medium text-slate-900 dark:text-slate-100">
                {p.name}
                <span className="font-mono text-xs text-slate-400">{p.moduleId}</span>
                <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                  {p.mode}
                </span>
                {p.isDefault && (
                  <span className="rounded-full bg-brand-100 px-2 py-0.5 text-xs font-medium text-brand-700 dark:bg-brand-900/40 dark:text-brand-200">
                    default
                  </span>
                )}
              </p>
              <div className="flex shrink-0 gap-2">
                <button
                  type="button"
                  onClick={() => setEditor({ ...p })}
                  className="focus-ring rounded border border-slate-300 px-2 py-1 text-xs font-medium text-slate-600 dark:border-slate-600 dark:text-slate-300"
                >
                  Edit
                </button>
                <button
                  type="button"
                  onClick={() => setDeleting(p)}
                  className="focus-ring rounded border border-red-300 px-2 py-1 text-xs font-medium text-red-600 hover:bg-red-50 dark:border-red-800 dark:hover:bg-red-900/20"
                >
                  Delete
                </button>
              </div>
            </div>
            <p className="mt-1 line-clamp-2 text-xs text-slate-500 dark:text-slate-400">{p.instructions}</p>
          </div>
        ))}
      </div>

      {editor && (
        <form
          className="space-y-4 rounded-lg border border-brand-200 bg-white p-4 dark:border-brand-900/60 dark:bg-slate-900"
          onSubmit={(e) => {
            e.preventDefault();
            save.mutate(editor);
          }}
        >
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1">
              <label htmlFor="profile-module" className="block text-sm font-medium text-slate-700 dark:text-slate-200">
                Module
              </label>
              <select
                id="profile-module"
                value={editor.moduleId}
                onChange={(e) => setEditor({ ...editor, moduleId: e.target.value })}
                className={inputClass}
              >
                {moduleIds.map((id) => (
                  <option key={id} value={id}>
                    {id}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-1">
              <label htmlFor="profile-name" className="block text-sm font-medium text-slate-700 dark:text-slate-200">
                Name
              </label>
              <input
                id="profile-name"
                value={editor.name}
                onChange={(e) => setEditor({ ...editor, name: e.target.value })}
                placeholder="e.g. Litigation voice"
                className={inputClass}
              />
            </div>
          </div>

          <div className="space-y-1">
            <label htmlFor="profile-instructions" className="block text-sm font-medium text-slate-700 dark:text-slate-200">
              Instructions
            </label>
            <textarea
              id="profile-instructions"
              value={editor.instructions}
              onChange={(e) => setEditor({ ...editor, instructions: e.target.value })}
              rows={6}
              placeholder="How this chatbot should behave, in plain language…"
              className={inputClass}
            />
          </div>

          <div className="flex flex-wrap items-center gap-5">
            <fieldset className="flex items-center gap-3">
              <legend className="sr-only">Mode</legend>
              {(["Append", "Replace"] as const).map((mode) => (
                <label key={mode} className="flex items-center gap-1.5 text-sm text-slate-700 dark:text-slate-200">
                  <input
                    type="radio"
                    name="profile-mode"
                    checked={editor.mode === mode}
                    onChange={() => setEditor({ ...editor, mode })}
                  />
                  {mode === "Append" ? "Append (specialize)" : "Replace (retask)"}
                </label>
              ))}
            </fieldset>
            <label className="flex items-center gap-1.5 text-sm text-slate-700 dark:text-slate-200">
              <input
                type="checkbox"
                checked={editor.isDefault}
                onChange={(e) => setEditor({ ...editor, isDefault: e.target.checked })}
              />
              Default for this module
            </label>
          </div>

          {save.isError && <p className="text-xs text-red-600">{(save.error as Error).message}</p>}

          <div className="flex gap-2">
            <button
              type="submit"
              disabled={save.isPending || !editor.name.trim() || !editor.instructions.trim() || !editor.moduleId}
              className="focus-ring rounded bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-500 disabled:opacity-40"
            >
              {save.isPending ? "Saving…" : "Save profile"}
            </button>
            <button
              type="button"
              onClick={() => setEditor(null)}
              className="focus-ring rounded border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-600 dark:border-slate-600 dark:text-slate-300"
            >
              Cancel
            </button>
          </div>
        </form>
      )}

      <ConfirmDialog
        open={deleting !== null}
        title="Delete profile"
        body={`Delete "${deleting?.name}"? The ${deleting?.moduleId} chatbot falls back to ${
          deleting?.isDefault ? "its built-in instructions" : "the current default profile"
        }.`}
        confirmLabel="Delete"
        tone="danger"
        onConfirm={() => {
          if (deleting) remove.mutate(deleting.id);
          setDeleting(null);
        }}
        onCancel={() => setDeleting(null)}
      />
    </div>
  );
}
