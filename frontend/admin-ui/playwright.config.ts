import { defineConfig, devices } from "@playwright/test";

// Real-browser E2E for the admin console. The API is mocked at the network layer (see e2e/), so no backend
// is needed — Playwright starts the Vite dev server itself. The console is served under the /admin base, so
// baseURL is the origin and tests navigate to /admin/. `.pw.ts` suffix keeps vitest from picking these up.
export default defineConfig({
  testDir: "./e2e",
  testMatch: "**/*.pw.ts",
  fullyParallel: true,
  reporter: "list",
  use: {
    baseURL: "http://localhost:5174",
    trace: "on-first-retry",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    command: "npx vite --port 5174",
    url: "http://localhost:5174/admin/",
    reuseExistingServer: true,
    timeout: 120_000,
  },
});
