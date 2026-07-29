import { z } from "zod";
import { passwordComplexitySchema } from "../auth/passwordComplexity";

/** Mismo patrón de confirmación que completePasswordResetSchema.ts — misma fuente de complejidad. */
export const assignTemporaryPasswordSchema = z
  .object({
    temporaryPassword: passwordComplexitySchema,
    confirmPassword: z.string().min(1, "Confirma la contraseña temporal."),
  })
  .refine((d) => d.temporaryPassword === d.confirmPassword, {
    message: "Las contraseñas no coinciden.",
    path: ["confirmPassword"],
  });

export type AssignTemporaryPasswordFormValues = z.infer<
  typeof assignTemporaryPasswordSchema
>;
