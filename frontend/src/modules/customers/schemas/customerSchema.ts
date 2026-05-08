import { z } from 'zod';

export const customerSchema = z.object({
  identification: z.string().min(1, 'customers.validation.identificationRequired'),
  fullName: z.string().min(1, 'customers.validation.fullNameRequired'),
  email: z
    .string()
    .trim()
    .optional()
    .or(z.literal(''))
    .refine(
      (value) => !value || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value),
      'customers.validation.invalidEmail'
    ),
  phone: z.string().optional(),
  address: z.string().optional(),
});

export type CustomerFormValues = z.infer<typeof customerSchema>;

export const defaultCustomerValues: CustomerFormValues = {
  identification: '',
  fullName: '',
  email: '',
  phone: '',
  address: '',
};
