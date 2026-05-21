import { z } from 'zod';

export const branchFormSchema = z.object({
  name: z.string().min(1, 'Ingresa el nombre de la sucursal.'),
  address: z.string().min(1, 'Ingresa la dirección.'),
  branchType: z.string().optional(),
  reference: z.string().optional(),
  phones: z.string().optional(),
  email: z
    .string()
    .trim()
    .optional()
    .or(z.literal(''))
    .refine((v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v), 'Correo inválido'),
  managerName: z.string().optional(),
  countryId: z.string().optional(),
  provinceId: z.string().optional(),
  cantonId: z.string().optional(),
  parishId: z.string().optional(),
  latitude: z.string().optional(),
  longitude: z.string().optional(),
  storageCapacity: z.coerce.number().min(0).optional().nullable(),
  dailySalesGoal: z.coerce.number().min(0).optional().nullable(),
  rechargeOption: z.string().optional(),
  isActive: z.boolean(),
  isMainBranch: z.boolean(),
});

export type BranchFormValues = z.infer<typeof branchFormSchema>;
