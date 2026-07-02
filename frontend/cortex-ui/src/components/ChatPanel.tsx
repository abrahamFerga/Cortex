import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import type { HubConnection } from "@microsoft/signalr";
import {
  createAgentConnection,
  type AgentStreamEvent,
} from "../lib/signalr";
import { api, uploadFile, type StoredFileInfo } from "../lib/api";
import { parseAttachmentRefs, withAttachmentRefs } from "../lib/attachments";
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

/** Grows the composer with its content, capped at max-h-48 (192px) where it starts scrolling. */
function autoGrow(el: HTMLTextAreaElement | null) {
  if (!el) return;
  el.style.height = "auto";
  el.style.height = `${Math.min(el.scrollHeight, 192)}px`;
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
  const [failedText, setFailedText] = useState<string | null>(null);
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const connectionRef = useRef<HubConnection | null>(null);
  const conversationIdRef = useRef<string | undefined>(undefined);
  const subscriptionRef = useRef<{ dispose: () => void } | null>(null);
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  // Whether the user is at (or near) the bottom of the transcript — auto-scroll only then.
  const pinnedRef = useRef(true);
  const prevMessageCountRef = useRef(0);
  const copyTimerRef = useRef<number | undefined>(undefined);

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

  // Keep the message list scrolled to the bottom — but only while the user is pinned there, so
  // scrolling up to read isn't yanked back by streaming tokens. A send (message count grew) always
  // snaps to the bottom to show the new turn.
  useEffect(() => {
    const grew = messages.length > prevMessageCountRef.current;
    prevMessageCountRef.current = messages.length;
    if (pinnedRef.current || grew) {
      scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight });
    }
  }, [messages]);

  // Resize the composer whenever its value changes (typing, and the clear after a send).
  useEffect(() => {
    autoGrow(textareaRef.current);
  }, [input]);

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

  // Copies a message's body (attachment refs stripped) and flips the button label for a moment.
  async function copyMessage(id: string, body: string) {
    try {
      await navigator.clipboard.writeText(body);
      setCopiedId(id);
      window.clearTimeout(copyTimerRef.current);
      copyTimerRef.current = window.setTimeout(() => setCopiedId(null), 1500);
    } catch {
      // Clipboard access can be blocked (insecure context / permissions) — fail quietly.
    }
  }

  // A turn failed before the assistant produced anything: drop the dead placeholder bubble and keep
  // the sent text around so the error banner can offer a Retry.
  function failTurn(assistantId: string, sentText: string, message: string) {
    setMessages((prev) => prev.filter((m) => !(m.id === assistantId && m.text === "")));
    setFailedText(sentText);
    setError(message);
  }

  function send(explicit?: string) {
    const typed = (explicit ?? input).trim();
    if ((!typed && attachments.length === 0) || status === "streaming" || uploading || !connectionRef.current) {
      return;
    }

    const text = withAttachmentRefs(typed, attachments);

    setInput("");
    setAttachments([]);
    sendText(text);
  }

  function sendText(text: string) {
    const connection = connectionRef.current;
    if (!connection || status === "streaming") {
      return;
    }

    setError(null);
    setFailedText(null);

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
    // On a Retry the failed user turn is still the last bubble — don't render it twice.
    setMessages((prev) => {
      const last = prev[prev.length - 1];
      const alreadyShown = last?.role === "user" && last.text === text;
      return alreadyShown ? [...prev, assistantMessage] : [...prev, userMessage, assistantMessage];
    });
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
            failTurn(assistantId, text, event.error ?? "Unknown stream error");
            break;
        }
      },
      complete: () => {
        subscriptionRef.current = null;
        setStatus("idle");
      },
      error: (e: unknown) => {
        subscriptionRef.current = null;
        failTurn(assistantId, text, String(e));
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
            className="focus-ring rounded-md border border-slate-300 px-2 py-1 text-xs font-medium text-slate-600 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-800"
          >
            New chat
          </button>
        </div>
      </div>

      <div
        ref={scrollRef}
        className="flex-1 space-y-4 overflow-y-auto pr-2"
        onScroll={(e) => {
          const el = e.currentTarget;
          pinnedRef.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80;
        }}
      >
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
                    className="focus-ring rounded-full border border-slate-300 px-3 py-1 text-xs text-slate-600 hover:border-brand-400 hover:text-brand-600 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-600 dark:text-slate-300 dark:hover:border-brand-500 dark:hover:text-brand-300"
                  >
                    {prompt}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
        {messages.map((m) => {
          const { body, files } = parseAttachmentRefs(m.text);
          return (
            <div
              key={m.id}
              className={m.role === "user" ? "group text-right" : "group text-left"}
            >
              <div
                className={
                  m.role === "user"
                    ? "inline-block max-w-[80%] break-words rounded-lg bg-brand-600 px-3 py-2 text-left text-sm text-white"
                    : "inline-block max-w-[80%] break-words rounded-lg bg-slate-100 px-3 py-2 text-sm text-slate-900 dark:bg-slate-800 dark:text-slate-100"
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
                  body ? (
                    <div className="min-w-0 text-sm leading-relaxed [overflow-wrap:anywhere]">
                      <Markdown>{body}</Markdown>
                    </div>
                  ) : (
                    <span
                      role="status"
                      aria-label="Assistant is responding"
                      className="inline-flex items-center gap-1"
                    >
                      <span className="inline-block h-1.5 w-1.5 animate-bounce rounded-full bg-slate-400 [animation-delay:-0.3s] motion-reduce:animate-none" />
                      <span className="inline-block h-1.5 w-1.5 animate-bounce rounded-full bg-slate-400 [animation-delay:-0.15s] motion-reduce:animate-none" />
                      <span className="inline-block h-1.5 w-1.5 animate-bounce rounded-full bg-slate-400 [animation-delay:0s] motion-reduce:animate-none" />
                    </span>
                  )
                ) : (
                  <span className="whitespace-pre-wrap">{body}</span>
                )}
                {files.length > 0 && (
                  <div className="mt-1.5 flex flex-wrap gap-1.5">
                    {files.map((f) => (
                      <a
                        key={f.id}
                        href={api.files.downloadUrl(f.id)}
                        download
                        target="_blank"
                        rel="noreferrer"
                        className={
                          m.role === "user"
                            ? "focus-ring inline-flex items-center gap-1 rounded-full border border-white/20 bg-white/10 px-2.5 py-1 text-xs text-white"
                            : "focus-ring inline-flex items-center gap-1 rounded-full border border-slate-300 bg-slate-100 px-2.5 py-1 text-xs text-slate-700 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200"
                        }
                      >
                        <span className="max-w-[16rem] truncate">{f.fileName}</span>
                      </a>
                    ))}
                  </div>
                )}
                {m.usage && m.usage.total > 0 && (
                  <div className="mt-1 text-[11px] text-slate-400 dark:text-slate-500">
                    {formatTokens(m.usage.total)} tokens · ↑{formatTokens(m.usage.input)} ↓{formatTokens(m.usage.output)}
                  </div>
                )}
              </div>
              {m.text && (
                <span aria-live="polite">
                  <button
                    type="button"
                    aria-label="Copy message"
                    onClick={() => void copyMessage(m.id, body)}
                    className="focus-ring mx-1 rounded px-1.5 py-0.5 align-bottom text-[11px] font-medium text-slate-400 opacity-0 transition hover:text-slate-600 focus-visible:opacity-100 group-hover:opacity-100 dark:hover:text-slate-300"
                  >
                    {copiedId === m.id ? "Copied" : "Copy"}
                  </button>
                </span>
              )}
            </div>
          );
        })}
      </div>

      {error && (
        <div className="mt-2 flex items-center gap-2">
          <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
          {failedText && (
            <button
              type="button"
              onClick={() => {
                const text = failedText;
                setError(null);
                setFailedText(null);
                sendText(text);
              }}
              className="focus-ring rounded-md border border-slate-300 px-2 py-1 text-xs font-medium text-slate-600 hover:bg-slate-100 dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-800"
            >
              Retry
            </button>
          )}
        </div>
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
                className="focus-ring ml-0.5 rounded text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                onClick={() => setAttachments((prev) => prev.filter((x) => x.id !== a.id))}
              >
                ×
              </button>
            </span>
          ))}
        </div>
      )}

      <form
        className="flex items-end gap-2"
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
          className="focus-ring rounded-md border border-slate-300 bg-white px-3 py-2 text-slate-500 hover:bg-slate-100 hover:text-slate-700 disabled:opacity-50 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-300 dark:hover:bg-slate-700"
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
        <textarea
          ref={textareaRef}
          rows={1}
          aria-label="Message"
          className="max-h-48 flex-1 resize-none overflow-y-auto rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100"
          placeholder="Type a message…"
          value={input}
          onChange={(e) => {
            setInput(e.target.value);
            autoGrow(e.currentTarget);
          }}
          onKeyDown={(e) => {
            // Enter sends; Shift+Enter falls through to insert a newline. IME composition
            // (isComposing) must not send — Enter there just confirms the composed text.
            if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
              e.preventDefault();
              send();
            }
          }}
        />
        {status === "streaming" ? (
          <button
            type="button"
            onClick={stop}
            className="focus-ring rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
          >
            Stop
          </button>
        ) : (
          <button
            type="submit"
            className="focus-ring rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-500"
          >
            Send
          </button>
        )}
      </form>
    </div>
  );
}
