import { z } from "zod";

const HEX_COLOR_REGEX = /^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$/;

export const companyBrandingSchema = z.object({
  primaryColor: z
    .string()
    .regex(HEX_COLOR_REGEX, "settings.company.validation.hex")
    .optional()
    .or(z.literal("")),
  secondaryColor: z
    .string()
    .regex(HEX_COLOR_REGEX, "settings.company.validation.hex")
    .optional()
    .or(z.literal("")),
  slogan: z.string().max(200, "settings.company.validation.max200").optional(),
  documentFooterText: z
    .string()
    .max(500, "settings.company.validation.max500")
    .optional(),
});

export type CompanyBrandingValues = z.infer<typeof companyBrandingSchema>;

export const defaultCompanyBrandingValues: CompanyBrandingValues = {
  primaryColor: "",
  secondaryColor: "",
  slogan: "",
  documentFooterText: "",
};
