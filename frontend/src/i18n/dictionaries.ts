import es from "./locales/es.json";

export type Locale = "es" | "en";
export type Dictionary = Record<string, string>;

/**
 * `es` (idioma por defecto y fuente de verdad) viaja en el bundle inicial; `en` se descarga bajo
 * demanda (P-01) y se copia en este mismo objeto al cargarse, así que el contrato no cambia.
 */
export const dictionaries: Record<Locale, Dictionary> = {
  es,
  en: {},
};

export const defaultLocale: Locale = "es";
export const storageKey = "zh.erp.locale";

const loaders: Record<Locale, () => Promise<Dictionary>> = {
  es: () => Promise.resolve(es),
  en: () => import("./locales/en.json").then((m) => m.default),
};
const loaded = new Set<Locale>([defaultLocale]);
const pending = new Map<Locale, Promise<void>>();

/** Solo tests: sustituye la descarga de un idioma (red lenta o fallida) sin simular el JSON. */
export function setLocaleLoaderForTests(locale: Locale, loader: () => Promise<Dictionary>): void {
  loaders[locale] = loader;
}

export function isLocaleLoaded(locale: Locale): boolean {
  return loaded.has(locale);
}

/**
 * Diccionario efectivo de un idioma: el suyo si ya se descargó; si no (p. ej. falló la descarga de
 * `en`), el del idioma por defecto. Toda lectura de textos pasa por aquí para que la UI y los
 * mensajes fuera de React muestren siempre el mismo idioma.
 */
export function getDictionary(locale: Locale): Dictionary {
  return isLocaleLoaded(locale) ? dictionaries[locale] : dictionaries[defaultLocale];
}

/** Descarga el diccionario una sola vez; si falla, el siguiente intento vuelve a descargarlo. */
export function ensureLocaleLoaded(locale: Locale): Promise<void> {
  if (loaded.has(locale)) return Promise.resolve();
  let request = pending.get(locale);
  if (!request) {
    request = loaders[locale]()
      .then((dict) => {
        Object.assign(dictionaries[locale], dict);
        loaded.add(locale);
      })
      .finally(() => pending.delete(locale));
    pending.set(locale, request);
  }
  return request;
}

export function safeGetStoredLocale(): Locale {
  const raw = localStorage.getItem(storageKey);
  if (raw === "es" || raw === "en") return raw;
  return defaultLocale;
}
