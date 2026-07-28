import { z } from 'zod';

export const createItemFromLineSchema = z.object({
  sku: z.string().trim()
    .min(1, 'El SKU es obligatorio.')
    .max(50, 'El SKU no puede exceder 50 caracteres.')
    .regex(/^[A-Za-z0-9\-_.]+$/, 'El SKU solo puede contener letras, números, guiones, puntos y guiones bajos.'),
  shortName: z.string().trim().min(1, 'El nombre corto es obligatorio.').max(50, 'El nombre corto no puede exceder 50 caracteres.'),
  description: z.string().trim().min(1, 'La descripción es obligatoria.').max(254, 'La descripción no puede exceder 254 caracteres.'),
  itemTypeId: z.string().min(1, 'El tipo de ítem es obligatorio.'),
  categoryNodeId: z.string().min(1, 'La categoría es obligatoria.'),
  brandId: z.string().min(1, 'La marca es obligatoria.'),
  defaultUomCode: z.string().min(1, 'La unidad de medida es obligatoria.'),
  barcodeType: z.string().min(1, 'El tipo de código de barras es obligatorio.'),
});

export type CreateItemFromLineFormValues = z.infer<typeof createItemFromLineSchema>;
