import { z } from 'zod';
import { EMISSION_TYPE_ELECTRONIC, EMISSION_TYPE_PHYSICAL } from '../api/emissionPointsService';

export const emissionPointsPageSchema = z.object({
  establishmentId: z.string().min(1, 'Selecciona un establecimiento.'),
  code: z
    .string()
    .min(1, 'Ingresa el código.')
    .max(3, 'Máximo 3 dígitos.')
    .regex(/^\d{1,3}$/, 'Solo dígitos numéricos (001-999).'),
  name: z.string().optional().or(z.literal('')),
  emissionType: z.enum([EMISSION_TYPE_ELECTRONIC, EMISSION_TYPE_PHYSICAL], {
    errorMap: () => ({ message: 'Selecciona el tipo de emisión.' }),
  }),
  isDefault: z.boolean(),
});

export type EmissionPointsPageFormValues = z.infer<typeof emissionPointsPageSchema>;

export function emptyEmissionPointsPageForm(): EmissionPointsPageFormValues {
  return {
    establishmentId: '',
    code: '',
    name: '',
    emissionType: EMISSION_TYPE_ELECTRONIC,
    isDefault: false,
  };
}
