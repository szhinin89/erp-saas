export type NoticeSeverity = "info" | "success" | "warning" | "danger";

export type NoticeIntent =
  | "status"
  | "actionRequired"
  | "blocking"
  | "confirmation";

export type NoticeSource =
  | "frontend-state"
  | "api-validation"
  | "api-error"
  | "domain-status";

export type NoticeVariables = Record<string, string | number>;

export interface NoticeContent {
  label: string;
  detail?: string;
}

/** Plantilla estática para claves de `noticeRegistry.ts` — análoga a `HelpContent`, admite
 * `{variable}` en `label`/`detail`. */
export interface NoticeTemplate {
  severity: NoticeSeverity;
  intent: NoticeIntent;
  label: string;
  detail?: string;
}

export interface NoticeVM {
  severity: NoticeSeverity;
  intent: NoticeIntent;
  source: NoticeSource;
  label: string;
  detail?: string;
  actionLabel?: string;
  onAction?: () => void;
}
