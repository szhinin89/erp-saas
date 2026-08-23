import type { NoticeVariables } from "./noticeTypes";

/** Reemplaza placeholders `{varName}` en un texto de aviso por los valores dados. Variables sin
 * resolver se dejan tal cual (visible en dev, nunca falla en runtime). */
export function interpolateNotice(text: string, vars?: NoticeVariables): string {
  if (!vars) return text;
  return text.replace(/\{(\w+)\}/g, (match, key: string) =>
    key in vars ? String(vars[key]) : match,
  );
}
