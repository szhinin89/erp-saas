import { z } from 'zod';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

const optionalGuid = z.string().uuid().nullable().optional();

const requiredGuid = z
  .string()
  .uuid('items.validation.invalidGuid')
  .refine((v) => v !== EMPTY_GUID, 'items.validation.guidRequired');

// ── Sections as sub-schemas ────────────────────────────────────────────────

const taxConfigSchema = z.object({
  appliesVatOnSale:     z.boolean().default(true),
  saleVatCode:         z.string().max(10).nullable().optional(),
  vatAccountId:        optionalGuid,
  appliesVatOnPurchase: z.boolean().default(true),
  purchaseVatCode:     z.string().max(10).nullable().optional(),
  purchaseVatAccountId: optionalGuid,
  appliesExciseTax:    z.boolean().default(false),
  exciseTaxCode:       z.string().max(10).nullable().optional(),
  exciseAccountId:     optionalGuid,
  sriServiceCode:      z.string().max(5).nullable().optional(),
})
  .refine(
    (d) => !d.appliesVatOnSale || !!d.saleVatCode,
    { message: 'items.validation.saleVatCodeRequired', path: ['saleVatCode'] }
  )
  .refine(
    (d) => !d.appliesVatOnPurchase || !!d.purchaseVatCode,
    { message: 'items.validation.purchaseVatCodeRequired', path: ['purchaseVatCode'] }
  )
  .refine(
    (d) => !d.appliesExciseTax || !!d.exciseTaxCode,
    { message: 'items.validation.exciseTaxCodeRequired', path: ['exciseTaxCode'] }
  );

const saleConfigSchema = z.object({
  isForSale:          z.boolean().default(true),
  maxDiscountPercent: z.number().min(0).max(100).nullable().optional(),
  isAvailableOnWeb:   z.boolean().default(false),
  isAvailableOnPOS:   z.boolean().default(false),
  isAvailableOnMobile: z.boolean().default(false),
  isEcommerceActive:  z.boolean().default(false),
});

const stockConfigSchema = z.object({
  tracksStock:    z.boolean().default(true),
  tracksLot:      z.boolean().default(false),
  tracksSeries:   z.boolean().default(false),
  allowDecimalQty:  z.boolean().default(false),
  allowDecimalSale: z.boolean().default(false),
  minStockQty: z.number().min(0).nullable().optional(),
  maxStockQty: z.number().min(0).nullable().optional(),
})
  .refine(
    (d) => !d.minStockQty || !d.maxStockQty || d.minStockQty <= d.maxStockQty,
    { message: 'items.validation.stockQtyRange', path: ['maxStockQty'] }
  );

// ── Full create schema ─────────────────────────────────────────────────────

export const createItemSchema = z.object({
  // Identity
  sku:            z.string().trim().min(1, 'items.validation.skuRequired').max(50, 'items.validation.skuMax')
                    .regex(/^[A-Za-z0-9\-_.]+$/, 'items.validation.skuFormat'),
  shortName:      z.string().trim().min(1, 'items.validation.shortNameRequired').max(50, 'items.validation.shortNameMax'),
  description:    z.string().trim().min(1, 'items.validation.descriptionRequired').max(254, 'items.validation.descriptionMax'),
  purchaseCode:   z.string().max(50).nullable().optional(),
  observations:   z.string().max(500).nullable().optional(),
  itemType:       z.enum(['Physical', 'Service', 'Digital', 'Kit', 'Bundle'], {
                    errorMap: () => ({ message: 'items.validation.itemTypeRequired' }),
                  }),
  defaultUomCode: z.string().trim().min(1, 'items.validation.uomRequired').max(10),

  // Classification
  categoryNodeId: z.string().uuid().nullable().optional(),
  brandId:        z.string().uuid().nullable().optional(),

  // Config sections
  taxConfig:   taxConfigSchema,
  saleConfig:  saleConfigSchema,
  stockConfig: stockConfigSchema,
});

export const updateItemSchema = createItemSchema.omit({ sku: true });

export type CreateItemFormValues = z.output<typeof createItemSchema>;
export type UpdateItemFormValues = z.output<typeof updateItemSchema>;

export const defaultCreateItemValues: CreateItemFormValues = {
  sku: '',
  shortName: '',
  description: '',
  purchaseCode: null,
  observations: null,
  itemType: 'Physical',
  defaultUomCode: '',
  categoryNodeId: null,
  brandId: null,
  taxConfig: {
    appliesVatOnSale: true,
    saleVatCode: null,
    vatAccountId: null,
    appliesVatOnPurchase: true,
    purchaseVatCode: null,
    purchaseVatAccountId: null,
    appliesExciseTax: false,
    exciseTaxCode: null,
    exciseAccountId: null,
    sriServiceCode: null,
  },
  saleConfig: {
    isForSale: true,
    maxDiscountPercent: null,
    isAvailableOnWeb: false,
    isAvailableOnPOS: false,
    isAvailableOnMobile: false,
    isEcommerceActive: false,
  },
  stockConfig: {
    tracksStock: true,
    tracksLot: false,
    tracksSeries: false,
    allowDecimalQty: false,
    allowDecimalSale: false,
    minStockQty: null,
    maxStockQty: null,
  },
};
