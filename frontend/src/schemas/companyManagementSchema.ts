import { z } from "zod";

const rucSchema = z
  .string()
  .trim()
  .length(13, "El RUC debe tener 13 caracteres.");

export const companyManagementFormSchema = z.object({
  taxId: rucSchema,
  legalName: z
    .string()
    .trim()
    .min(1, "La razón social es obligatoria.")
    .max(200),
  tradeName: z.string().trim().max(200).optional().or(z.literal("")),
  isActive: z.boolean().optional(),
});

export type CompanyManagementFormValues = z.infer<
  typeof companyManagementFormSchema
>;
