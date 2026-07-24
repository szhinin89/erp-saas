import { z } from 'zod';

export const profileCreateSchema = z.object({
  name: z.string().min(1, 'Enter the profile name.'),
  description: z.string().optional(),
  isActive: z.boolean(),
});

export type ProfileCreateFormValues = z.infer<typeof profileCreateSchema>;
