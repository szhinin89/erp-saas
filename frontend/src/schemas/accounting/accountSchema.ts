import { z } from 'zod';

export const createAccountFormSchema = z.object({
  code: z.string().min(1, 'Ingresa el código de la cuenta.'),
  name: z.string().min(1, 'Ingresa el nombre de la cuenta.'),
  type: z.coerce.number().int().min(0).max(4),
  nature: z.coerce.number().int().min(0).max(1),
  parentId: z.string(),
});

export type CreateAccountFormValues = z.infer<typeof createAccountFormSchema>;
