import { z } from "zod";

export const forgotPasswordEmailSchema = z.object({
  email: z
    .string()
    .min(1, "Ingresa el correo electrónico.")
    .email("Ingresa un correo electrónico válido."),
});

export type ForgotPasswordFormValues = z.infer<
  typeof forgotPasswordEmailSchema
>;
