// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { UsersAdmin } from "./UsersAdmin";

const USERS = [
  {
    id: "u1",
    subject: "sub-alice",
    email: "alice@example.com",
    displayName: "Alice",
    isActive: true,
    lastSeenAt: null,
    roles: ["user"],
    permissions: [],
  },
];

const ROLES = [
  { role: "system_admin", permissions: ["*"], editable: false, builtIn: true },
  { role: "user", permissions: ["chat.use"], editable: true, builtIn: true },
  { role: "auditor", permissions: ["platform.audit.view"], editable: true, builtIn: false },
];

const CATALOG = { platform: [], modules: [] };

function stubApi() {
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    if (url.includes("/api/admin/users") && method === "GET") {
      return Promise.resolve({ ok: true, json: () => Promise.resolve(USERS) } as unknown as Response);
    }
    if (url.includes("/api/admin/roles") && method === "GET") {
      return Promise.resolve({ ok: true, json: () => Promise.resolve(ROLES) } as unknown as Response);
    }
    if (url.includes("/api/admin/security/catalog")) {
      return Promise.resolve({ ok: true, json: () => Promise.resolve(CATALOG) } as unknown as Response);
    }
    return Promise.resolve({ ok: true, json: () => Promise.resolve(null) } as unknown as Response);
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderUsers() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <UsersAdmin />
    </QueryClientProvider>,
  );
}

describe("UsersAdmin", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    vi.unstubAllGlobals();
  });

  it("renders a user with their email and external subject", async () => {
    stubApi();
    renderUsers();

    expect(await screen.findByText("Alice")).toBeTruthy();
    expect(screen.getByText("alice@example.com")).toBeTruthy();
    expect(screen.getByText("sub-alice")).toBeTruthy();
  });

  it("deactivates a user via PUT after confirming through the dialog", async () => {
    const fetchMock = stubApi();
    renderUsers();

    fireEvent.click(await screen.findByRole("button", { name: "Deactivate" }));

    // Deactivation is confirmed through a dialog before the PUT is issued.
    const dialog = await screen.findByRole("alertdialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "Deactivate" }));

    await waitFor(() => {
      const put = fetchMock.mock.calls.find(
        (c) => String(c[0]).includes("/api/admin/users/u1/active") && (c[1] as RequestInit | undefined)?.method === "PUT",
      );
      expect(put).toBeTruthy();
      expect(JSON.parse((put![1] as RequestInit).body as string)).toEqual({ isActive: false });
    });
  });

  it("does not deactivate when the confirmation is dismissed", async () => {
    const fetchMock = stubApi();
    renderUsers();

    fireEvent.click(await screen.findByRole("button", { name: "Deactivate" }));

    const dialog = await screen.findByRole("alertdialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "Cancel" }));

    expect(screen.queryByRole("alertdialog")).toBeNull();
    expect(
      fetchMock.mock.calls.some((c) => (c[1] as RequestInit | undefined)?.method === "PUT"),
    ).toBe(false);
  });

  it("offers custom roles for assignment and assigns one via POST", async () => {
    const fetchMock = stubApi();
    renderUsers();

    await screen.findByText("Alice");
    const select = screen.getByLabelText("Add a role");

    // The custom (tenant-defined) role is assignable — the dropdown sources from GET /roles, not a hardcoded list.
    expect(screen.getByRole("option", { name: "auditor" })).toBeTruthy();

    fireEvent.change(select, { target: { value: "auditor" } });

    await waitFor(() => {
      const post = fetchMock.mock.calls.find(
        (c) => String(c[0]).includes("/api/admin/users/u1/roles") && (c[1] as RequestInit | undefined)?.method === "POST",
      );
      expect(post).toBeTruthy();
      expect(JSON.parse((post![1] as RequestInit).body as string)).toEqual({ role: "auditor" });
    });
  });
});
