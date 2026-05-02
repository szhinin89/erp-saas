import { z } from 'zod';

export const createAccountFormSchema = z.object({
  code: z.string().min(1, 'El código de cuenta es obligatorio'),
  name: z.string().min(1, 'El nombre de la cuenta es obligatorio'),
  type: z.coerce.number().int().min(0).max(4),
  nature: z.coerce.number().int().min(0).max(1),
  parentId: z.string(),
});

export type CreateAccountFormValues = z.infer<typeof createAccountFormSchema>;
