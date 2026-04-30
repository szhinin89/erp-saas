import es from './locales/es.json';
import en from './locales/en.json';
import qu from './locales/qu.json';

export type Locale = 'es' | 'en' | 'qu';
export type Dictionary = Record<string, string>;

export const dictionaries: Record<Locale, Dictionary> = {
  es,
  en,
  qu,
};

export const defaultLocale: Locale = 'es';
export const storageKey = 'zh.erp.locale';

export function safeGetStoredLocale(): Locale {
  const raw = localStorage.getItem(storageKey);
  if (raw === 'es' || raw === 'en' || raw === 'qu') return raw;
  return defaultLocale;
}

