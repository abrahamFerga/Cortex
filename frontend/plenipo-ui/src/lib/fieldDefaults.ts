import type { TabEditorField } from "./api";

/**
 * What a server-declared form starts as, shared by the generic tab editor and the setup wizard.
 *
 * The shell still never pre-picks an option on its own — that guessing is exactly what the blank
 * "Choose…" entry exists to prevent. This is the other thing: a field SAYING what it should start
 * as. `default` is a constant the manifest knows; `defaultFrom` is for what only the viewer's
 * browser knows, because a manifest declared at startup cannot know where the viewer lives.
 *
 * A default is a starting point, never a value posted behind the user's back — they can clear or
 * change it, and an untouched empty field still posts nothing.
 */

/** Values only the browser can answer. Keep in sync with FieldDefaultSources (C#). */
const SOURCES: Record<string, () => string | undefined> = {
  "browser-timezone": browserTimeZone,
};

function browserTimeZone(): string | undefined {
  try {
    // Absent in exotic/locked-down runtimes; a missing time zone is not worth throwing over.
    return Intl.DateTimeFormat().resolvedOptions().timeZone || undefined;
  } catch {
    return undefined;
  }
}

/** The starting value for one field: "" unless it declares a default the field can actually take. */
export function resolveFieldDefault(field: TabEditorField): string {
  const dynamic = field.defaultFrom ? SOURCES[field.defaultFrom]?.() : undefined;
  const candidate = dynamic ?? field.default ?? "";
  if (!candidate) return "";

  // A default the field would reject is worse than none: offering a value whose own endpoint
  // 400s it just moves the confusion later. Options loaded from an endpoint aren't known here,
  // so only a declared vocabulary is checked.
  if (field.options && !field.options.some((o) => o.value === candidate)) return "";
  return candidate;
}

/** Starting values for a whole form, keyed by field name. */
export function resolveFieldDefaults(fields: TabEditorField[]): Record<string, string> {
  return Object.fromEntries(fields.map((f) => [f.field, resolveFieldDefault(f)]));
}
