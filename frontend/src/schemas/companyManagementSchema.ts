import { z } from 'zod';

const rucSchema = z
  .string()
  .trim()
  .length(13, 'El RUC debe tener 13 caracteres.');

export const companyManagementFormSchema = z.object({
  taxId: rucSchema,
  legalName: z.string().trim().min(1, 'La razón social es obligatoria.').max(200),
  tradeName: z.string().trim().max(200).optional().or(z.literal('')),
  mainAddress: z.string().trim().min(1, 'La dirección es obligatoria.').max(500),
  phone: z.string().trim().max(40).optional().or(z.literal('')),
  email: z.string().trim().email('Email inválido.').max(120).optional().or(z.literal('')),
  countryCode: z.string().trim().length(3),
  timezone: z.string().trim().min(1).max(64),
  currencyCode: z.string().trim().length(3),
  logoUrl: z.string().trim().url('URL inválida.').max(500).optional().or(z.literal('')),
  brandingJson: z.string().trim().max(8000).optional().or(z.literal('')),
  isActive: z.boolean().optional(),
});

export type CompanyManagementFormValues = z.infer<typeof companyManagementFormSchema>;
