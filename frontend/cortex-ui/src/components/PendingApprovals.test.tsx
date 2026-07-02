// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { PendingApprovals } from "./PendingApprovals";

// Route the API calls PendingApprovals makes: GET /me (to learn the user can approve), GET the pending
// list, and the POST /approve when clicked.
function stubApi() {
  const fetchMock = vi.fn((input: RequestInfo | URL) => {
    const url = String(input);
    if (url.endsWith("/api/platform/me")) {
      return Promise.resolve({
        ok: true,
        json: () =>
          Promise.resolve({ userId: "u1", displayName: "Dev", tenantId: "t1", permissions: ["chat.approvals.manage"] }),
      } as unknown as Response);
    }
    if (url.endsWith("/api/chat/approvals")) {
      return Promise.resolve({
        ok: true,
        json: () =>
          Promise.resolve([
            {
              id: "ap1",
              conversationId: "c1",
              moduleId: "finance",
              toolName: "record_transaction",
              argumentsJson: '{"description":"Lunch","amount":12}',
              createdAt: "2026-06-28",
            },
          ]),
      } as unknown as Response);
    }
    return Promise.resolve({ ok: true, json: () => Promise.resolve(null) } as unknown as Response);
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderApprovals() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <PendingApprovals moduleId="finance" />
    </QueryClientProvider>,
  );
}

describe("PendingApprovals (human-in-the-loop gate)", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("lists a blocked tool call with its arguments and approves it on click", async () => {
    const fetchMock = stubApi();
    renderApprovals();

    // The blocked side-effecting call surfaces, with its recorded arguments.
    expect(await screen.findByText("record_transaction")).toBeTruthy();
    expect(screen.getByText(/Lunch/)).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Approve" }));

    // Approving re-executes that exact call on the server (POST …/approvals/ap1/approve).
    await waitFor(() =>
      expect(fetchMock.mock.calls.some((c) => String(c[0]).includes("/api/chat/approvals/ap1/approve"))).toBe(true),
    );
  });
});
