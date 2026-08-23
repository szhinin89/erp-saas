import type { HelpVariables } from "./helpTypes";

/** Reemplaza placeholders `{varName}` en un texto de ayuda por los valores dados. Variables sin resolver se dejan tal cual (visible en dev, nunca falla en runtime). */
export function interpolateHelp(text: string, vars?: HelpVariables): string {
  if (!vars) return text;
  return text.replace(/\{(\w+)\}/g, (match, key: string) =>
    key in vars ? String(vars[key]) : match,
  );
}
