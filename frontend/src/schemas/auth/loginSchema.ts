import { z } from 'zod';

export const loginSchema = z.object({
  email: z.string().min(1, 'Ingresa el correo electrónico.').email('Ingresa un correo electrónico válido.'),
  password: z.string().min(1, 'Ingresa la contraseña.'),
});

export type LoginFormValues = z.infer<typeof loginSchema>;
