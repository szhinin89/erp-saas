/**
 * Único punto de normalización de códigos de catálogo opcionales (tributarios: ICE, IVA
 * opcional, autorización SRI, etc.). Un `<select>`/`<input>` HTML no puede representar `null`
 * — solo `""` (opción "No aplica" o campo vacío) — así que sin esto, `""`/`"   "` viajan
 * intactos al backend y se tratan como código real en vez de "sin valor". Mantiene consistencia
 * con `OptionalCode.Normalize` (ERP.Domain.Common, backend).
 */
export function normalizeOptionalCode(value: string | null | undefined): string | null {
  if (value == null) return null;
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

export function trim(s: string): string {
  return s.trim();
}

export function uppercase(s: string): string {
  return s.toUpperCase();
}

export function lowercase(s: string): string {
  return s.toLowerCase();
}

export function removeExtraSpaces(s: string): string {
  return s.replace(/\s+/g, ' ').trim();
}

export function removeInvalidChars(s: string, allowed: RegExp): string {
  return s.split('').filter((c) => allowed.test(c)).join('');
}

export function sanitizeInteger(raw: string, positiveOnly = false): string {
  const negative = !positiveOnly && raw.trimStart().startsWith('-');
  const digits = raw.replace(/[^\d]/g, '');
  return negative && digits.length > 0 ? `-${digits}` : digits;
}

export function sanitizeDecimal(raw: string, decimals: number, positiveOnly = false): string {
  const negative = !positiveOnly && raw.trimStart().startsWith('-');
  let s = raw.replace(/[^\d.]/g, '');
  const parts = s.split('.');
  if (parts.length > 1) {
    const intPart = parts[0]!;
    const decPart = parts.slice(1).join('').replace(/[^\d]/g, '').slice(0, decimals);
    s = decPart.length > 0 ? `${intPart}.${decPart}` : intPart;
  }
  return negative && s.length > 0 ? `-${s}` : s;
}

export function formatMoney(value: number, decimals = 2): string {
  return value.toFixed(decimals);
}

export function formatMoneyWithSymbol(value: number, decimals = 2, symbol = '$'): string {
  return `${symbol}${value.toFixed(decimals)}`;
}

export function parseDecimal(raw: string): number {
  const clean = raw.replace(/[^\d.-]/g, '');
  return parseFloat(clean) || 0;
}

export function sanitizePhoneLocal(raw: string, maxLength: number): string {
  return raw.replace(/[^\d]/g, '').slice(0, maxLength);
}

export function sanitizeLetters(raw: string): string {
  return raw.replace(/[^a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s'.-]/g, '');
}

export function sanitizeAlphanumeric(raw: string): string {
  return raw.replace(/[^a-zA-Z0-9]/g, '');
}
