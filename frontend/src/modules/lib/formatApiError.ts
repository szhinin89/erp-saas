import { isAxiosError } from "axios";
import {
  dictionaries,
  defaultLocale,
  safeGetStoredLocale,
  type Locale,
} from "../../i18n/dictionaries";

function dict(locale: Locale, key: string, fallback: string): string {
  const d = dictionaries[locale] ?? dictionaries[defaultLocale];
  return d[key] ?? fallback;
}

function currentLocale(): Locale {
  try {
    return safeGetStoredLocale();
  } catch {
    return defaultLocale;
  }
}

/**
 * Mensaje legible para la UI ante fallos de red o respuestas HTTP de la API.
 * Prioriza el cuerpo JSON del backend (`message` / `error`) y trata 502/503 como API caída.
 */
export function formatApiError(err: unknown, locale?: Locale): string {
  const loc = locale ?? currentLocale();
  const generic = dict(loc, "common.errorGeneric", "Error");
  const unreachable = dict(
    loc,
    "common.apiUnreachable",
    "Cannot reach the API. Start the backend (dotnet run in ERP.API on http://localhost:5003) and reload.",
  );

  if (isAxiosError(err)) {
    const status = err.response?.status;
    const raw = err.response?.data;
    if (raw && typeof raw === "object") {
      const o = raw as Record<string, unknown>;
      const msg = o.message ?? o.error;
      if (typeof msg === "string" && msg.trim()) return msg.trim();
    }
    if (status === 502 || status === 503 || status === 504) return unreachable;
    if (!err.response) return unreachable;
    const m = err.message?.trim();
    if (m) return m;
    return generic;
  }
  if (err instanceof Error && err.message.trim()) return err.message.trim();
  return generic;
}
