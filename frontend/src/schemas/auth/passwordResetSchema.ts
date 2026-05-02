import { z } from 'zod';

const guidRegex = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

export const passwordResetFormSchema = z
  .object({
    tenantId: z
      .string()
      .min(1, 'El identificador del tenant es obligatorio')
      .regex(guidRegex, 'Debe ser un GUID de tenant válido'),
    email: z.string().min(1, 'El correo es obligatorio').email('Introduce un correo electrónico válido'),
    newPassword: z.string().min(8, 'La nueva contraseña debe tener al menos 8 caracteres'),
    confirmPassword: z.string().min(1, 'Confirma la nueva contraseña'),
  })
  .refine((d) => d.newPassword === d.confirmPassword, {
    message: 'Las contraseñas no coinciden',
    path: ['confirmPassword'],
  });

export type PasswordResetFormValues = z.infer<typeof passwordResetFormSchema>;
