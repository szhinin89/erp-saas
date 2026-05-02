import { z } from 'zod';

export const catalogCategoryFormSchema = z.object({
  code: z.string().min(1, 'El código es obligatorio'),
  name: z.string().min(1, 'El nombre es obligatorio'),
  lineId: z.string().min(1, 'Seleccione una línea de producto'),
});

export type CatalogCategoryFormValues = z.infer<typeof catalogCategoryFormSchema>;

export const catalogSubcategoryFormSchema = z.object({
  code: z.string().min(1, 'El código es obligatorio'),
  name: z.string().min(1, 'El nombre es obligatorio'),
  lineId: z.string().min(1, 'Seleccione una línea de producto'),
  categoryId: z.string().min(1, 'Seleccione una categoría'),
});

export type CatalogSubcategoryFormValues = z.infer<typeof catalogSubcategoryFormSchema>;
