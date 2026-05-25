/**
 * Contrato auth platform ↔ backend (única fuente de verdad en frontend).
 */
export const JWT_PLATFORM_OPERATOR_ROLE = 'PlatformOperator' as const;

export type JwtPlatformOperatorRole = typeof JWT_PLATFORM_OPERATOR_ROLE;

export function isJwtPlatformOperatorRole(role: string | null | undefined): boolean {
  return (role ?? '').trim() === JWT_PLATFORM_OPERATOR_ROLE;
}

/** Rol en arrays de navegación (`roles`, `itemRoles`) alineado con JWT. */
export const NAV_PLATFORM_OPERATOR_ROLE = JWT_PLATFORM_OPERATOR_ROLE;

/** Roles con privilegios de administración de suscriptor (backend). */
export const SUBSCRIBER_ADMIN_JWT_ROLES = ['Admin', JWT_PLATFORM_OPERATOR_ROLE] as const;

/** Campo JSON canónico en deployment API. */
export const DEPLOYMENT_API_PLATFORM_PANEL_FLAG = 'platformPanelEnabled' as const;

/** Campo JSON canónico en menú API. */
export const NAV_API_PLATFORM_PANEL_FLAG = 'requirePlatformPanel' as const;

export function readPlatformPanelEnabled(obj: Record<string, unknown> | undefined): boolean {
  if (!obj) return true;
  if (typeof obj[DEPLOYMENT_API_PLATFORM_PANEL_FLAG] === 'boolean') {
    return obj[DEPLOYMENT_API_PLATFORM_PANEL_FLAG] as boolean;
  }
  return true;
}

interface PlatformPanelNavGroup {
  requirePlatformPanel?: boolean;
}

export function readsRequirePlatformPanel(group: PlatformPanelNavGroup | undefined): boolean {
  return group?.requirePlatformPanel === true;
}
