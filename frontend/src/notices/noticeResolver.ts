import { NOTICE_REGISTRY } from "./noticeRegistry";
import { interpolateNotice } from "./noticeVariables";
import type { NoticeKeyId } from "./noticeKeys";
import type { NoticeSource, NoticeVM, NoticeVariables } from "./noticeTypes";

/** Resuelve una clave estática de noticeRegistry a un NoticeVM ya interpolado. */
export function resolveNotice(
  key: NoticeKeyId,
  source: NoticeSource,
  vars?: NoticeVariables,
): NoticeVM | null {
  const entry = NOTICE_REGISTRY[key];
  if (!entry) return null;
  return {
    severity: entry.severity,
    intent: entry.intent,
    source,
    label: interpolateNotice(entry.label, vars),
    detail: entry.detail ? interpolateNotice(entry.detail, vars) : undefined,
  };
}

/** Constructor puro para avisos cuyo contenido ya fue calculado por lógica de dominio (p. ej.
 * purchaseLineReadiness.ts) — Notice solo normaliza la forma, nunca reescribe el texto. */
export function buildNotice(input: {
  severity: NoticeVM["severity"];
  intent: NoticeVM["intent"];
  source: NoticeSource;
  label: string;
  detail?: string;
  actionLabel?: string;
  onAction?: () => void;
}): NoticeVM {
  return { ...input };
}
