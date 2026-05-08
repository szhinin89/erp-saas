import { z } from 'zod';

export const catalogCategoryFormSchema = z.object({
  code: z.string().min(1, 'Ingresa el código.'),
  name: z.string().min(1, 'Ingresa el nombre.'),
  lineId: z.string().min(1, 'Selecciona una línea de producto.'),
});

export type CatalogCategoryFormValues = z.infer<typeof catalogCategoryFormSchema>;

export const catalogSubcategoryFormSchema = z.object({
  code: z.string().min(1, 'Ingresa el código.'),
  name: z.string().min(1, 'Ingresa el nombre.'),
  lineId: z.string().min(1, 'Selecciona una línea de producto.'),
  categoryId: z.string().min(1, 'Selecciona una categoría.'),
});

export type CatalogSubcategoryFormValues = z.infer<typeof catalogSubcategoryFormSchema>;
