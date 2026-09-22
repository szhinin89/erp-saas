import { z } from "zod";

export const companyOperationSchema = z.object({
  languageCode: z.enum(["es", "en"]),
});

export type CompanyOperationValues = z.infer<typeof companyOperationSchema>;

export const defaultCompanyOperationValues: CompanyOperationValues = {
  languageCode: "es",
};
