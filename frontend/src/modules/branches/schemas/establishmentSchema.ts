import { z } from 'zod';

export const establishmentFormSchema = z.object({
  code: z
    .string()
    .min(1, 'Ingresa el código.')
    .max(3, 'Máximo 3 dígitos.')
    .regex(/^\d+$/, 'Solo dígitos numéricos.'),
  name: z.string().min(1, 'Ingresa el nombre del establecimiento.'),
  address: z.string().min(1, 'Ingresa la dirección.'),
  phone: z.string().optional().or(z.literal('')),
  isMain: z.boolean(),
});

export type EstablishmentFormValues = z.infer<typeof establishmentFormSchema>;

export function emptyEstablishmentForm(): EstablishmentFormValues {
  return { code: '', name: '', address: '', phone: '', isMain: false };
}
