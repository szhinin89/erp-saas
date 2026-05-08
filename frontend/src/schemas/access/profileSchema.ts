import { z } from 'zod';

export const profileCreateSchema = z.object({
  name: z.string().min(1, 'Ingresa el nombre del perfil.'),
  description: z.string(),
});

export type ProfileCreateFormValues = z.infer<typeof profileCreateSchema>;
