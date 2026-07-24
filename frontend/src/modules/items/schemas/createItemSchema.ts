import { z } from 'zod';

// ── Sections as sub-schemas ────────────────────────────────────────────────

// NOTA: no existen campos de cuenta contable (vatAccountId/purchaseVatAccountId/
// exciseAccountId) ni sriServiceCode — no forman parte del contrato.
const taxConfigSchema = z.object({
  saleVatCode:     z.string().max(10).nullable().optional(),
  purchaseVatCode: z.string().max(10).nullable().optional(),
  exciseTaxCode:   z.string().max(10).nullable().optional(),
});

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

// ── Identificación: código de barras (obligatorio) y código de proveedor (opcional) ──

const barcodeSchema = z.object({
  code:        z.string().trim().min(1, 'items.validation.barcodeCodeRequired').max(100, 'items.validation.barcodeCodeMax'),
  barcodeType: z.string().trim().min(1, 'items.validation.barcodeTypeRequired'),
  isPrimary:   z.boolean().default(false),
});

const barcodesArraySchema = z.array(barcodeSchema)
  .min(1, 'items.validation.barcodesRequired')
  .refine(
    (list) => list.filter((b) => b.isPrimary).length === 1,
    { message: 'items.validation.barcodePrimaryRequired' }
  )
  .refine(
    (list) => new Set(list.map((b) => b.code.trim().toUpperCase())).size === list.length,
    { message: 'items.validation.barcodeDuplicate' }
  );

const supplierCodeSchema = z.object({
  // El proveedor es obligatorio por fila (Fase 2) — no existe código de proveedor sin proveedor.
  supplierId: z.string().uuid('items.validation.supplierRequired'),
  code:       z.string().trim().min(1, 'items.validation.supplierCodeRequired').max(100, 'items.validation.supplierCodeMax'),
  isPrimary:  z.boolean().default(false),
});

const supplierCodesArraySchema = z.array(supplierCodeSchema)
  .default([])
  .refine(
    (list) => list.filter((s) => s.isPrimary).length <= 1,
    { message: 'items.validation.supplierCodePrimaryMax' }
  );

// ── Full create schema ─────────────────────────────────────────────────────

export const createItemSchema = z.object({
  // Identity
  sku:            z.string().trim().min(1, 'items.validation.skuRequired').max(50, 'items.validation.skuMax')
                    .regex(/^[A-Za-z0-9\-_.]+$/, 'items.validation.skuFormat'),
  shortName:      z.string().trim().min(1, 'items.validation.shortNameRequired').max(50, 'items.validation.shortNameMax'),
  description:    z.string().trim().min(1, 'items.validation.descriptionRequired').max(254, 'items.validation.descriptionMax'),
  observations:   z.string().max(500).nullable().optional(),
  itemTypeId:     z.string().uuid('items.validation.itemTypeRequired'),
  defaultUomCode: z.string().trim().min(1, 'items.validation.uomRequired').max(10),

  // Classification (obligatoria en creación)
  categoryNodeId: z.string().uuid('items.validation.categoryNodeRequired'),
  brandId:        z.string().uuid('items.validation.brandRequired'),

  // Identificación
  barcodes:       barcodesArraySchema,
  supplierCodes:  supplierCodesArraySchema,

  // Config sections
  taxConfig:   taxConfigSchema,
  saleConfig:  saleConfigSchema,
  stockConfig: stockConfigSchema,

  // BaseSalePrice (SSOT, ADR-021) — único campo de precio del ítem en todo el sistema.
  // Opcional en creación: un ítem puede nacer sin PVP definido.
  baseSalePrice: z.number().min(0, 'items.validation.priceMin').nullable().optional(),
});

export const updateItemSchema = createItemSchema
  // itemTypeId es inmutable post-creación — UpdateItemCommand no lo acepta;
  // se omite aquí para que Zod lo excluya del payload real de actualización
  // (el <select> sigue mostrando el valor, solo deshabilitado, vía GeneralTab).
  .omit({ barcodes: true, supplierCodes: true, itemTypeId: true })
  .extend({
    // SKU es editable (Fase 1): único por tenant, excluyendo el propio ítem — la
    // unicidad se valida en el backend (UpdateItemCommandValidator + índice único BD).
    sku:            z.string().trim().min(1, 'items.validation.skuRequired').max(50, 'items.validation.skuMax')
                      .regex(/^[A-Za-z0-9\-_.]+$/, 'items.validation.skuFormat'),
    categoryNodeId: z.string().uuid().nullable().optional(),
    brandId:        z.string().uuid().nullable().optional(),
    // BaseSalePrice es obligatorio en Update — refleja UpdateItemCommandValidator.NotNull().
    // Evita que el formulario envíe un valor ausente que el backend interpretaría
    // como "borrar el precio".
    baseSalePrice: z.number({
      required_error: 'items.validation.priceRequired',
      invalid_type_error: 'items.validation.priceRequired',
    }).min(0, 'items.validation.priceMin'),
  });

export type CreateItemFormValues = z.output<typeof createItemSchema>;
export type UpdateItemFormValues = z.output<typeof updateItemSchema>;

export const defaultCreateItemValues: CreateItemFormValues = {
  sku: '',
  shortName: '',
  description: '',
  observations: null,
  itemTypeId: '',
  defaultUomCode: '',
  categoryNodeId: '',
  brandId: '',
  barcodes: [
    { code: '', barcodeType: '', isPrimary: true },
  ],
  supplierCodes: [],
  taxConfig: {
    saleVatCode: null,
    purchaseVatCode: null,
    exciseTaxCode: null,
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
  baseSalePrice: null,
};
