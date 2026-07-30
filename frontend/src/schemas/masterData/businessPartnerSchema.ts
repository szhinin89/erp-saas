import { z } from "zod";
import { validateIdentification } from "../../lib/validators/documentValidators";
export const SRI_RUC = "04";
export const SRI_CI = "05";
const SRI_PASSPORT = "06";
const SRI_CONSUMIDOR = "07";
const SRI_EXTERIOR = "08";
const SRI_PLACA = "09";

const VALID_SRI_ID_TYPES = [
  SRI_RUC,
  SRI_CI,
  SRI_PASSPORT,
  SRI_CONSUMIDOR,
  SRI_EXTERIOR,
  SRI_PLACA,
] as const;

/** RUC y CI permiten al backend inferir la naturaleza jurídica automáticamente. */
export function isLegalEntityTypeInferable(identificationType: string): boolean {
  return identificationType === SRI_RUC || identificationType === SRI_CI;
}

function identificationRefinement(data: {
  identificationType: string;
  identificationNumber: string;
}) {
  return validateIdentification(
    data.identificationType,
    data.identificationNumber.trim(),
  );
}

const identificationErrorMap: Record<string, string> = {
  [SRI_RUC]: "RUC inválido según las reglas del SRI.",
  [SRI_CI]: "Cédula inválida (10 dígitos, dígito verificador mod 10).",
  [SRI_PASSPORT]: "El número de pasaporte es requerido.",
  [SRI_CONSUMIDOR]: "El número de identificación es requerido.",
  [SRI_EXTERIOR]: "El número de identificación del exterior es requerido.",
  [SRI_PLACA]: "La placa es requerida.",
};

/**
 * Schema para identidad del BusinessPartner V2.
 *
 * ELIMINADO: email, phone, legalRepresentativeName
 *   → email y phone van en BusinessPartnerContact (POST /contacts)
 *   → representante legal va en BusinessPartnerContact con Role=Legal
 *
 *  NUEVO: legalEntityTypeCode
 * → catálogo global.legal_entity_type
 * → usado para clasificación tributaria SRI
 * → opcional cuando la identificación permite inferirlo (RUC/CI); obligatorio en el resto
 *   de los casos — el backend es la autoridad final, este schema solo replica la
 *   obligatoriedad condicional para feedback inmediato (ver isLegalEntityTypeInferable).
 *
 * refundProviderTypeCode / paymentTermId: solo obligatorios cuando role='supplier' — viven en
 * SupplierRoleConfig (backend), que exige PaymentTermId obligatorio por regla de dominio ya
 * existente. Para Customer quedan sin usar (el formulario ni los muestra).
 */
const businessPartnerBaseShape = {
  identificationType: z
    .string()
    .min(1, "Seleccione un tipo.")
    .refine(
      (v) => (VALID_SRI_ID_TYPES as readonly string[]).includes(v),
      "Tipo de identificación no válido.",
    ),
  identificationNumber: z.string().min(1, "El número es requerido."),
  // Opcional: el backend infiere la naturaleza jurídica cuando la identificación lo permite
  // (RUC/CI). Solo es obligatoria cuando no puede inferirse — ver superRefine más abajo.
  legalEntityTypeCode: z.coerce
    .number({ invalid_type_error: "Tipo de entidad legal inválido." })
    .refine((v) => v > 0, "Tipo de entidad legal inválido.")
    .optional(),
  legalName: z
    .string()
    .min(2, "La razón social debe tener al menos 2 caracteres.")
    .max(200, "La razón social no puede superar 200 caracteres."),
  tradeName: z.string().max(200).optional(),
  countryCode: z
    .string()
    .length(2, "Debe ser un código de 2 letras (ISO alpha-2).")
    .optional()
    .or(z.literal("")),
  refundProviderTypeCode: z.string().optional().or(z.literal("")),
  paymentTermId: z.string().optional().or(z.literal("")),
};

export function businessPartnerSchema(
  role: "customer" | "supplier" = "customer",
) {
  return z.object(businessPartnerBaseShape).superRefine((data, ctx) => {
    if (!identificationRefinement(data)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["identificationNumber"],
        message:
          identificationErrorMap[data.identificationType] ??
          "Identificación inválida.",
      });
    }
    if (
      !isLegalEntityTypeInferable(data.identificationType) &&
      !data.legalEntityTypeCode
    ) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["legalEntityTypeCode"],
        message:
          "El tipo de entidad legal es obligatorio para este tipo de identificación.",
      });
    }
    if (role === "supplier") {
      if (!data.refundProviderTypeCode) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["refundProviderTypeCode"],
          message: "El tipo de proveedor es obligatorio.",
        });
      }
      if (!data.paymentTermId) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["paymentTermId"],
          message: "La condición de pago es obligatoria.",
        });
      }
    }
  });
}

export type BusinessPartnerFormValues = z.infer<
  ReturnType<typeof businessPartnerSchema>
>;
