/* eslint-disable react-refresh/only-export-components */
/**
 * i18n: es / en / **qu** donde **qu** = Kichwa de Cañar (Ecuador); ver `dictionaries.ts`.
 */
import React, { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { dictionaries, defaultLocale, safeGetStoredLocale, storageKey, type Locale } from './dictionaries';

type TParams = Record<string, string | number>;

type TFunction = (key: string, fallbackOrParams?: string | TParams) => string;

type I18nContextValue = {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  t: TFunction;
};

const I18nContext = createContext<I18nContextValue | null>(null);

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>(() => safeGetStoredLocale());

  const setLocale = useCallback((next: Locale) => {
    setLocaleState(next);
    localStorage.setItem(storageKey, next);
  }, []);

  const t = useCallback<TFunction>(
    (key, fallbackOrParams) => {
      const dict = dictionaries[locale] ?? dictionaries[defaultLocale];
      let text = dict[key] ?? (typeof fallbackOrParams === 'string' ? fallbackOrParams : undefined) ?? key;
      if (fallbackOrParams && typeof fallbackOrParams === 'object') {
        for (const [param, value] of Object.entries(fallbackOrParams)) {
          text = text.replaceAll(`{{${param}}}`, String(value));
        }
      }
      return text;
    },
    [locale],
  );

  const value = useMemo<I18nContextValue>(() => ({ locale, setLocale, t }), [locale, setLocale, t]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error('useI18n must be used within I18nProvider');
  return ctx;
}

