import type { MessageItem, MessageType } from "../lib/messages";
import type { NoticeSeverity, NoticeVM } from "./noticeTypes";

/** Puente entre lib/messages y ZH-NOTICE — solo consume el tipo público, nunca el store interno
 * (messages/_internal/* está bloqueado por ESLint no-restricted-imports). */
export function messageTypeToSeverity(type: MessageType): NoticeSeverity {
  return type === "error" ? "danger" : type;
}

export function messageToNotice(item: MessageItem): NoticeVM {
  return {
    severity: messageTypeToSeverity(item.type),
    intent: "status",
    source: item.type === "error" ? "api-error" : "frontend-state",
    label: item.message,
  };
}
