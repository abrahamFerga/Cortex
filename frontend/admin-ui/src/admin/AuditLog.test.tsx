// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuditLog } from "./AuditLog";

const TOOL_CALLS = [
  {
    id: "tc1",
    occurredAt: "2026-06-30T10:00:00Z",
    userDisplay: "Dev User",
    moduleId: "finance",
    toolName: "summarize_spending",
    permission: "tools.finance.summarize_spending",
    success: true,
    error: null,
    durationMs: 42,
  },
];

const AUTH_EVENTS = [
  {
    id: "ae1",
    occurredAt: "2026-06-30T10:01:00Z",
    eventType: "RolePermissionsChanged",
    userDisplay: "Dev User",
    subject: "sub-1",
    detail: "role 'user': granted chat.use",
    ipAddress: null,
  },
];

function stubApi() {
  vi.stubGlobal(
    "fetch",
    vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url.includes("/api/admin/audit/tool-calls")) {
        return Promise.resolve({ ok: true, json: () => Promise.resolve(TOOL_CALLS) } as unknown as Response);
      }
      if (url.includes("/api/admin/audit/auth-events")) {
        return Promise.resolve({ ok: true, json: () => Promise.resolve(AUTH_EVENTS) } as unknown as Response);
      }
      return Promise.resolve({ ok: true, json: () => Promise.resolve([]) } as unknown as Response);
    }),
  );
}

describe("AuditLog", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    vi.unstubAllGlobals();
  });

  it("renders both the agent tool-call log and the access/security events", async () => {
    stubApi();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <AuditLog />
      </QueryClientProvider>,
    );

    // Both section headings…
    expect(await screen.findByText("Agent tool calls")).toBeTruthy();
    expect(screen.getByText(/Access .* security events/i)).toBeTruthy();
    // …a tool call row…
    expect(await screen.findByText("summarize_spending")).toBeTruthy();
    // …and a security event with its type badge.
    expect(await screen.findByText("RolePermissionsChanged")).toBeTruthy();
  });
});
