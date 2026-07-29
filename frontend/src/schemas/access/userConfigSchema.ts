import { z } from "zod";
import { MEMBERSHIP_ROLES } from "../../modules/access/users/api/membershipService";
import { COMPANY_USER_LOGIN_MODES } from "../../modules/access/api/companyUserPreferencesService";
import { passwordComplexitySchema } from "../auth/passwordComplexity";

/**
 * Fase 2 (UAT/pulido UX) — schema único del formulario de /access/users/new y
 * /access/users/:userId. Reemplaza a userGeneralSchema.ts: un solo useForm cubre identidad,
 * rol/perfil, sucursales autorizadas y preferencias de ingreso, con un único botón "Guardar
 * configuración" en vez de un submit por sección.
 *
 * `stage` es un campo interno (nunca editado por el usuario, solo por UserConfigPage/
 * UserGeneralSection vía setValue) que decide qué validación aplica:
 * - 'pending': username aún no resuelto por lookupUserByUsername. El botón de guardar
 *   permanece deshabilitado en este estado — no hay reglas adicionales que validar aquí.
 * - 'create': username sin IdentityUser existente — alta completa (mismas reglas de
 *   password/nombre que createSystemUserSchema, vía composición, no duplicadas).
 * - 'assign': username ya existe como IdentityUser (con o sin membership previa) — solo
 *   rol/perfil.
 * - 'edit': edición de una membership ya existente — mismas reglas que 'assign'.
 */
export const userConfigStages = [
  "pending",
  "create",
  "assign",
  "edit",
] as const;
export type UserConfigStage = (typeof userConfigStages)[number];

export const userConfigSchema = z
  .object({
    stage: z.enum(userConfigStages),
    username: z.string().min(1, "El username es obligatorio."),
    firstName: z.string(),
    lastName: z.string(),
    email: z
      .union([
        z.string().email("El email no tiene un formato válido."),
        z.literal(""),
      ])
      .nullable()
      .optional(),
    password: z.string(),
    role: z.enum(MEMBERSHIP_ROLES),
    profileId: z.string().nullable(),
    authorizedBranchIds: z.array(z.string()),
    loginMode: z.enum(COMPANY_USER_LOGIN_MODES),
    defaultBranchId: z.string().nullable(),
  })
  .superRefine((data, ctx) => {
    if (data.stage === "create") {
      if (data.firstName.trim().length === 0) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "El nombre es obligatorio.",
          path: ["firstName"],
        });
      } else if (data.firstName.length > 50) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "Máximo 50 caracteres.",
          path: ["firstName"],
        });
      }
      if (data.lastName.trim().length === 0) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "El apellido es obligatorio.",
          path: ["lastName"],
        });
      } else if (data.lastName.length > 50) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "Máximo 50 caracteres.",
          path: ["lastName"],
        });
      }
      const passwordResult = passwordComplexitySchema.safeParse(data.password);
      if (!passwordResult.success) {
        for (const issue of passwordResult.error.issues) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: issue.message,
            path: ["password"],
          });
        }
      }
    }

    if (data.loginMode === "DirectToDefault") {
      if (!data.defaultBranchId) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message:
            "Selecciona una sucursal por defecto para el modo de ingreso directo.",
          path: ["defaultBranchId"],
        });
      } else if (!data.authorizedBranchIds.includes(data.defaultBranchId)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "La sucursal por defecto debe estar autorizada.",
          path: ["defaultBranchId"],
        });
      }
    }
  });

export type UserConfigFormValues = z.infer<typeof userConfigSchema>;
