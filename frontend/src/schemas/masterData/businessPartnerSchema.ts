import { z } from 'zod';
import { isValidCedula, isValidRuc } from '../../lib/validators/documentValidators';
import { PersonTypeEnum } from '../../modules/masterData/types/businessPartner.types';

const SRI_RUC        = '04';
const SRI_CI         = '05';
const SRI_PASSPORT   = '06';
const SRI_CONSUMIDOR = '07';
const SRI_EXTERIOR   = '08';
const SRI_PLACA      = '09';

const VALID_SRI_ID_TYPES = [
  SRI_RUC, SRI_CI, SRI_PASSPORT, SRI_CONSUMIDOR, SRI_EXTERIOR, SRI_PLACA,
] as const;

function identificationRefinement(data: { identificationType: string; identificationNumber: string }) {
  const n = data.identificationNumber.trim();
  if (data.identificationType === SRI_RUC) return isValidRuc(n);
  if (data.identificationType === SRI_CI)  return isValidCedula(n);
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

/**
 * Schema para identidad del BusinessPartner V2.
 *
 * ELIMINADO: email, phone, legalRepresentativeName
 *   → email y phone van en BusinessPartnerContact (POST /contacts)
 *   → representante legal va en BusinessPartnerContact con Role=Legal
 *
 * NUEVO: personType (obligatorio)
 *   → 1=Natural, 2=Legal, 3=Government, 4=Organization
 */
export const businessPartnerSchema = z
  .object({
    identificationType: z.string().min(1, 'Seleccione un tipo.').refine(
      (v) => (VALID_SRI_ID_TYPES as readonly string[]).includes(v),
      'Tipo de identificación no válido.',
    ),
    identificationNumber: z.string().min(1, 'El número es requerido.'),
    personType: z.number({
      required_error: 'El tipo de persona es obligatorio.',
      invalid_type_error: 'Tipo de persona inválido.',
    }).refine(
      (v) => Object.values(PersonTypeEnum).includes(v as typeof PersonTypeEnum[keyof typeof PersonTypeEnum]),
      'Tipo de persona inválido.',
    ),
    legalName: z.string().min(2, 'La razón social debe tener al menos 2 caracteres.')
                         .max(200, 'La razón social no puede superar 200 caracteres.'),
    tradeName: z.string().max(200).optional(),
    countryCode: z.string().length(2, 'Debe ser un código de 2 letras (ISO alpha-2).').optional()
                           .or(z.literal('')),
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
