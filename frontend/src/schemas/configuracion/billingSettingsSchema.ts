import { z } from 'zod';

export const billingSettingsSchema = z.object({
  legalName:          z.string().min(1, 'La razón social es obligatoria').max(150),
  tradeName:          z.string().min(1, 'El nombre comercial es obligatorio').max(150),
  ruc:                z.string().regex(/^\d{13}$/, 'El RUC debe tener exactamente 13 dígitos'),
  mainAddress:        z.string().min(1, 'La dirección es obligatoria').max(250),
  phone:              z.string().min(1, 'El teléfono es obligatorio').max(25),
  email:              z.string().email('Correo inválido').optional().or(z.literal('')),
  requiresAccounting: z.boolean(),
  specialTaxpayer:    z.string().max(50).optional(),
  footerText:         z.string().max(500).optional(),
  receiptWidth:       z.number().int().refine((v) => v === 58 || v === 80, 'El ancho debe ser 58 o 80 mm'),
  logoBase64:         z.string().max(100000).optional().nullable(),
});

export type BillingSettingsValues = z.infer<typeof billingSettingsSchema>;
