import { z } from 'zod';

/** Construye un schema Zod para filas de catálogo simple (claves dinámicas). */
export function buildCatalogSimpleRowSchema(
  keys: string[],
  requiredKeys: readonly string[]
): z.ZodObject<Record<string, z.ZodTypeAny>> {
  const req = new Set(requiredKeys);
  const shape: Record<string, z.ZodTypeAny> = {};
  for (const key of keys) {
    shape[key] = req.has(key)
      ? z.string().min(1, 'Este campo es obligatorio.')
      : z.string().optional();
  }
  return z.object(shape);
}
