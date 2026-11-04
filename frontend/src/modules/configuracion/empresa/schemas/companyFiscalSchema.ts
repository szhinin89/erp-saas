import { z } from "zod";

/**
 * Pestaña "Fiscal" (nivel frontend de form-validation.md). Formatos de la Ficha Técnica SRI 2.34 y
 * de los XSD oficiales; el backend aplica las mismas reglas (Company + FluentValidation).
 * Cada designación por resolución es un par número + "vigente desde": ambos vacíos = no designada.
 */
const SPECIAL_TAXPAYER_NO = /^[A-Za-z0-9]{3,13}$/;
const RETENTION_AGENT_NO_MAX = 8;
const LARGE_TAXPAYER_NO_MAX = 300;

/** Anexo 21: "número de la resolución, omitiendo los ceros a la izquierda". */
export const normalizeRetentionAgentNo = (value: string): string => {
  const trimmed = value.trim();
  if (!/^\d+$/.test(trimmed)) return trimmed;
  const withoutZeros = trimmed.replace(/^0+/, "");
  return withoutZeros === "" ? trimmed : withoutZeros;
};

export const companyFiscalSchema = z
  .object({
    taxRegimeCode: z.string().optional(),
    isAccountingReq: z.boolean(),
    specialTaxpayerNo: z
      .string()
      .optional()
      .refine((v) => !v?.trim() || SPECIAL_TAXPAYER_NO.test(v.trim()), {
        message: "settings.company.validation.specialTaxpayer",
      }),
    isForeignTrade: z.boolean(),
    withholdsRenta: z.boolean(),
    withholdsVat: z.boolean(),
    retentionAgentResolutionNo: z
      .string()
      .optional()
      .refine(
        (v) => {
          if (!v?.trim()) return true;
          const n = normalizeRetentionAgentNo(v);
          return /^[1-9]\d*$/.test(n) && n.length <= RETENTION_AGENT_NO_MAX;
        },
        { message: "settings.company.validation.retentionAgent" },
      ),
    retentionAgentEffectiveFrom: z.string().optional(),
    largeTaxpayerResolutionNo: z
      .string()
      .max(LARGE_TAXPAYER_NO_MAX, "settings.company.validation.max300")
      .optional(),
    largeTaxpayerEffectiveFrom: z.string().optional(),
  })
  .superRefine((values, ctx) => {
    const pairs = [
      ["retentionAgentResolutionNo", "retentionAgentEffectiveFrom"],
      ["largeTaxpayerResolutionNo", "largeTaxpayerEffectiveFrom"],
    ] as const;
    for (const [numberField, dateField] of pairs) {
      const hasNumber = !!values[numberField]?.trim();
      const hasDate = !!values[dateField];
      if (hasNumber && !hasDate)
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: [dateField],
          message: "settings.company.validation.effectiveFrom",
        });
      if (hasDate && !hasNumber)
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: [numberField],
          message: "settings.company.validation.resolution",
        });
    }
  });

export type CompanyFiscalValues = z.infer<typeof companyFiscalSchema>;

export const defaultCompanyFiscalValues: CompanyFiscalValues = {
  taxRegimeCode: "",
  isAccountingReq: false,
  specialTaxpayerNo: "",
  isForeignTrade: false,
  withholdsRenta: false,
  withholdsVat: false,
  retentionAgentResolutionNo: "",
  retentionAgentEffectiveFrom: "",
  largeTaxpayerResolutionNo: "",
  largeTaxpayerEffectiveFrom: "",
};
