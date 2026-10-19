/* eslint-disable react-refresh/only-export-components */
/** i18n: español / inglés. */
import React, {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
} from "react";
import {
  defaultLocale,
  ensureLocaleLoaded,
  getDictionary,
  isLocaleLoaded,
  safeGetStoredLocale,
  storageKey,
  type Locale,
} from "./dictionaries";

type TParams = Record<string, string | number>;

type TFunction = (key: string, fallbackOrParams?: string | TParams) => string;

type I18nContextValue = {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  t: TFunction;
};

const I18nContext = createContext<I18nContextValue | null>(null);

function translate(
  locale: Locale,
  key: string,
  fallbackOrParams?: string | TParams,
): string {
  const dict = getDictionary(locale);
  let text =
    dict[key] ??
    (typeof fallbackOrParams === "string" ? fallbackOrParams : undefined) ??
    key;
  if (fallbackOrParams && typeof fallbackOrParams === "object") {
    for (const [param, value] of Object.entries(fallbackOrParams)) {
      text = text.replaceAll(`{{${param}}}`, String(value));
    }
  }
  return text;
}

export function I18nProvider({ children }: { children: React.ReactNode }) {
  // main.tsx descarga el idioma guardado antes del primer render (un solo intento). Si falló, se
  // muestra el idioma por defecto (también en formatApiError, vía getDictionary) y se conserva la
  // preferencia guardada: es la elección del usuario, no se sobrescribe por un fallo de red.
  const [locale, setLocaleState] = useState<Locale>(() => {
    const stored = safeGetStoredLocale();
    return isLocaleLoaded(stored) ? stored : defaultLocale;
  });
  // Solo la última selección se aplica: una descarga lenta no pisa un cambio posterior.
  const lastRequest = useRef(0);

  const setLocale = useCallback((next: Locale) => {
    const request = ++lastRequest.current;
    const apply = () => {
      if (request !== lastRequest.current) return;
      setLocaleState(next);
      localStorage.setItem(storageKey, next);
    };
    if (isLocaleLoaded(next)) {
      apply();
      return;
    }
    // Si la descarga falla se conserva el idioma actual y no se guarda la preferencia.
    ensureLocaleLoaded(next).then(apply, () => {});
  }, []);

  const t = useCallback<TFunction>(
    (key, fallbackOrParams) => translate(locale, key, fallbackOrParams),
    [locale],
  );

  const value = useMemo<I18nContextValue>(
    () => ({ locale, setLocale, t }),
    [locale, setLocale, t],
  );

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

const fallbackI18n: I18nContextValue = {
  locale: defaultLocale,
  setLocale: () => {},
  t: (key, fallbackOrParams) => translate(defaultLocale, key, fallbackOrParams),
};

/**
 * Igual que `useI18n`, pero tolera la ausencia de `I18nProvider` SOLO en tests (`MODE === "test"`),
 * donde muchos árboles de componentes se montan sin provider. Fuera de tests lanza igual que
 * `useI18n`: el provider vive en `main.tsx`, así que su ausencia en la app real es un bug y no debe
 * enmascararse con el idioma por defecto.
 */
export function useOptionalI18n(): I18nContextValue {
  const ctx = useContext(I18nContext);
  if (ctx) return ctx;
  if (import.meta.env.MODE !== "test") {
    throw new Error("useOptionalI18n must be used within I18nProvider (solo tests pueden omitirlo)");
  }
  return fallbackI18n;
}

export function useI18n() {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error("useI18n must be used within I18nProvider");
  return ctx;
}
