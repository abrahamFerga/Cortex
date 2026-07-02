import type { StoredFileInfo } from "./api";

/**
 * Appends attachment references to the outgoing message. The convention is plain text on purpose:
 * it survives every channel (web, AG-UI, SignalR, WhatsApp) and the agent's document tools take the
 * file id directly (read_document, ocr_document).
 */
export function withAttachmentRefs(text: string, attachments: StoredFileInfo[]): string {
  if (attachments.length === 0) return text;
  const refs = attachments.map((a) => `- ${a.fileName} (file id: ${a.id})`).join("\n");
  const body = text || "Please look at the attached file(s).";
  return `${body}\n\n[Attached files]\n${refs}`;
}
