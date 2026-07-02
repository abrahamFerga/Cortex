// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ModulesAdmin } from "./ModulesAdmin";

const MODULES = [
  { id: "finance", displayName: "Finance", description: "Money stuff.", enabled: true },
  { id: "nutrition", displayName: "Nutrition", description: "Food stuff.", enabled: false },
];

function stubApi() {
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    if (url.includes("/api/admin/modules") && method === "GET") {
      return Promise.resolve({ ok: true, json: () => Promise.resolve(MODULES) } as unknown as Response);
    }
    return Promise.resolve({ ok: true, json: () => Promise.resolve(null) } as unknown as Response);
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderModules() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <ModulesAdmin />
    </QueryClientProvider>,
  );
}

describe("ModulesAdmin", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    vi.unstubAllGlobals();
  });

  it("lists installed modules with their enabled state", async () => {
    stubApi();
    renderModules();

    expect(await screen.findByText("Finance")).toBeTruthy();
    expect(screen.getByText("Nutrition")).toBeTruthy();
    // Nutrition is disabled, so it carries the "disabled" marker.
    expect(screen.getByText("disabled")).toBeTruthy();
  });

  it("disables a module via PUT when its switch is toggled off", async () => {
    const fetchMock = stubApi();
    renderModules();

    await screen.findByText("Finance");
    // First switch is finance (currently enabled) — toggle it off.
    fireEvent.click(screen.getAllByRole("switch")[0]);

    await waitFor(() => {
      const put = fetchMock.mock.calls.find(
        (c) => String(c[0]).includes("/api/admin/modules/finance") && (c[1] as RequestInit | undefined)?.method === "PUT",
      );
      expect(put).toBeTruthy();
      expect(JSON.parse((put![1] as RequestInit).body as string)).toEqual({ enabled: false });
    });
  });
});
