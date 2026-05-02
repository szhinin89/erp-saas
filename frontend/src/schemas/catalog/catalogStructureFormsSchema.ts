import { z } from 'zod';

/** Alta / edición de línea de producto (solo código y nombre). */
export const catalogLineCodeNameSchema = z.object({
  code: z.string().min(1, 'El código es obligatorio'),
  name: z.string().min(1, 'El nombre es obligatorio'),
});

export type CatalogLineCodeNameValues = z.infer<typeof catalogLineCodeNameSchema>;
