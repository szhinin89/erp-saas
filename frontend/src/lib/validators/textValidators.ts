import { z } from 'zod';
import { phoneConfig } from '../config/phone.config';

const NAV_KEYS = new Set([
  'Backspace', 'Delete', 'Tab', 'Enter',
  'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown',
  'Home', 'End',
]);

// ── Key filters ────────────────────────────────────────────────────────────────

/** Solo letras (incluyendo acentos, ñ), espacios y caracteres comunes en nombres. */
export function allowsLettersKey(e: React.KeyboardEvent<HTMLInputElement>): boolean {
  const { key, ctrlKey, metaKey } = e;
  if (ctrlKey || metaKey) return true;
  if (NAV_KEYS.has(key)) return true;
  return /^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s'.-]$/.test(key);
}

/** Solo letras y dígitos (sin espacios). */
export function allowsAlphanumericKey(e: React.KeyboardEvent<HTMLInputElement>): boolean {
  const { key, ctrlKey, metaKey } = e;
  if (ctrlKey || metaKey) return true;
  if (NAV_KEYS.has(key)) return true;
  return /^[a-zA-Z0-9]$/.test(key);
}

/** Solo dígitos (para teléfono local u otros campos numérico-texto). */
export function allowsPhoneDigitKey(e: React.KeyboardEvent<HTMLInputElement>): boolean {
  const { key, ctrlKey, metaKey } = e;
  if (ctrlKey || metaKey) return true;
  if (NAV_KEYS.has(key)) return true;
  return /^\d$/.test(key);
}

// ── Zod schemas ────────────────────────────────────────────────────────────────

/** Solo letras y caracteres comunes en nombres propios. */
export const zLettersOnly = z
  .string()
  .regex(/^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s'.-]*$/, 'Solo se permiten letras.');

/** Solo letras y números (sin espacios). */
export const zAlphanumeric = z
  .string()
  .regex(/^[a-zA-Z0-9]*$/, 'Solo se permiten letras y números.');

/** Código SKU: mayúsculas, números, guiones y guiones bajos. */
export const zSkuCode = z
  .string()
  .regex(/^[A-Z0-9\-_]*$/, 'SKU: solo mayúsculas, números, guiones y guiones bajos.');

/** Email obligatorio. */
export const zEmail = z.string().email('Correo electrónico inválido.');

/** Email opcional (campo vacío permitido). */
export const zEmailOptional = z
  .string()
  .optional()
  .refine(
    (v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v),
    'Correo electrónico inválido.',
  );

/** Teléfono con prefijo de país (validación de longitud según phoneConfig). */
export const zPhone = z
  .string()
  .optional()
  .refine(
    (v) => {
      if (!v) return true;
      const prefix = phoneConfig.defaultCountryCode;
      const local = v.startsWith(prefix) ? v.slice(prefix.length) : v;
      return /^\d{7,15}$/.test(local.replace(/\s/g, ''));
    },
    `Teléfono inválido. Ingrese ${phoneConfig.localLength} dígitos.`,
  );
