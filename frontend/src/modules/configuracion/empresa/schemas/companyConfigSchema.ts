import { z } from "zod";

export const companyConfigSchema = z.object({
  corporateEmail: z
    .string()
    .email("settings.company.validation.email")
    .optional()
    .or(z.literal("")),
  phone: z.string().max(20, "settings.company.validation.max20").optional(),
  website: z.string().url("settings.company.validation.url").optional().or(z.literal("")),
  currencyCode: z.string().min(1),
  timezone: z.string().min(1),
  legalRepName: z.string().max(200, "settings.company.validation.max200").optional(),
  legalRepPosition: z.string().max(100, "settings.company.validation.max100").optional(),
  legalRepIdNumber: z.string().max(20, "settings.company.validation.max20").optional(),
  legalRepEmail: z
    .string()
    .email("settings.company.validation.email")
    .optional()
    .or(z.literal("")),
  legalRepPhone: z.string().max(20, "settings.company.validation.max20").optional(),
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
