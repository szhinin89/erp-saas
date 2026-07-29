import { z } from "zod";
import { COMPANY_USER_LOGIN_MODES } from "../../modules/access/api/companyUserPreferencesService";

/**
 * Validación de interfaz únicamente (formato). La autorización real de DefaultBranchId contra
 * CompanyUserBranch se valida exclusivamente en el backend (Fase C/F) — este schema solo exige
 * que, si el modo es DirectToDefault, se haya seleccionado alguna sucursal en el formulario.
 */
export const companyUserPreferencesSchema = z
  .object({
    loginMode: z.enum(COMPANY_USER_LOGIN_MODES),
    defaultBranchId: z.string().nullable(),
  })
  .refine(
    (data) => data.loginMode !== "DirectToDefault" || !!data.defaultBranchId,
    {
      message:
        "Selecciona una sucursal por defecto para el modo de ingreso directo.",
      path: ["defaultBranchId"],
    },
  );

export type CompanyUserPreferencesFormValues = z.infer<
  typeof companyUserPreferencesSchema
>;
