import { z } from "zod";
import { MEMBERSHIP_ROLES } from "../../modules/access/users/api/membershipService";
import { passwordComplexitySchema } from "../auth/passwordComplexity";

/**
 * Validación de interfaz para el alta de un IdentityUser nuevo. Username es la identidad de
 * login (Fase G); email pasa a ser un dato de contacto opcional. Las reglas de password espejan
 * CreateSystemUserCommandValidator (backend) — feedback inmediato sin duplicar la fuente de
 * verdad de negocio, que sigue siendo el backend.
 */
export const createSystemUserSchema = z.object({
  username: z
    .string()
    .min(3, "El username debe tener al menos 3 caracteres.")
    .max(50, "Máximo 50 caracteres.")
    .regex(
      /^[a-zA-Z0-9][a-zA-Z0-9._-]{1,48}[a-zA-Z0-9]$/,
      "Solo letras, números, punto, guion o guion bajo.",
    ),
  firstName: z
    .string()
    .min(1, "El nombre es obligatorio.")
    .max(50, "Máximo 50 caracteres."),
  lastName: z
    .string()
    .min(1, "El apellido es obligatorio.")
    .max(50, "Máximo 50 caracteres."),
  email: z
    .union([
      z.string().email("El email no tiene un formato válido."),
      z.literal(""),
    ])
    .nullable()
    .optional(),
  password: passwordComplexitySchema,
  role: z.enum(MEMBERSHIP_ROLES),
  profileId: z.string().nullable(),
});

export type CreateSystemUserFormValues = z.infer<typeof createSystemUserSchema>;
