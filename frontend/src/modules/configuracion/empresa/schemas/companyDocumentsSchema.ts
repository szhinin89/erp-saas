import { z } from "zod";

export const companyDocumentsSchema = z.object({
  extraLegend: z.string().max(500, "Máximo 500 caracteres").optional(),
});

export type CompanyDocumentsValues = z.infer<typeof companyDocumentsSchema>;

export const defaultCompanyDocumentsValues: CompanyDocumentsValues = {
  extraLegend: "",
};
