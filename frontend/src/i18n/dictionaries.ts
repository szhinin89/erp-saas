import es from "./locales/es.json";
import en from "./locales/en.json";
/**
 * Tercer idioma de la UI: **Kichwa de Cañar, Ecuador** (no “quechua” genérico ni variantes Perú/Bolivia).
 * El código del locale sigue siendo **`qu`** y el archivo **`qu.json`** solo por compatibilidad técnica (tipo `Locale`, localStorage, selectores).
 */
import qu from "./locales/qu.json";

/** `qu` = Kichwa Cañar (Ecuador); ver comentario sobre `qu.json` arriba. */
export type Locale = "es" | "en" | "qu";
export type Dictionary = Record<string, string>;

export const dictionaries: Record<Locale, Dictionary> = {
  es,
  en,
  qu,
};

export const defaultLocale: Locale = "es";
export const storageKey = "zh.erp.locale";

export function safeGetStoredLocale(): Locale {
  const raw = localStorage.getItem(storageKey);
  if (raw === "es" || raw === "en" || raw === "qu") return raw;
  return defaultLocale;
}
