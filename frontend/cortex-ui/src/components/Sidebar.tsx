import { NavLink } from "react-router-dom";
import type { ModuleTab } from "../lib/api";

interface SidebarProps {
  moduleId: string | undefined;
  tabs: ModuleTab[];
}

function navClass({ isActive }: { isActive: boolean }): string {
  const base =
    "block rounded-md px-3 py-2 text-sm font-medium transition-colors";
  return isActive
    ? `${base} bg-brand-600 text-white`
    : `${base} text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800`;
}

export function Sidebar({ moduleId, tabs }: SidebarProps) {
  return (
    <nav className="w-56 shrink-0 border-r border-slate-200 bg-white p-3 dark:border-slate-700 dark:bg-slate-900">
      {moduleId && (
        <p className="mb-2 px-3 text-xs font-semibold uppercase tracking-wide text-slate-400">
          {moduleId}
        </p>
      )}
      <ul className="space-y-1">
        {tabs.map((tab) => (
          <li key={tab.id}>
            <NavLink to={tab.route} end className={navClass}>
              {tab.label}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
