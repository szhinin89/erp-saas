import { z } from 'zod';

export const companyConfigSchema = z.object({
  subscriberName:  z.string().min(1, 'El nombre de la cuenta es requerido'),
  preferredLanguage: z.string().min(1),
  legalName:       z.string().min(1, 'La razón social es requerida'),
  tradeName:       z.string().optional(),
  taxId:           z.string().optional(),
  mainAddress:     z.string().optional(),
  currency:        z.string().min(1),
  timezone:        z.string().min(1),
});

export type CompanyConfigValues = z.infer<typeof companyConfigSchema>;

export const defaultCompanyConfigValues: CompanyConfigValues = {
  subscriberName:    '',
  preferredLanguage: 'es',
  legalName:         '',
  tradeName:         '',
  taxId:             '',
  mainAddress:       '',
  currency:          'USD',
  timezone:          'America/Guayaquil',
};
