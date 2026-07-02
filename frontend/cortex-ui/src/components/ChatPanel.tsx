import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import type { HubConnection } from "@microsoft/signalr";
import {
  createAgentConnection,
  type AgentStreamEvent,
} from "../lib/signalr";
import { api, uploadFile, type StoredFileInfo } from "../lib/api";
import { withAttachmentRefs } from "../lib/attachments";
import { Markdown } from "./Markdown";
import { PendingApprovals } from "./PendingApprovals";

interface TokenUsage {
  input: number;
  output: number;
  total: number;
}

interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  text: string;
  tools: string[];
  usage?: TokenUsage;
}

interface ChatPanelProps {
  moduleId: string;
  /** Example prompts shown as one-click starters when the conversation is empty. */
  suggestedPrompts?: string[];
  /** When set, resume this conversation (load its history); when undefined, a fresh conversation. */
  conversationId?: string;
  /** Called when a brand-new conversation gets its server id (so a parent can select/refresh the list). */
  onConversationStarted?: (id: string) => void;
  /** Called when the user clicks "New chat"; a parent that owns selection should clear it. */
  onNewChat?: () => void;
}

function newId(): string {
  return Math.random().toString(36).slice(2);
}

function formatTokens(n: number): string {
  return new Intl.NumberFormat("en-US").format(n);
}

function formatBytes(n: number): string {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(0)} KB`;
  return `${(n / (1024 * 1024)).toFixed(1)} MB`;
}

export function ChatPanel({
  moduleId,
  suggestedPrompts,
  conversationId,
  onConversationStarted,
  onNewChat,
}: ChatPanelProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [status, setStatus] = useState<"idle" | "streaming">("idle");
  const [error, setError] = useState<string | null>(null);
  const [sessionTokens, setSessionTokens] = useState(0);
  const [attachments, setAttachments] = useState<StoredFileInfo[]>([]);
  const [uploading, setUploading] = useState(false);
  const queryClient = useQueryClient();

  const connectionRef = useRef<HubConnection | null>(null);
  const conversationIdRef = useRef<string | undefined>(undefined);
  const subscriptionRef = useRef<{ dispose: () => void } | null>(null);
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  async function attachFiles(list: FileList | null) {
    if (!list || list.length === 0) return;
    setUploading(true);
    setError(null);
    try {
      for (const file of Array.from(list)) {
        const stored = await uploadFile(file);
        setAttachments((prev) => [...prev, stored]);
      }
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : `Upload failed: ${String(e)}`);
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  }

  // Establish the hub connection once.
  useEffect(() => {
    const connection = createAgentConnection();
    connectionRef.current = connection;
    let active = true;
    connection
      .start()
      .then(() => {
        if (active) {
          setError(null);
        }
      })
      .catch((e: unknown) => {
        // React StrictMode double-mounts effects in dev: the cleanup stops the first connection
        // mid-negotiation, which rejects with "stopped during negotiation". Ignore that abort and
        // only surface a real failure from the connection that's still active.
        if (active) {
          setError(`Could not connect to agent hub: ${String(e)}`);
        }
      });

    return () => {
      active = false;
      void connection.stop();
      connectionRef.current = null;
    };
  }, []);

  // Keep the message list scrolled to the bottom.
  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight });
  }, [messages]);

  // Resume the selected conversation (load its history) — or reset for a new one — when the selection
  // changes. Skips when the selection already matches what's shown (e.g. the conversation we just created).
  useEffect(() => {
    if (conversationId === conversationIdRef.current) {
      return;
    }
    if (!conversationId) {
      setMessages([]);
      setError(null);
      setSessionTokens(0);
      conversationIdRef.current = undefined;
      return;
    }

    conversationIdRef.current = conversationId;
    setError(null);
    setSessionTokens(0);
    let active = true;
    api
      .conversationMessages(conversationId)
      .then((history) => {
        if (!active) return;
        setMessages(
          history.map((m) => ({
            id: m.id,
            role: m.role === "Assistant" ? "assistant" : "user",
            text: m.content,
            tools: [],
          })),
        );
      })
      .catch((e: unknown) => {
        if (active) setError(`Could not load conversation: ${String(e)}`);
      });
    return () => {
      active = false;
    };
  }, [conversationId]);

  function appendToAssistant(
    assistantId: string,
    update: (m: ChatMessage) => ChatMessage,
  ) {
    setMessages((prev) =>
      prev.map((m) => (m.id === assistantId ? update(m) : m)),
    );
  }

  // Start a fresh conversation: clear history, drop the server conversation id,
  // and reset the running token tally.
  function newChat() {
    if (status === "streaming") {
      return;
    }
    setMessages([]);
    setError(null);
    setSessionTokens(0);
    conversationIdRef.current = undefined;
  }

  // Stop a streaming turn: disposing the subscription cancels the server-side run (its CancellationToken
  // fires), so we stop paying for tokens too — not just hiding the output.
  function stop() {
    subscriptionRef.current?.dispose();
    subscriptionRef.current = null;
    setStatus("idle");
  }

  function send(explicit?: string) {
    const typed = (explicit ?? input).trim();
    const connection = connectionRef.current;
    if ((!typed && attachments.length === 0) || status === "streaming" || uploading || !connection) {
      return;
    }

    const text = withAttachmentRefs(typed, attachments);

    setError(null);
    setInput("");
    setAttachments([]);

    const userMessage: ChatMessage = {
      id: newId(),
      role: "user",
      text,
      tools: [],
    };
    const assistantId = newId();
    const assistantMessage: ChatMessage = {
      id: assistantId,
      role: "assistant",
      text: "",
      tools: [],
    };
    setMessages((prev) => [...prev, userMessage, assistantMessage]);
    setStatus("streaming");

    const request = {
      moduleId,
      conversationId: conversationIdRef.current,
      message: text,
    };

    subscriptionRef.current = connection.stream("Stream", request).subscribe({
      next: (event: AgentStreamEvent) => {
        switch (event.type) {
          case "Token":
            if (event.text) {
              appendToAssistant(assistantId, (m) => ({
                ...m,
                text: m.text + event.text,
              }));
            }
            break;
          case "ToolInvoked":
            if (event.toolName) {
              const tool = event.toolName;
              appendToAssistant(assistantId, (m) => ({
                ...m,
                tools: [...m.tools, tool],
              }));
            }
            break;
          case "Usage": {
            const usage: TokenUsage = {
              input: event.inputTokens ?? 0,
              output: event.outputTokens ?? 0,
              total: event.totalTokens ?? 0,
            };
            appendToAssistant(assistantId, (m) => ({ ...m, usage }));
            setSessionTokens((t) => t + usage.total);
            break;
          }
          case "ApprovalRequired":
            // A side-effecting tool was blocked; refresh the pending-approvals list so it shows up.
            void queryClient.invalidateQueries({ queryKey: ["approvals"] });
            break;
          case "Completed":
            if (event.conversationId) {
              const wasNew = conversationIdRef.current === undefined;
              conversationIdRef.current = event.conversationId;
              if (wasNew) {
                onConversationStarted?.(event.conversationId);
              }
            }
            break;
          case "Error":
            setError(event.error ?? "Unknown stream error");
            break;
        }
      },
      complete: () => {
        subscriptionRef.current = null;
        setStatus("idle");
      },
      error: (e: unknown) => {
        subscriptionRef.current = null;
        setError(String(e));
        setStatus("idle");
      },
    });
  }

  return (
    <div className="flex h-full flex-col">
      <div className="mb-2 flex items-center justify-between border-b border-slate-200 pb-2 dark:border-slate-700">
        <span className="text-sm font-medium text-slate-600 dark:text-slate-300">
          {moduleId} assistant
        </span>
        <div className="flex items-center gap-3">
          {sessionTokens > 0 && (
            <span
              className="text-xs text-slate-400"
              title="Tokens used in this session (resets when you open or start a conversation)"
            >
              {formatTokens(sessionTokens)} tokens
            </span>
          )}
          <button
            type="button"
            onClick={() => (onNewChat ? onNewChat() : newChat())}
            disabled={status === "streaming" || messages.length === 0}
            className="rounded-md border border-slate-300 px-2 py-1 text-xs font-medium text-slate-600 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-800"
          >
            New chat
          </button>
        </div>
      </div>

      <div ref={scrollRef} className="flex-1 space-y-4 overflow-y-auto pr-2">
        {messages.length === 0 && (
          <div className="text-sm text-slate-400">
            <p>Start a conversation with the {moduleId} agent.</p>
            {suggestedPrompts && suggestedPrompts.length > 0 && (
              <div className="mt-3 flex flex-wrap gap-2">
                <span className="self-center text-xs text-slate-400">Try:</span>
                {suggestedPrompts.map((prompt) => (
                  <button
                    key={prompt}
                    type="button"
                    onClick={() => send(prompt)}
                    disabled={status === "streaming"}
                    className="rounded-full border border-slate-300 px-3 py-1 text-xs text-slate-600 hover:border-brand-400 hover:text-brand-600 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-600 dark:text-slate-300 dark:hover:border-brand-500 dark:hover:text-brand-300"
                  >
                    {prompt}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
        {messages.map((m) => (
          <div
            key={m.id}
            className={m.role === "user" ? "text-right" : "text-left"}
          >
            <div
              className={
                m.role === "user"
                  ? "inline-block max-w-[80%] rounded-lg bg-brand-600 px-3 py-2 text-left text-sm text-white"
                  : "inline-block max-w-[80%] rounded-lg bg-slate-100 px-3 py-2 text-sm text-slate-900 dark:bg-slate-800 dark:text-slate-100"
              }
            >
              {m.tools.length > 0 && (
                <div className="mb-1 flex flex-wrap gap-1">
                  {m.tools.map((tool, i) => (
                    <span
                      key={`${tool}-${i}`}
                      className="rounded-full bg-amber-100 px-2 py-0.5 text-xs text-amber-800 dark:bg-amber-900/40 dark:text-amber-200"
                    >
                      used tool: {tool}
                    </span>
                  ))}
                </div>
              )}
              {m.role === "assistant" ? (
                m.text ? (
                  <div className="text-sm leading-relaxed">
                    <Markdown>{m.text}</Markdown>
                  </div>
                ) : (
                  <span>…</span>
                )
              ) : (
                <span className="whitespace-pre-wrap">{m.text}</span>
              )}
              {m.usage && m.usage.total > 0 && (
                <div className="mt-1 text-[11px] text-slate-400 dark:text-slate-500">
                  {formatTokens(m.usage.total)} tokens · ↑{formatTokens(m.usage.input)} ↓{formatTokens(m.usage.output)}
                </div>
              )}
            </div>
          </div>
        ))}
      </div>

      {error && (
        <p className="mt-2 text-sm text-red-600">{error}</p>
      )}

      <div className="mt-3">
        <PendingApprovals moduleId={moduleId} />
      </div>

      {attachments.length > 0 && (
        <div className="mb-2 flex flex-wrap gap-2" aria-label="Attachments">
          {attachments.map((a) => (
            <span
              key={a.id}
              className="inline-flex items-center gap-1 rounded-full border border-slate-300 bg-slate-100 px-2.5 py-1 text-xs text-slate-700 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200"
            >
              <span className="max-w-[16rem] truncate">{a.fileName}</span>
              <span className="text-slate-400">{formatBytes(a.sizeBytes)}</span>
              <button
                type="button"
                aria-label={`Remove ${a.fileName}`}
                className="ml-0.5 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                onClick={() => setAttachments((prev) => prev.filter((x) => x.id !== a.id))}
              >
                ×
              </button>
            </span>
          ))}
        </div>
      )}

      <form
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          send();
        }}
      >
        <input
          ref={fileInputRef}
          type="file"
          multiple
          aria-label="Attach file"
          className="hidden"
          onChange={(e) => void attachFiles(e.target.files)}
        />
        <button
          type="button"
          aria-label="Attach a file"
          title="Attach a file"
          disabled={uploading || status === "streaming"}
          onClick={() => fileInputRef.current?.click()}
          className="rounded-md border border-slate-300 bg-white px-3 py-2 text-slate-500 hover:bg-slate-100 hover:text-slate-700 disabled:opacity-50 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-300 dark:hover:bg-slate-700"
        >
          {uploading ? (
            <span className="text-xs">…</span>
          ) : (
            <svg
              aria-hidden="true"
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="m21.44 11.05-9.19 9.19a6 6 0 0 1-8.49-8.49l8.57-8.57A4 4 0 1 1 18 8.84l-8.59 8.57a2 2 0 0 1-2.83-2.83l8.49-8.48" />
            </svg>
          )}
        </button>
        <input
          aria-label="Message"
          className="flex-1 rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100"
          placeholder="Type a message…"
          value={input}
          onChange={(e) => setInput(e.target.value)}
        />
        {status === "streaming" ? (
          <button
            type="button"
            onClick={stop}
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
          >
            Stop
          </button>
        ) : (
          <button
            type="submit"
            className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-500"
          >
            Send
          </button>
        )}
      </form>
    </div>
  );
}
