import { createContext, useContext, type ReactNode } from "react";

/**
 * Host branding for the domain shell — the product name and logo shown in the top bar. Supplied via
 * `CortexApp`/`AppShell`'s `branding` prop so a host presents its own identity, not "Cortex". (The accent
 * color is themed separately, via the `--cortex-brand-*` CSS variables.)
 */
export interface CortexBranding {
  /** Product name shown in the top bar. Defaults to "Cortex". */
  name?: string;
  /** Custom logo node (e.g. an <img> or SVG). Defaults to the Cortex mark. */
  logo?: ReactNode;
}

export const BrandingContext = createContext<CortexBranding>({});

/** The active host branding (empty defaults when no `branding` was provided). */
export function useBranding(): CortexBranding {
  return useContext(BrandingContext);
}
