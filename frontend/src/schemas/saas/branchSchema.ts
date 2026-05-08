import { z } from 'zod';

export const branchFormSchema = z.object({
  name: z.string().min(1, 'Ingresa el nombre de la sucursal.'),
  address: z.string().min(1, 'Ingresa la dirección.'),
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
