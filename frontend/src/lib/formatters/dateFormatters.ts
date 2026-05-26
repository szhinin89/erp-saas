import { dateConfig } from '../config/date.config';

/**
 * Always renders ISO date string as DD/MM/YYYY regardless of browser/OS locale.
 * Returns '—' for empty, null, or invalid input.
 */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '—';
  return d.toLocaleDateString(dateConfig.locale, dateConfig.displayFormat);
}

/**
 * Returns today as YYYY-MM-DD (for input min/max/default values).
 */
export function todayIso(): string {
  return new Date().toISOString().split('T')[0]!;
}

/**
 * Returns true if the string is a valid ISO date (YYYY-MM-DD) and the date exists.
 */
export function isValidIsoDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const d = new Date(value);
  return !isNaN(d.getTime()) && d.toISOString().startsWith(value);
}
