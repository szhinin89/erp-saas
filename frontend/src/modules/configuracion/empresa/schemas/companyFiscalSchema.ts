import { z } from "zod";

export const companyFiscalSchema = z.object({
  taxRegimeCode: z.string().optional(),
  isAccountingReq: z.boolean(),
  specialTaxpayerNo: z.string().max(200, "Máximo 200 caracteres").optional(),
  isForeignTrade: z.boolean(),
  withholdsRenta: z.boolean(),
  withholdsVat: z.boolean(),
});

export type CompanyFiscalValues = z.infer<typeof companyFiscalSchema>;

export const defaultCompanyFiscalValues: CompanyFiscalValues = {
  taxRegimeCode: "",
  isAccountingReq: false,
  specialTaxpayerNo: "",
  isForeignTrade: false,
  withholdsRenta: false,
  withholdsVat: false,
};
