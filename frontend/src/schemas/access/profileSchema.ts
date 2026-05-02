import { z } from 'zod';

export const profileCreateSchema = z.object({
  name: z.string().min(1, 'El nombre del perfil es obligatorio'),
  description: z.string(),
});

export type ProfileCreateFormValues = z.infer<typeof profileCreateSchema>;
