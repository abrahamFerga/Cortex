// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { InvitesPanel } from "./InvitesPanel";

const PENDING = [
  { id: "inv-1", email: "pending@example.com", roles: ["tenant_admin"], createdAt: "2026-07-11T00:00:00Z", redeemedAt: null },
  { id: "inv-2", email: "done@example.com", roles: [], createdAt: "2026-07-10T00:00:00Z", redeemedAt: "2026-07-10T12:00:00Z" },
];

function stubApi() {
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    if (url.includes("/api/admin/users/invites") && method === "GET") {
      return Promise.resolve({ ok: true, json: () => Promise.resolve(PENDING) } as unknown as Response);
    }
    if (url.includes("/api/admin/users/invites") && method === "POST") {
      return Promise.resolve({
        ok: true,
        json: () =>
          Promise.resolve({ id: "inv-3", emailSent: false, message: "Invite recorded for ada@example.com." }),
      } as unknown as Response);
    }
    return Promise.resolve({ ok: true, json: () => Promise.resolve(null) } as unknown as Response);
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderPanel() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <InvitesPanel allRoles={["tenant_admin", "user"]} />
    </QueryClientProvider>,
  );
}

describe("InvitesPanel", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    vi.unstubAllGlobals();
  });

  it("lists only pending invites, with their starting roles", async () => {
    stubApi();
    renderPanel();

    expect(await screen.findByText("pending@example.com")).toBeTruthy();
    // Appears twice: as a role checkbox and on the pending invite row.
    expect(screen.getAllByText("tenant_admin").length).toBeGreaterThanOrEqual(2);
    // The redeemed invite is history, not a pending action.
    expect(screen.queryByText("done@example.com")).toBeNull();
  });

  it("submits an invite with the chosen roles and relays the server's honest message", async () => {
    const fetchMock = stubApi();
    renderPanel();
    await screen.findByText("pending@example.com");

    fireEvent.change(screen.getByPlaceholderText("ada@example.com"), {
      target: { value: "ada@example.com" },
    });
    fireEvent.click(screen.getByLabelText(/tenant_admin/));
    fireEvent.click(screen.getByRole("button", { name: "Invite" }));

    await waitFor(() => {
      const post = fetchMock.mock.calls.find(
        (c) => String(c[0]).includes("/api/admin/users/invites") && (c[1] as RequestInit)?.method === "POST",
      );
      expect(post).toBeTruthy();
      expect(JSON.parse((post![1] as RequestInit).body as string)).toEqual({
        email: "ada@example.com",
        roles: ["tenant_admin"],
      });
    });

    // The no-SMTP deployment gets the truth, not a fake "email sent".
    expect(await screen.findByText(/Invite recorded/)).toBeTruthy();
  });

  it("revokes a pending invite", async () => {
    const fetchMock = stubApi();
    renderPanel();
    await screen.findByText("pending@example.com");

    fireEvent.click(screen.getByRole("button", { name: "Revoke" }));

    await waitFor(() => {
      expect(
        fetchMock.mock.calls.some(
          (c) =>
            String(c[0]).includes("/api/admin/users/invites/inv-1") &&
            (c[1] as RequestInit | undefined)?.method === "DELETE",
        ),
      ).toBe(true);
    });
  });
});
