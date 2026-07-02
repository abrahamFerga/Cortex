// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AppShell } from "./AppShell";
import { defineModule, type ModuleTabProps } from "../lib/moduleUi";

const manifest = [
  {
    id: "finance",
    displayName: "Finance",
    tabs: [{ id: "transactions", label: "Transactions", route: "/finance/transactions" }],
  },
];

const json = (body: unknown) =>
  Promise.resolve({ ok: true, json: () => Promise.resolve(body) } as unknown as Response);

function stubApi(chatEnabled = true) {
  vi.stubGlobal(
    "fetch",
    vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url.includes("/api/platform/modules")) return json(manifest);
      if (url.includes("/api/platform/me"))
        return json({ userId: "u", displayName: "Dev", tenantId: "t", permissions: [] });
      if (url.includes("/api/platform/info")) return json({ chatEnabled, demoMode: false });
      return json(null);
    }),
  );
}

function stubModulesError(status: number) {
  vi.stubGlobal(
    "fetch",
    vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url.includes("/api/platform/modules")) {
        return Promise.resolve({
          ok: false,
          status,
          statusText: status === 403 ? "Forbidden" : "Server Error",
          text: () => Promise.resolve(""),
        } as unknown as Response);
      }
      if (url.includes("/api/platform/info")) return json({ chatEnabled: true, demoMode: false });
      if (url.includes("/api/platform/me"))
        return json({ userId: "u", displayName: "Dev", tenantId: "t", permissions: [] });
      return json(null);
    }),
  );
}

function renderAt(path: string) {
  const Board = ({ moduleId, tab }: ModuleTabProps) => <div>{`board:${moduleId}:${tab.id}`}</div>;
  const finance = defineModule("finance", { tabs: { transactions: Board } });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter
        initialEntries={[path]}
        future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
      >
        <AppShell moduleUi={[finance]} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("AppShell deep-linking", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("renders a deep-linked tab route instead of bouncing to /chat", async () => {
    stubApi();
    renderAt("/finance/transactions");

    // The active module is resolved from the URL on the first loaded render, so the tab's route
    // exists before the catch-all redirect can fire — the registered component renders.
    expect(await screen.findByText("board:finance:transactions")).toBeTruthy();
  });

  it("shows the Chat tab in the nav when chat is enabled", async () => {
    stubApi(true);
    renderAt("/finance/transactions"); // avoid /chat so ChatView (SignalR) isn't mounted

    await screen.findByText("board:finance:transactions");
    expect(screen.getByText("Chat")).toBeTruthy(); // the sidebar's Chat nav link
  });

  it("hides the Chat tab and lands on a module tab when chat is disabled (no AI provider)", async () => {
    stubApi(false);
    renderAt("/"); // default landing

    // With no chat, the default landing is the module's first tab, and there's no Chat nav link.
    expect(await screen.findByText("board:finance:transactions")).toBeTruthy();
    expect(screen.queryByText("Chat")).toBeNull();
  });

  it("shows an access-denied screen (not 'unreachable') when the manifest load is forbidden", async () => {
    stubModulesError(403);
    renderAt("/");

    expect(await screen.findByText("You don't have access")).toBeTruthy();
    expect(screen.queryByText("Can't reach the Cortex API")).toBeNull();
  });

  it("shows the unreachable screen for a non-auth failure", async () => {
    stubModulesError(500);
    renderAt("/");

    expect(await screen.findByText("Can't reach the Cortex API")).toBeTruthy();
  });

  it("exposes a skip-to-content link and a labelled main landmark", async () => {
    stubApi();
    renderAt("/finance/transactions");
    await screen.findByText("board:finance:transactions");

    const skip = screen.getByRole("link", { name: "Skip to content" });
    expect(skip.getAttribute("href")).toBe("#main-content");
    expect(screen.getByRole("main", { name: "Workspace" })).toBeTruthy();
  });
});
