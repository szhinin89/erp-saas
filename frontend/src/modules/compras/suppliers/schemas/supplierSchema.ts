import { z } from 'zod';

export const supplierSchema = z.object({
  taxId:            z.string().min(1, 'suppliers.validation.taxIdRequired'),
  legalName:        z.string().min(1, 'suppliers.validation.legalNameRequired'),
  tradeName:        z.string().optional(),
  primaryContact:   z.string().optional(),
  email:            z
    .string()
    .trim()
    .optional()
    .or(z.literal(''))
    .refine(
      (v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v),
      'suppliers.validation.invalidEmail',
    ),
  phone:            z.string().optional(),
  address:          z.string().optional(),
  website:          z.string().optional(),
  category:         z.string().optional(),
  status:           z.enum(['active', 'pending', 'inactive']),
});

export type SupplierFormValues = z.infer<typeof supplierSchema>;

export const defaultSupplierValues: SupplierFormValues = {
  taxId:          '',
  legalName:      '',
  tradeName:      '',
  primaryContact: '',
  email:          '',
  phone:          '',
  address:        '',
  website:        '',
  category:       '',
  status:         'active',
};
