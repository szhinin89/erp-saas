import { z } from 'zod';

export const loginSchema = z.object({
  email: z.string().min(1, 'El correo es obligatorio').email('Introduce un correo electrónico válido'),
  password: z.string().min(1, 'La contraseña es obligatoria'),
});

export type LoginFormValues = z.infer<typeof loginSchema>;
