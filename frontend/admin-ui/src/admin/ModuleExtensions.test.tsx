// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { ExtensionPage } from "./ModuleExtensions";

const EXTENSIONS = [
  {
    id: "finance",
    displayName: "Networthy Finance",
    tabs: [
      {
        id: "institutions",
        label: "Institutions",
        route: "/ext/finance/institutions",
        dataEndpoint: "/api/finance/admin/institutions",
        columns: [
          { field: "name", header: "Institution" },
          { field: "status", header: "Status" },
        ],
      },
    ],
  },
];

const ROWS = [
  { name: "Citibanamex", status: "linked" },
  { name: "BBVA", status: "pending" },
];

function stubApi() {
  const fetchMock = vi.fn((input: RequestInfo | URL) => {
    const url = String(input);
    if (url.includes("/api/admin/extensions")) {
      return Promise.resolve({ ok: true, json: () => Promise.resolve(EXTENSIONS) } as unknown as Response);
    }
    if (url.includes("/api/finance/admin/institutions")) {
      return Promise.resolve({ ok: true, json: () => Promise.resolve(ROWS) } as unknown as Response);
    }
    return Promise.resolve({ ok: true, json: () => Promise.resolve(null) } as unknown as Response);
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/ext/:moduleId/:tabId" element={<ExtensionPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("ExtensionPage", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    vi.unstubAllGlobals();
  });

  it("renders a module-declared admin page through the generic tab machinery", async () => {
    stubApi();
    renderAt("/ext/finance/institutions");

    // The page attributes itself to the owning module, and the generic tab renders its heading…
    expect(await screen.findByText("Institutions")).toBeTruthy();
    expect(screen.getByText("Networthy Finance")).toBeTruthy();

    // …and the declared dataEndpoint's rows render in the declared columns.
    expect(await screen.findByText("Citibanamex")).toBeTruthy();
    expect(screen.getByText("BBVA")).toBeTruthy();
    expect(screen.getByText("Institution")).toBeTruthy();
  });

  it("says so when the page doesn't exist or the caller can't see it", async () => {
    stubApi();
    renderAt("/ext/finance/nope");

    expect(await screen.findByText(/doesn't exist here/)).toBeTruthy();
  });
});
