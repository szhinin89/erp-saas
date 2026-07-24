import { z } from 'zod';
import { passwordComplexitySchema } from './passwordComplexity';

/** Fase H — mismo patrón de confirmación de contraseña que resetWithTokenSchema.ts. */
export const completePasswordResetFormSchema = z
  .object({
    newPassword: passwordComplexitySchema,
    confirmPassword: z.string().min(1, 'Confirma la nueva contraseña.'),
  })
  .refine((d) => d.newPassword === d.confirmPassword, {
    message: 'Las contraseñas no coinciden.',
    path: ['confirmPassword'],
  });

export type CompletePasswordResetFormValues = z.infer<typeof completePasswordResetFormSchema>;
