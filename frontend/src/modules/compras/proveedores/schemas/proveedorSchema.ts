import { z } from 'zod';

export const proveedorSchema = z.object({
  ruc: z.string().min(1, 'proveedores.validation.rucRequired'),
  razonSocial: z.string().min(1, 'proveedores.validation.razonSocialRequired'),
  nombreComercial: z.string().optional(),
  contactoPrincipal: z.string().optional(),
  email: z
    .string()
    .trim()
    .optional()
    .or(z.literal(''))
    .refine(
      (v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v),
      'proveedores.validation.invalidEmail'
    ),
  phone: z.string().optional(),
  address: z.string().optional(),
  website: z.string().optional(),
  category: z.string().optional(),
  status: z.enum(['activo', 'pendiente', 'inactivo']).default('activo'),
});

export type ProveedorFormValues = z.infer<typeof proveedorSchema>;

export const defaultProveedorValues: ProveedorFormValues = {
  ruc: '',
  razonSocial: '',
  nombreComercial: '',
  contactoPrincipal: '',
  email: '',
  phone: '',
  address: '',
  website: '',
  category: '',
  status: 'activo',
};
