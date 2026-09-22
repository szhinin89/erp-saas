import es from "./locales/es.json";
import en from "./locales/en.json";

export type Locale = "es" | "en";
export type Dictionary = Record<string, string>;

export const dictionaries: Record<Locale, Dictionary> = {
  es,
  en,
};

export const defaultLocale: Locale = "es";
export const storageKey = "zh.erp.locale";

export function safeGetStoredLocale(): Locale {
  const raw = localStorage.getItem(storageKey);
  if (raw === "es" || raw === "en") return raw;
  return defaultLocale;
}