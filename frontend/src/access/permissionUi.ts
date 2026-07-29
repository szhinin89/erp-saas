/** Roles that receive wildcard permissions from backend (`*`). */
export const ADMIN_ROLES = ["Admin"] as const;

export function isAdminRole(role?: string | null): boolean {
  if (!role) return false;
  return ADMIN_ROLES.includes(role as (typeof ADMIN_ROLES)[number]);
}
