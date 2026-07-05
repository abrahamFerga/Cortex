// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { OpsView } from "./OpsView";

const snapshot = {
  jobs: { queued: 2, running: 1, failed24h: 3, oldestQueuedAgeSeconds: 900 },
  connectors: [{ connectorId: "local-folder", bindingCount: 4, lastSyncedAt: new Date().toISOString() }],
  rag: { collections: 5, chunks: 1234, lastIngestAt: new Date().toISOString() },
  notifications: { webhookConfigured: false },
  ai: { provider: "Mock", model: "gpt-4o-mini", monthTokens: 850, maxMonthlyTokens: 1000 },
};

function renderOps(data: unknown = snapshot) {
  vi.stubGlobal(
    "fetch",
    vi.fn(() =>
      Promise.resolve({ ok: true, json: () => Promise.resolve(data) } as unknown as Response),
    ),
  );
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <OpsView />
    </QueryClientProvider>,
  );
}

describe("OpsView (tenant health snapshot)", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("renders the four health cards from one snapshot call", async () => {
    renderOps();

    expect(await screen.findByText("Background jobs")).toBeTruthy();
    expect(screen.getByText("Failed (24h)")).toBeTruthy();
    // 15-minute-old queued job -> the backlog warning shows.
    expect(screen.getByText(/waited 15 minutes/)).toBeTruthy();
    expect(screen.getByText("local-folder")).toBeTruthy();
    // 850 of 1000 = 85% -> budget shown with percentage (and alarm styling).
    expect(screen.getByText("850 (85%)")).toBeTruthy();
    expect(screen.getByText(/Webhook delivery: not configured/)).toBeTruthy();
  });

  it("reads cleanly with no budget and no connectors", async () => {
    renderOps({
      ...snapshot,
      jobs: { queued: 0, running: 0, failed24h: 0 },
      connectors: [],
      ai: { ...snapshot.ai, maxMonthlyTokens: 0 },
    });

    expect(await screen.findByText("No connectors enabled.")).toBeTruthy();
    expect(screen.getByText(/No monthly budget set/)).toBeTruthy();
  });
});
