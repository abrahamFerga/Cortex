import { NavLink, Navigate, Route, Routes, useLocation } from "react-router-dom";
import { AdminErrorBoundary } from "../components/AdminErrorBoundary";
import { SecurityCatalogView } from "./SecurityCatalogView";
import { RolesEditor } from "./RolesEditor";
import { UsersAdmin } from "./UsersAdmin";
import { ModulesAdmin } from "./ModulesAdmin";
import { TenantsAdmin } from "./TenantsAdmin";
import { AiSettingsAdmin } from "./AiSettingsAdmin";
import { UsageDashboard } from "./UsageDashboard";
import { AuditLog } from "./AuditLog";

// Routes are relative to the router's /admin basename, so these are bare section paths.
const SECTIONS = [
  { to: "/", label: "Security", end: true },
  { to: "/roles", label: "Roles", end: false },
  { to: "/users", label: "Users", end: false },
  { to: "/modules", label: "Modules", end: false },
  { to: "/tenants", label: "Tenants", end: false },
  { to: "/ai", label: "AI Settings", end: false },
  { to: "/usage", label: "Token Usage", end: false },
  { to: "/audit", label: "Audit Log", end: false },
];

function subNavClass({ isActive }: { isActive: boolean }): string {
  const base = "block rounded-md px-3 py-2 text-sm font-medium transition-colors";
  return isActive
    ? `${base} bg-brand-600 text-white`
    : `${base} text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800`;
}

/**
 * The platform administration area: configure RBAC and AI tool permissions,
 * inspect the security map, watch token usage, and review the agent audit log.
 * This is the whole surface of the admin console app (served at /admin).
 */
export function AdminPage() {
  const location = useLocation();
  return (
    <div className="flex min-h-0 flex-1">
      <nav className="w-52 shrink-0 border-r border-slate-200 bg-white p-3 dark:border-slate-700 dark:bg-slate-900">
        <p className="mb-2 px-3 text-xs font-semibold uppercase tracking-wide text-slate-400">
          Administration
        </p>
        <ul className="space-y-1">
          {SECTIONS.map((s) => (
            <li key={s.to}>
              <NavLink to={s.to} end={s.end} className={subNavClass}>
                {s.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      <main
        id="main-content"
        tabIndex={-1}
        aria-label="Administration"
        className="min-h-0 flex-1 overflow-y-auto p-6 focus:outline-none"
      >
        {/* Keyed on the route so a crash in one section clears when the operator navigates to another. */}
        <AdminErrorBoundary key={location.pathname}>
          <Routes>
            <Route index element={<SecurityCatalogView />} />
            <Route path="roles" element={<RolesEditor />} />
            <Route path="users" element={<UsersAdmin />} />
            <Route path="modules" element={<ModulesAdmin />} />
            <Route path="tenants" element={<TenantsAdmin />} />
            <Route path="ai" element={<AiSettingsAdmin />} />
            <Route path="usage" element={<UsageDashboard />} />
            <Route path="audit" element={<AuditLog />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </AdminErrorBoundary>
      </main>
    </div>
  );
}
