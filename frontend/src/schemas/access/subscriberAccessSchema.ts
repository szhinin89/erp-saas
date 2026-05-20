import { z } from 'zod';

export const subscriberAccessUpsertSchema = z.object({
  email: z.string().min(1, 'Ingresa el correo electrónico.').email('Ingresa un correo electrónico válido.'),
  role: z.enum(['User', 'Admin']),
  profileId: z.string(),
  firstName: z.string(),
  lastName: z.string(),
  password: z.string(),
});

export type SubscriberAccessFormValues = z.infer<typeof subscriberAccessUpsertSchema>;
