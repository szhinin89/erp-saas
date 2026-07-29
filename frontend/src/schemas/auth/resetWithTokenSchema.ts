import { z } from "zod";

export const resetWithTokenFormSchema = z
  .object({
    newPassword: z
      .string()
      .min(8, "La nueva contraseña debe tener al menos 8 caracteres."),
    confirmPassword: z.string().min(1, "Confirma la nueva contraseña."),
  })
  .refine((d) => d.newPassword === d.confirmPassword, {
    message: "Las contraseñas no coinciden.",
    path: ["confirmPassword"],
  });

export type ResetWithTokenFormValues = z.infer<typeof resetWithTokenFormSchema>;
