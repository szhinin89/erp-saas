import { z } from 'zod';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

const guidSchema = z.string().uuid();

const requiredGuidSchema = z
  .string()
  .uuid('products.validation.invalidGuid')
  .refine((value) => value !== EMPTY_GUID, 'products.validation.requiredGuid');

export const productSchema = z.object({
  saleCode: z.string().trim().min(1, 'products.validation.saleCodeRequired').max(50, 'products.validation.saleCodeMax'),
  shortName: z.string().trim().min(1, 'products.validation.shortNameRequired').max(200, 'products.validation.shortNameMax'),
  description: z.string().trim().min(1, 'products.validation.descriptionRequired').max(500, 'products.validation.descriptionMax'),
  lineId: requiredGuidSchema,
  categoryId: requiredGuidSchema,
  subcategoryId: requiredGuidSchema,
  unitOfMeasureId: requiredGuidSchema,
  brandId: requiredGuidSchema,
  productTypeId: requiredGuidSchema,
  tariffId: requiredGuidSchema,
  saleTaxId: guidSchema.or(z.literal(EMPTY_GUID)),
  purchaseTaxId: guidSchema.or(z.literal(EMPTY_GUID)),
});

export type ProductFormValues = z.infer<typeof productSchema>;

export const defaultProductValues: ProductFormValues = {
  saleCode: '',
  shortName: '',
  description: '',
  lineId: EMPTY_GUID,
  categoryId: EMPTY_GUID,
  subcategoryId: EMPTY_GUID,
  unitOfMeasureId: EMPTY_GUID,
  brandId: EMPTY_GUID,
  productTypeId: EMPTY_GUID,
  tariffId: EMPTY_GUID,
  saleTaxId: EMPTY_GUID,
  purchaseTaxId: EMPTY_GUID,
};

export function toOptionalGuid(value: string): string | null {
  return value === EMPTY_GUID ? null : value;
}
