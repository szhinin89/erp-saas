import { z } from 'zod';

/**
 * Formulario catálogo completo (identificación tipada, razón social, notas).
 * Usado por CustomerFormFields y flujos legacy de catálogo/facturación.
 */
export const customerCatalogFormSchema = z.object({
  identificationType: z.string().min(1, 'Selecciona el tipo de identificación.'),
  identificationNumber: z.string().min(1, 'Ingresa el número de identificación.'),
  legalName: z.string().min(1, 'Ingresa la razón social o nombre.'),
  tradeName: z.string(),
  addressLine: z.string(),
  phone: z.string(),
  email: z.union([z.literal(''), z.string().email('Ingresa un correo electrónico válido.')]),
  notes: z.string(),
  isActive: z.boolean(),
});

export type CustomerCatalogFormValues = z.infer<typeof customerCatalogFormSchema>;

/** @deprecated Alias legacy — preferir `CustomerCatalogFormValues`. */
export type CustomerFormValues = CustomerCatalogFormValues;

/** @deprecated Alias legacy — preferir `customerCatalogFormSchema`. */
export const customerFormSchema = customerCatalogFormSchema;
