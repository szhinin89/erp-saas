import { z } from 'zod';

export const variantAttributeSchema = z.object({
  attributeDefinitionId: z.string().uuid('items.validation.attrDefRequired'),
  value: z.string().trim().min(1, 'items.validation.attrValueRequired').max(200),
});

export const addVariantSchema = z.object({
  skuOverride: z.string().max(80).nullable().optional(),
  sortOrder:   z.number().int().min(0).default(0),
  attributes:  z.array(variantAttributeSchema)
                 .min(1, 'items.validation.attrRequired'),
});

export type AddVariantFormValues = z.output<typeof addVariantSchema>;

export const defaultAddVariantValues: AddVariantFormValues = {
  skuOverride: null,
  sortOrder: 0,
  attributes: [],
};

// ── Variant Matrix cell ─────────────────────────────────────────────────────

export interface VariantMatrixCell {
  axisValues: Record<string, string>; // { "COLOR": "Red", "SIZE": "L" }
  skuOverride: string;
  barcodeCode: string;
  isActive: boolean;
}
