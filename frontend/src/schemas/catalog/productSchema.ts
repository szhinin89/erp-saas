import { z } from 'zod';

/** Formulario de creación de producto (pestaña general + GUIDs de catálogo). */
export const productCreateFormSchema = z.object({
  saleCode: z.string().min(1, 'El código de venta es obligatorio'),
  purchaseCode: z.string(),
  shortName: z.string().min(1, 'El nombre corto es obligatorio'),
  description: z.string().min(1, 'La descripción es obligatoria'),
  lineId: z.string(),
  categoryId: z.string(),
  subcategoryId: z.string(),
  unitOfMeasureId: z.string(),
  brandId: z.string(),
  productTypeId: z.string(),
  tariffId: z.string(),
  saleTaxId: z.string(),
  purchaseTaxId: z.string(),
  isService: z.boolean(),
  isForSale: z.boolean(),
  availableOnWeb: z.boolean(),
  availableOnMobile: z.boolean(),
});

export type ProductCreateFormValues = z.infer<typeof productCreateFormSchema>;
