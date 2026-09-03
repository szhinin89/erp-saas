import { z } from "zod";

export const systemProviderSettingsFormSchema = z
  .object({
    ruc: z
      .string()
      .trim()
      .length(13, "El RUC debe tener 13 dígitos.")
      .regex(/^[0-9]+$/, "El RUC debe ser numérico.")
      .optional()
      .or(z.literal("")),
    legalName: z.string().trim().max(300).optional().or(z.literal("")),
    ciiuCode: z.string().trim().max(20).optional().or(z.literal("")),
    effectiveDate: z.string().trim().optional().or(z.literal("")),
    enabled: z.boolean().optional(),
  })
  .refine(
    (values) =>
      !values.enabled ||
      (!!values.ruc?.trim() && !!values.legalName?.trim() && !!values.ciiuCode?.trim()),
    {
      message:
        "No se puede habilitar el proveedor de sistema sin RUC, razón social y CIIU completos.",
      path: ["enabled"],
    },
  );

export type SystemProviderSettingsFormValues = z.infer<
  typeof systemProviderSettingsFormSchema
>;
