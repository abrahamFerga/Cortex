// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { SecurityCatalogView } from "./SecurityCatalogView";

const CATALOG = {
  platform: [
    { permission: "platform.users.manage", category: "Platform administration", description: "Manage users.", requiresApproval: false, audited: false },
  ],
  modules: [
    {
      id: "finance",
      displayName: "Finance",
      tools: [
        { permission: "tools.finance.record_transaction", category: "Tool · Finance", description: "Record a transaction.", requiresApproval: true, audited: true },
      ],
    },
  ],
};

function stubApi() {
  vi.stubGlobal(
    "fetch",
    vi.fn((input: RequestInfo | URL) => {
      if (String(input).includes("/api/admin/security/catalog")) {
        return Promise.resolve({ ok: true, json: () => Promise.resolve(CATALOG) } as unknown as Response);
      }
      return Promise.resolve({ ok: true, json: () => Promise.resolve(null) } as unknown as Response);
    }),
  );
}

describe("SecurityCatalogView", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    vi.unstubAllGlobals();
  });

  it("renders the platform permissions and each module's agent tools from the catalog", async () => {
    stubApi();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <SecurityCatalogView />
      </QueryClientProvider>,
    );

    expect(await screen.findByText("platform.users.manage")).toBeTruthy();
    expect(screen.getByText(/Finance.*agent tools/i)).toBeTruthy();
    expect(screen.getByText("tools.finance.record_transaction")).toBeTruthy();
    // The record-transaction tool is side-effecting + audited, so it carries those flags.
    expect(screen.getByText("approval")).toBeTruthy();
    expect(screen.getByText("audited")).toBeTruthy();
  });
});
