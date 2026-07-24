import { z } from 'zod';

const NAV_KEYS = new Set([
  'Backspace', 'Delete', 'Tab', 'Enter',
  'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown',
  'Home', 'End',
]);

/**
 * Retorna true si la tecla debe ser permitida en un input de enteros.
 * Usar en onKeyDown del input.
 */
export function allowsIntegerKey(
  e: React.KeyboardEvent<HTMLInputElement>,
  positiveOnly = false,
): boolean {
  const { key, ctrlKey, metaKey } = e;
  if (ctrlKey || metaKey) return true;
  if (NAV_KEYS.has(key)) return true;
  if (!positiveOnly && key === '-') {
    const input = e.currentTarget;
    return input.selectionStart === 0 && !input.value.includes('-');
  }
  return /^\d$/.test(key);
}

/**
 * Retorna true si la tecla debe ser permitida en un input decimal.
 * Usar en onKeyDown del input.
 */
export function allowsDecimalKey(
  e: React.KeyboardEvent<HTMLInputElement>,
  decimals: number,
  positiveOnly = false,
): boolean {
  const { key, ctrlKey, metaKey } = e;
  if (ctrlKey || metaKey) return true;
  if (NAV_KEYS.has(key)) return true;
  if (!positiveOnly && key === '-') {
    const input = e.currentTarget;
    return input.selectionStart === 0 && !input.value.includes('-');
  }
  if ((key === '.' || key === ',') && decimals > 0) {
    return !e.currentTarget.value.includes('.');
  }
  if (/^\d$/.test(key)) {
    if (decimals === 0) return true;
    const input = e.currentTarget;
    const dotIndex = input.value.indexOf('.');
    if (dotIndex === -1) return true;
    const cursorPos = input.selectionStart ?? 0;
    const selEnd = input.selectionEnd ?? 0;
    if (cursorPos <= dotIndex) return true;
    if (selEnd > cursorPos) return true;
    return input.value.length - dotIndex - 1 < decimals;
  }
  return false;
}

// ── Zod schemas ────────────────────────────────────────────────────────────────

/** Entero. Encadenar con .min()/.max() según necesidad. */
export const zInteger = z.coerce.number().int('Debe ser un número entero.');

/** Entero ≥ 0. */
export const zPositiveInteger = z.coerce
  .number()
  .int('Debe ser un número entero.')
  .min(0, 'Debe ser mayor o igual a 0.');

/** Decimal. Encadenar con .min()/.max() según necesidad. */
export const zDecimal = z.coerce.number();

/** Moneda (≥ 0). Precisión decimal configurable en el componente. */
export const zCurrency = z.coerce.number().min(0, 'El valor no puede ser negativo.');

/** Porcentaje 0-100. */
export const zPercentage = z.coerce
  .number()
  .min(0, 'Mínimo 0%.')
  .max(100, 'Máximo 100%.');
