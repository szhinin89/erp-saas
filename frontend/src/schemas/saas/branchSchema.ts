import { z } from 'zod';

export const branchFormSchema = z.object({
  name: z.string().min(1, 'El nombre de la sucursal es obligatorio'),
  address: z.string().min(1, 'La dirección es obligatoria'),
  reference: z.string(),
  phones: z.string(),
  countryId: z.string(),
  provinceId: z.string(),
  cantonId: z.string(),
  parishId: z.string(),
  latitude: z.string(),
  longitude: z.string(),
  rechargeOption: z.string(),
  isActive: z.boolean(),
  isMainBranch: z.boolean(),
});

export type BranchFormValues = z.infer<typeof branchFormSchema>;
