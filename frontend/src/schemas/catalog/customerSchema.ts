import { z } from 'zod';

export const customerFormSchema = z.object({
  identificationType: z.string().min(1, 'Seleccione un tipo de identificación'),
  identificationNumber: z.string().min(1, 'El número de identificación es obligatorio'),
  legalName: z.string().min(1, 'La razón social es obligatoria'),
  tradeName: z.string(),
  addressLine: z.string(),
  phone: z.string(),
  email: z.union([z.literal(''), z.string().email('El correo no es válido')]),
  notes: z.string(),
  isActive: z.boolean(),
});

export type CustomerFormValues = z.infer<typeof customerFormSchema>;
