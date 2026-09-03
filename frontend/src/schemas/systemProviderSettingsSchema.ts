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
  .refine(
    (values) =>
      !values.enabled ||
      (!!values.ruc?.trim() && !!values.legalName?.trim() && !!values.ciiuCode?.trim()),
    {
      message:
        "No se puede habilitar la configuración global del proveedor tecnológico sin RUC, razón social y CIIU completos.",
      path: ["enabled"],
    },
  );

export type SystemProviderSettingsFormValues = z.infer<
  typeof systemProviderSettingsFormSchema
>;
