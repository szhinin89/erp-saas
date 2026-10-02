import { z } from "zod";

export const systemProviderSettingsFormSchema = z
  .object({
    ruc: z
      .string()
      .trim()
      .length(13, "El RUC del proveedor tecnológico debe tener 13 dígitos.")
      .regex(/^[0-9]+$/, "El RUC del proveedor tecnológico debe ser numérico.")
      .optional()
      .or(z.literal("")),
    legalName: z
      .string()
      .trim()
      .max(300, "La razón social del proveedor tecnológico no puede superar 300 caracteres.")
      .optional()
      .or(z.literal("")),
    ciiuCode: z
      .string()
      .trim()
      .max(20, "El código CIIU no puede superar 20 caracteres.")
      .optional()
      .or(z.literal("")),
    effectiveDate: z.string().trim().optional().or(z.literal("")),
    enabled: z.boolean().optional(),
  })
  // ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D7): habilitar exige RUC y fecha de vigencia (fecha
  // desde la cual el RUC Proveedor es obligatorio en los comprobantes). Espejo de UX: el backend
  // (validador + dominio) es la autoridad y responde 422 con el error asociado al campo.
  .superRefine((values, ctx) => {
    if (!values.enabled) return;
    if (!values.ruc?.trim()) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["ruc"],
        message: "El RUC del proveedor tecnológico es obligatorio para habilitar la configuración.",
      });
    }
    if (!values.effectiveDate?.trim()) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["effectiveDate"],
        message:
          "La fecha de vigencia es obligatoria para habilitar: desde esa fecha el RUC del proveedor se incluye en los comprobantes electrónicos.",
      });
    }
    if (!values.legalName?.trim() || !values.ciiuCode?.trim()) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["enabled"],
        message:
          "No se puede habilitar la configuración global del proveedor tecnológico sin RUC, razón social y CIIU completos.",
      });
    }
  });

export type SystemProviderSettingsFormValues = z.infer<
  typeof systemProviderSettingsFormSchema
>;
