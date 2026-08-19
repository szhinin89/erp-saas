import { z } from "zod";

const HEX_COLOR_REGEX = /^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$/;

export const companyBrandingSchema = z.object({
  primaryColor: z
    .string()
    .regex(HEX_COLOR_REGEX, "Color hexadecimal inválido")
    .optional()
    .or(z.literal("")),
  secondaryColor: z
    .string()
    .regex(HEX_COLOR_REGEX, "Color hexadecimal inválido")
    .optional()
    .or(z.literal("")),
  slogan: z.string().max(200, "Máximo 200 caracteres").optional(),
  documentFooterText: z
    .string()
    .max(500, "Máximo 500 caracteres")
    .optional(),
});

export type CompanyBrandingValues = z.infer<typeof companyBrandingSchema>;

export const defaultCompanyBrandingValues: CompanyBrandingValues = {
  primaryColor: "",
  secondaryColor: "",
  slogan: "",
  documentFooterText: "",
};
