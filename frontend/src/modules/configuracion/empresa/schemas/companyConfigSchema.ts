import { z } from "zod";

export const companyConfigSchema = z.object({
  corporateEmail: z
    .string()
    .email("Correo electrónico inválido")
    .optional()
    .or(z.literal("")),
  phone: z.string().max(20, "Máximo 20 caracteres").optional(),
  website: z.string().url("URL inválida").optional().or(z.literal("")),
  currencyCode: z.string().min(1),
  timezone: z.string().min(1),
  legalRepName: z.string().max(200, "Máximo 200 caracteres").optional(),
  legalRepPosition: z.string().max(100, "Máximo 100 caracteres").optional(),
  legalRepIdNumber: z.string().max(20, "Máximo 20 caracteres").optional(),
  legalRepEmail: z
    .string()
    .email("Correo electrónico inválido")
    .optional()
    .or(z.literal("")),
  legalRepPhone: z.string().max(20, "Máximo 20 caracteres").optional(),
});

export type CompanyConfigValues = z.infer<typeof companyConfigSchema>;

export const defaultCompanyConfigValues: CompanyConfigValues = {
  corporateEmail: "",
  phone: "",
  website: "",
  currencyCode: "USD",
  timezone: "America/Guayaquil",
  legalRepName: "",
  legalRepPosition: "",
  legalRepIdNumber: "",
  legalRepEmail: "",
  legalRepPhone: "",
};
