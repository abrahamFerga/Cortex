// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { TenantsAdmin } from "./TenantsAdmin";

const TENANTS = [
  { id: "t1", name: "Acme", slug: "acme", isActive: true, createdAt: "2026-06-01T00:00:00Z" },
  { id: "t2", name: "Globex", slug: "globex", isActive: false, createdAt: "2026-06-02T00:00:00Z" },
];

function stubApi() {
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    if (url.includes("/api/admin/tenants") && method === "GET") {
      return Promise.resolve({ ok: true, json: () => Promise.resolve(TENANTS) } as unknown as Response);
    }
    return Promise.resolve({ ok: true, json: () => Promise.resolve(null) } as unknown as Response);
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderTenants() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <TenantsAdmin />
    </QueryClientProvider>,
  );
}

describe("TenantsAdmin", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    vi.unstubAllGlobals();
  });

  it("lists every tenant with its active state", async () => {
    stubApi();
    renderTenants();

    expect(await screen.findByText("Acme")).toBeTruthy();
    expect(screen.getByText("Globex")).toBeTruthy();
    // Globex is inactive, so it carries the marker.
    expect(screen.getByText("inactive")).toBeTruthy();
  });

  it("deactivates a tenant via PUT after confirming through the dialog", async () => {
    const fetchMock = stubApi();
    renderTenants();

    await screen.findByText("Acme");
    // Acme is active, so it offers "Deactivate".
    fireEvent.click(screen.getAllByRole("button", { name: "Deactivate" })[0]);

    // The dialog spells out the consequence before anything is sent.
    const dialog = await screen.findByRole("alertdialog");
    expect(within(dialog).getByText(/All of this tenant's users will be denied access/)).toBeTruthy();
    fireEvent.click(within(dialog).getByRole("button", { name: "Deactivate" }));

    await waitFor(() => {
      const put = fetchMock.mock.calls.find(
        (c) => String(c[0]).includes("/api/admin/tenants/t1/active") && (c[1] as RequestInit | undefined)?.method === "PUT",
      );
      expect(put).toBeTruthy();
      expect(JSON.parse((put![1] as RequestInit).body as string)).toEqual({ isActive: false });
    });
  });

  it("does not deactivate when the confirmation is dismissed", async () => {
    const fetchMock = stubApi();
    renderTenants();

    await screen.findByText("Acme");
    fireEvent.click(screen.getAllByRole("button", { name: "Deactivate" })[0]);

    const dialog = await screen.findByRole("alertdialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "Cancel" }));

    expect(screen.queryByRole("alertdialog")).toBeNull();
    expect(
      fetchMock.mock.calls.some((c) => (c[1] as RequestInit | undefined)?.method === "PUT"),
    ).toBe(false);
  });

  it("activates an inactive tenant in one click without confirmation", async () => {
    const fetchMock = stubApi();
    renderTenants();

    await screen.findByText("Globex");
    // Globex is inactive, so it offers "Activate" — no dialog on the activate direction.
    fireEvent.click(screen.getByRole("button", { name: "Activate" }));
    expect(screen.queryByRole("alertdialog")).toBeNull();

    await waitFor(() => {
      const put = fetchMock.mock.calls.find(
        (c) => String(c[0]).includes("/api/admin/tenants/t2/active") && (c[1] as RequestInit | undefined)?.method === "PUT",
      );
      expect(put).toBeTruthy();
      expect(JSON.parse((put![1] as RequestInit).body as string)).toEqual({ isActive: true });
    });
  });
});
