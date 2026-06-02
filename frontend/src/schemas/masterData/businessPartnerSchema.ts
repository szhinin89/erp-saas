import { z } from 'zod';
import { isValidCedula, isValidRuc } from '../../lib/validators/documentValidators';

// Códigos SRI de sri_id_type
const SRI_RUC             = '04';
const SRI_CI              = '05';
const SRI_PASSPORT        = '06';
const SRI_CONSUMIDOR      = '07';
const SRI_EXTERIOR        = '08';
const SRI_PLACA           = '09';

const VALID_SRI_ID_TYPES = [
  SRI_RUC, SRI_CI, SRI_PASSPORT, SRI_CONSUMIDOR, SRI_EXTERIOR, SRI_PLACA,
] as const;

function identificationRefinement(data: { identificationType: string; identificationNumber: string }) {
  const { identificationType, identificationNumber } = data;
  const n = identificationNumber.trim();
  if (identificationType === SRI_RUC) return isValidRuc(n);
  if (identificationType === SRI_CI)  return isValidCedula(n);
  return n.length > 0;
}

const identificationErrorMap: Record<string, string> = {
  [SRI_RUC]:        'RUC inválido (13 dígitos, módulo 10 en los primeros 10).',
  [SRI_CI]:         'Cédula inválida (10 dígitos, dígito verificador mod 10).',
  [SRI_PASSPORT]:   'El número de pasaporte es requerido.',
  [SRI_CONSUMIDOR]: 'El número de identificación es requerido.',
  [SRI_EXTERIOR]:   'El número de identificación del exterior es requerido.',
  [SRI_PLACA]:      'La placa es requerida.',
};

export const businessPartnerSchema = z
  .object({
    identificationType:   z.string().min(1, 'Seleccione un tipo.').refine(
      (v) => (VALID_SRI_ID_TYPES as readonly string[]).includes(v),
      'Tipo de identificación no válido.',
    ),
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
