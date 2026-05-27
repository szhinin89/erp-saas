import { z } from 'zod';

export const emissionPointFormSchema = z.object({
  code: z
    .string()
    .min(1, 'Ingresa el código.')
    .max(3, 'Máximo 3 dígitos.')
    .regex(/^\d+$/, 'Solo dígitos numéricos.'),
  name: z.string().optional().or(z.literal('')),
  isDefault: z.boolean(),
});

export type EmissionPointFormValues = z.infer<typeof emissionPointFormSchema>;

export function emptyEmissionPointForm(): EmissionPointFormValues {
  return { code: '', name: '', isDefault: false };
}
