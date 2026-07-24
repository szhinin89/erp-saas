import { z } from 'zod';

const optionalEmail = (message: string) =>
  z
    .string()
    .trim()
    .optional()
    .or(z.literal(''))
    .refine((v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v), message);

export const branchFormSchema = z.object({
  // Identificación
  name: z.string().min(1, 'Ingresa el nombre de la sucursal.'),
  description: z.string().optional(),
  isMainBranch: z.boolean(),

  // Dirección
  address: z.string().min(1, 'Ingresa la dirección.'),
  countryId: z.string().optional(),
  provinceId: z.string().optional(),
  cantonId: z.string().optional(),
  parishId: z.string().optional(),
  reference: z.string().optional(),
  postalCode: z.string().optional(),
  latitude: z.string().optional(),
  longitude: z.string().optional(),

  // Contacto
  phone: z.string().optional(),
  secondaryPhone: z.string().optional(),
  email: optionalEmail('Correo inválido'),
  website: z.string().optional(),

  // Responsable
  managerName: z.string().optional(),
  managerPosition: z.string().optional(),
  managerEmail: optionalEmail('Correo inválido'),
  managerPhone: z.string().optional(),

  // Operación
  isActive: z.boolean(),
  openingDate: z.string().optional(),
  internalNotes: z.string().optional(),
});

export type BranchFormValues = z.infer<typeof branchFormSchema>;
