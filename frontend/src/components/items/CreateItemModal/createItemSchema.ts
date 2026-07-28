import { z } from 'zod';

export const createItemModalSchema = z.object({
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
  barcode: z.string().trim().min(1, 'El código de barras es obligatorio.').max(100, 'El código de barras no puede exceder 100 caracteres.'),
  barcodeType: z.string().min(1, 'El tipo de código de barras es obligatorio.'),
  // Precio de Venta — opcional; solo relevante cuando initialData.purchaseContext habilita la
  // sección de simulación. `null` = campo vacío (nunca se infiere un valor). No impone reglas
  // comerciales de margen — solo valida que, si se ingresa, no sea negativo.
  salePrice: z.number().nullable().optional()
    .refine(v => v === null || v === undefined || v >= 0, { message: 'El precio no puede ser negativo.' }),
  updatePrice: z.boolean().default(false),
}).refine(
  data => !data.updatePrice || (data.salePrice != null && data.salePrice > 0),
  { message: 'Ingrese un precio de venta válido para actualizar el precio del Item.', path: ['salePrice'] },
);

export type CreateItemModalFormValues = z.infer<typeof createItemModalSchema>;
