import type { AuthResponse } from "../types/auth";

/** Roles that receive wildcard permissions from backend (`*`). */
export const ADMIN_ROLES = ["Admin"] as const;

export const GLOBAL_TENANT_ID = "00000000-0000-0000-0000-000000000000";

export type AuthUser = Omit<
  AuthResponse,
  "token" | "refreshToken" | "refreshTokenExpiry"
>;

export function isAdminRole(role?: string | null): boolean {
  if (!role) return false;
  return ADMIN_ROLES.includes(role as (typeof ADMIN_ROLES)[number]);
}

export function canProvisionCompanies(user?: AuthUser | null): boolean {
  return (
    user?.tenantId === GLOBAL_TENANT_ID &&
    user?.role === "Admin" &&
    !user?.companyId
  );
}
