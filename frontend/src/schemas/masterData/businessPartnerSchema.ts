import { z } from 'zod';
import { isValidCedula, isValidRuc } from '../../lib/validators/documentValidators';

function identificationRefinement(data: { identificationType: string; identificationNumber: string }) {
  const { identificationType, identificationNumber } = data;
  const n = identificationNumber.trim();
  if (identificationType === 'RUC') return isValidRuc(n);
  if (identificationType === 'CI')  return isValidCedula(n);
  return n.length > 0;
}

const identificationErrorMap: Record<string, string> = {
  RUC:      'RUC inválido (13 dígitos, módulo 10 en los primeros 10).',
  CI:       'Cédula inválida (10 dígitos, dígito verificador mod 10).',
  PASSPORT: 'El número de pasaporte es requerido.',
  OTHER:    'El número de identificación es requerido.',
};

export const businessPartnerSchema = z
  .object({
    identificationType:   z.string().min(1, 'Seleccione un tipo.'),
    identificationNumber: z.string().min(1, 'El número es requerido.'),
    legalName:            z.string().min(2, 'La razón social debe tener al menos 2 caracteres.'),
    tradeName:            z.string().optional(),
    email: z
      .string()
      .optional()
      .refine(
        (v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v),
        'Correo electrónico inválido.',
      ),
    phone: z
      .string()
      .optional()
      .refine(
        (v) => !v || /^[0-9\s+\-()]{7,20}$/.test(v),
        'Teléfono inválido (7–20 dígitos).',
      ),
  })
  .superRefine((data, ctx) => {
    if (!identificationRefinement(data)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['identificationNumber'],
        message: identificationErrorMap[data.identificationType] ?? 'Identificación inválida.',
      });
    }
  });

export type BusinessPartnerFormValues = z.infer<typeof businessPartnerSchema>;
