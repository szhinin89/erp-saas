import { isAdminRole } from "../../access/permissionUi";

type Translate = (key: string, fallback?: string) => string;

/**
 * IAM-V1-F2 — única presentación de la función de una membership. Internamente sigue siendo
 * `Role = Admin | User` (contrato y autorización sin cambios): `Admin` es «Administrador de
 * empresa» (acceso total por su rol, nunca por un perfil) y `User` es «Usuario operativo»
 * (acceso según su perfil). Nunca mostrar el valor crudo en pantallas de IAM.
 */
export function membershipRoleLabel(role: string | null | undefined, t: Translate): string {
  return isAdminRole(role)
    ? t("access.role.companyAdmin", "Administrador de empresa")
    : t("access.role.operative", "Usuario operativo");
}

export function membershipRoleDescription(
  role: string | null | undefined,
  t: Translate,
): string {
  return isAdminRole(role)
    ? t(
        "access.role.companyAdmin.description",
        "Acceso total a esta empresa por su función. No usa perfil de acceso.",
      )
    : t(
        "access.role.operative.description",
        "Accede solo a las pantallas y acciones de su perfil.",
      );
}
