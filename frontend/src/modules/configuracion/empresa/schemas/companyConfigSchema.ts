import { z } from 'zod';

export const companyConfigSchema = z.object({
  companyName:       z.string().min(1, 'El nombre de la empresa es requerido'),
  ruc:               z.string().optional(),
  shortName:         z.string().optional(),
  currency:          z.string().min(1),
  language:          z.string().min(1),
  timezone:          z.string().min(1),
  invoicePrefix:     z.string().optional(),
  initialFolio:      z.coerce.number().min(1).optional(),
  defaultCreditDays: z.coerce.number().min(0).optional(),
});

export type CompanyConfigValues = z.infer<typeof companyConfigSchema>;

export const defaultCompanyConfigValues: CompanyConfigValues = {
  companyName:       '',
  ruc:               '',
  shortName:         '',
  currency:          'USD',
  language:          'es',
  timezone:          'America/Guayaquil',
  invoicePrefix:     'FAC-',
  initialFolio:      1001,
  defaultCreditDays: 30,
};
