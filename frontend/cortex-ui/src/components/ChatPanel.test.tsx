// @vitest-environment jsdom
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { HubConnection } from "@microsoft/signalr";

// Replace the SignalR connection with a stub so the panel renders without a real hub.
const { mockConnection, disposeMock } = vi.hoisted(() => {
  const disposeMock = vi.fn();
  return {
    disposeMock,
    mockConnection: {
      start: vi.fn(() => Promise.resolve()),
      stop: vi.fn(() => Promise.resolve()),
      stream: vi.fn(() => ({ subscribe: vi.fn(() => ({ dispose: disposeMock })) })),
    },
  };
});

vi.mock("../lib/signalr", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../lib/signalr")>();
  return { ...actual, createAgentConnection: (): HubConnection => mockConnection as unknown as HubConnection };
});

import { ChatPanel } from "./ChatPanel";

function renderChat() {
  // /me returns no approval rights, so the embedded PendingApprovals stays hidden.
  vi.stubGlobal(
    "fetch",
    vi.fn(() => Promise.resolve({ ok: true, json: () => Promise.resolve({ permissions: [] }) } as unknown as Response)),
  );
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <ChatPanel moduleId="finance" suggestedPrompts={["Summarize my spending"]} />
    </QueryClientProvider>,
  );
}

describe("ChatPanel", () => {
  beforeAll(() => {
    // jsdom doesn't implement Element.scrollTo, which the chat list's auto-scroll effect calls.
    Element.prototype.scrollTo = vi.fn() as unknown as typeof Element.prototype.scrollTo;
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    vi.unstubAllGlobals();
  });

  it("offers the starter prompts and streams the one the user clicks", () => {
    renderChat();

    fireEvent.click(screen.getByRole("button", { name: "Summarize my spending" }));

    // Clicking a starter sends it straight to the hub's streaming method.
    expect(mockConnection.stream).toHaveBeenCalledWith(
      "Stream",
      expect.objectContaining({ moduleId: "finance", message: "Summarize my spending" }),
    );
  });

  it("gives the message input an accessible label (its placeholder vanishes once you type)", () => {
    renderChat();
    expect(screen.getByRole("textbox", { name: "Message" })).toBeTruthy();
  });

  it("shows a Stop button while streaming and cancels the turn (disposing the stream) when clicked", () => {
    renderChat();

    // Sending a turn puts the panel into the streaming state → the Send button becomes Stop.
    fireEvent.click(screen.getByRole("button", { name: "Summarize my spending" }));
    fireEvent.click(screen.getByRole("button", { name: "Stop" }));

    // Disposing the subscription cancels the server-side run; the panel returns to idle (Send is back).
    expect(disposeMock).toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Send" })).toBeTruthy();
  });

  it("resumes a conversation by loading and rendering its message history", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn((input: RequestInfo | URL) => {
        const url = String(input);
        if (url.includes("/api/chat/conversations/c1/messages")) {
          return Promise.resolve({
            ok: true,
            json: () =>
              Promise.resolve([
                { id: "m1", role: "User", content: "What did I spend?" },
                { id: "m2", role: "Assistant", content: "You spent 1,200 on groceries." },
              ]),
          } as unknown as Response);
        }
        // /me and anything else: a user with no special permissions.
        return Promise.resolve({ ok: true, json: () => Promise.resolve({ permissions: [] }) } as unknown as Response);
      }),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <ChatPanel moduleId="finance" conversationId="c1" />
      </QueryClientProvider>,
    );

    // Selecting a conversation loads its persisted history — both the user turn and the assistant reply.
    expect(await screen.findByText("What did I spend?")).toBeTruthy();
    expect(screen.getByText("You spent 1,200 on groceries.")).toBeTruthy();
  });
});
