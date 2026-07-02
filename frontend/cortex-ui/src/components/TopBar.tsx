import { NavLink } from "react-router-dom";
import { useMe } from "../hooks/useMe";
import { useBranding } from "../lib/branding";
import { ModuleSwitcher } from "./ModuleSwitcher";

/** True if the caller holds any platform-administration permission (or the global wildcard). */
function canAdminister(permissions: string[]): boolean {
  return permissions.some(
    (p) => p === "*" || p === "platform.*" || p.startsWith("platform."),
  );
}

// The admin console is a separate app (served at /admin), not a route inside this shell — so the
// link is a real navigation, not a router NavLink. Override with VITE_ADMIN_URL when the console
// is hosted elsewhere.
const ADMIN_URL = import.meta.env.VITE_ADMIN_URL ?? "/admin";

export function TopBar() {
  const { data: me } = useMe();
  const { name = "Cortex", logo } = useBranding();
  const showAdmin = canAdminister(me?.permissions ?? []);

  return (
    <header className="flex h-14 items-center gap-6 border-b border-slate-200 bg-white px-4 dark:border-slate-700 dark:bg-slate-900">
      <div className="flex items-center gap-2">
        {logo ?? (
          <div className="flex h-7 w-7 items-center justify-center rounded-md bg-brand-600 text-sm font-bold text-white">
            C
          </div>
        )}
        <span className="text-lg font-semibold tracking-tight text-slate-900 dark:text-slate-100">
          {name}
        </span>
      </div>

      <ModuleSwitcher />

      <nav className="flex items-center gap-1 text-sm">
        <NavLink
          to="/chat"
          className={({ isActive }) =>
            `rounded-md px-3 py-1.5 font-medium ${
              isActive
                ? "bg-slate-100 text-slate-900 dark:bg-slate-800 dark:text-slate-100"
                : "text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-slate-100"
            }`
          }
        >
          Workspace
        </NavLink>
        {showAdmin && (
          <a
            href={ADMIN_URL}
            className="rounded-md px-3 py-1.5 font-medium text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-slate-100"
          >
            Admin ↗
          </a>
        )}
      </nav>

      <div className="ml-auto text-sm text-slate-600 dark:text-slate-300">
        {me?.displayName ?? "…"}
      </div>
    </header>
  );
}
