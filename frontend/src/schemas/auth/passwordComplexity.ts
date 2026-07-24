import { z } from 'zod';

/**
 * Fase H — única regla de complejidad de contraseña nueva vigente en el frontend (mínimo 8
 * caracteres, al menos una mayúscula, al menos un número). Espeja PasswordComplexityRules
 * (backend, fuente de verdad de negocio) — este fragmento es solo feedback inmediato de
 * interfaz, reutilizado por todo formulario que capture una contraseña nueva de IdentityUser.
 */
export const passwordComplexitySchema = z
  .string()
  .min(8, 'La contraseña debe tener al menos 8 caracteres.')
  .regex(/[A-Z]/, 'Debe incluir al menos una mayúscula.')
  .regex(/[0-9]/, 'Debe incluir al menos un número.');
