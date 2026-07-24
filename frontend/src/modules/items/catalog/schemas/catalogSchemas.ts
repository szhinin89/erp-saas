import { z } from 'zod';

// ── Brand ────────────────────────────────────────────────────────────────

export const brandSchema = z.object({
  code: z.string().trim().min(1, 'Ingresa el código.').max(20, 'Máximo 20 caracteres.'),
  name: z.string().trim().min(1, 'Ingresa el nombre.').max(120, 'Máximo 120 caracteres.'),
  manufacturer: z.string().max(120).nullable().optional(),
  countryOfOrigin: z.string().max(80).nullable().optional(),
});

export type BrandFormValues = z.infer<typeof brandSchema>;
export type BrandFormInput = z.input<typeof brandSchema>;
export const emptyBrandForm = (): BrandFormValues => ({ code: '', name: '', manufacturer: null, countryOfOrigin: null });

// ── Attribute Group ──────────────────────────────────────────────────────

export const attributeGroupSchema = z.object({
  code: z.string().trim().min(1, 'Ingresa el código.').max(20, 'Máximo 20 caracteres.'),
  name: z.string().trim().min(1, 'Ingresa el nombre.').max(120, 'Máximo 120 caracteres.'),
  sortOrder: z.number().min(0).default(0),
});

export type AttributeGroupFormValues = z.infer<typeof attributeGroupSchema>;
export type AttributeGroupFormInput = z.input<typeof attributeGroupSchema>;
export const emptyAttributeGroupForm = (): AttributeGroupFormValues => ({ code: '', name: '', sortOrder: 0 });

// ── Attribute Definition ─────────────────────────────────────────────────

export const attributeDefinitionSchema = z.object({
  groupId: z.string().min(1, 'Selecciona un grupo.'),
  code: z.string().trim().min(1, 'Ingresa el código.').max(20, 'Máximo 20 caracteres.'),
  name: z.string().trim().min(1, 'Ingresa el nombre.').max(120, 'Máximo 120 caracteres.'),
  dataType: z.string().min(1, 'Selecciona el tipo de dato.'),
  isVariantAxis: z.boolean().default(false),
  isRequired: z.boolean().default(false),
  sortOrder: z.number().min(0).default(0),
});

export type AttributeDefinitionFormValues = z.infer<typeof attributeDefinitionSchema>;
export type AttributeDefinitionFormInput = z.input<typeof attributeDefinitionSchema>;
export const emptyAttributeDefinitionForm = (): AttributeDefinitionFormValues => ({
  groupId: '', code: '', name: '', dataType: 'Text', isVariantAxis: false, isRequired: false, sortOrder: 0,
});
