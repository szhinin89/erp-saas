import { z } from 'zod';

/** Alta / edición de línea de producto (solo código y nombre). */
export const catalogLineCodeNameSchema = z.object({
  code: z.string().min(1, 'Ingresa el código.'),
  name: z.string().min(1, 'Ingresa el nombre.'),
});

export type CatalogLineCodeNameValues = z.infer<typeof catalogLineCodeNameSchema>;
