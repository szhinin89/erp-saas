import { z } from 'zod';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

const guidSchema = z.string().uuid();

const requiredGuidSchema = z
  .string()
  .uuid('products.validation.invalidGuid')
  .refine((value) => value !== EMPTY_GUID, 'products.validation.requiredGuid');

export const productSchema = z.object({
  // Identificación básica
  saleCode: z.string().trim().min(1, 'products.validation.saleCodeRequired').max(50, 'products.validation.saleCodeMax'),
  purchaseCode: z.string().trim().max(50, 'products.validation.purchaseCodeMax').default(''),
  barcodes: z.array(
    z.object({
      code: z.string().trim().min(1, 'products.validation.barcodeCodeRequired').max(100, 'products.validation.barcodeCodeMax'),
      type: z.number().int().min(1, 'products.validation.barcodeTypeInvalid').max(99, 'products.validation.barcodeTypeInvalid'),
    })
  ).default([]),
  shortName: z.string().trim().min(1, 'products.validation.shortNameRequired').max(50, 'products.validation.shortNameMax'),
  description: z.string().trim().min(1, 'products.validation.descriptionRequired').max(254, 'products.validation.descriptionMax'),
  observations: z.string().trim().max(500, 'products.validation.observationsMax').default(''),

  // Categorización (3 niveles)
  lineId: requiredGuidSchema,
  categoryId: requiredGuidSchema,
  subcategoryId: requiredGuidSchema,

  // Catálogos relacionados
  unitOfMeasureId: requiredGuidSchema,
  brandId: requiredGuidSchema,
  productTypeId: requiredGuidSchema,
  tariffId: requiredGuidSchema,

  // Impuestos
  appliesVatOnSale: z.boolean().default(true),
  appliesVatOnPurchase: z.boolean().default(true),
  appliesExciseTax: z.boolean().default(false),
  saleTaxId: guidSchema.or(z.literal(EMPTY_GUID)),
  purchaseTaxId: guidSchema.or(z.literal(EMPTY_GUID)),
  exciseTaxId: guidSchema.or(z.literal(EMPTY_GUID)),

  // Comportamiento de stock
  isService: z.boolean().default(false),
  tracksStock: z.boolean().default(true),
  tracksLot: z.boolean().default(false),
  tracksSeries: z.boolean().default(false),
  hasRecipe: z.boolean().default(false),
  stockWithDecimal: z.boolean().default(false),
  saleWithDecimal: z.boolean().default(false),
  maxItemDiscountPercent: z.number().min(0).max(100).default(0),

  // Canales de venta
  availableOnWeb: z.boolean().default(true),
  availableOnMobile: z.boolean().default(true),
  isEcommerceActive: z.boolean().default(false),
  isFavorite: z.boolean().default(false),
  isForSale: z.boolean().default(true),

  // Variantes
  baseColor: z.string().trim().max(50).default(''),
  hasMultipleColors: z.boolean().default(false),
  hasSizes: z.boolean().default(false),

  // Aranceles
  handlesTariff: z.boolean().default(false),
});

export type ProductFormValues = z.infer<typeof productSchema>;

export const defaultProductValues: ProductFormValues = {
  // Identificación básica
  saleCode: '',
  purchaseCode: '',
  shortName: '',
  description: '',
  observations: '',

  // Categorización
  lineId: EMPTY_GUID,
  categoryId: EMPTY_GUID,
  subcategoryId: EMPTY_GUID,

  // Catálogos
  unitOfMeasureId: EMPTY_GUID,
  brandId: EMPTY_GUID,
  productTypeId: EMPTY_GUID,
  tariffId: EMPTY_GUID,

  // Impuestos
  appliesVatOnSale: true,
  appliesVatOnPurchase: true,
  appliesExciseTax: false,
  saleTaxId: EMPTY_GUID,
  purchaseTaxId: EMPTY_GUID,
  exciseTaxId: EMPTY_GUID,

  // Comportamiento de stock
  isService: false,
  tracksStock: true,
  tracksLot: false,
  tracksSeries: false,
  hasRecipe: false,
  stockWithDecimal: false,
  saleWithDecimal: false,
  maxItemDiscountPercent: 0,

  // Canales de venta
  availableOnWeb: true,
  availableOnMobile: true,
  isEcommerceActive: false,
  isFavorite: false,
  isForSale: true,

  // Variantes
  baseColor: '',
  hasMultipleColors: false,
  hasSizes: false,
  barcodes: [],

  // Aranceles
  handlesTariff: false,
};

export function toOptionalGuid(value: string): string | null {
  return value === EMPTY_GUID ? null : value;
}
