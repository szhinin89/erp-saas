/**
 * Contrato auth platform ↔ backend (única fuente de verdad en frontend).
 */
export const JWT_PLATFORM_OPERATOR_ROLE = 'PlatformOperator' as const;

/** Alias legacy aceptado en tokens antiguos y menús BD. */
export const LEGACY_JWT_PLATFORM_OPERATOR_ROLE = 'SuperAdmin' as const;

export type JwtPlatformOperatorRole = typeof JWT_PLATFORM_OPERATOR_ROLE;

export function isJwtPlatformOperatorRole(role: string | null | undefined): boolean {
  const r = (role ?? '').trim();
  return r === JWT_PLATFORM_OPERATOR_ROLE || r === LEGACY_JWT_PLATFORM_OPERATOR_ROLE;
}

/** Rol en arrays de navegación (`roles`, `itemRoles`) alineado con JWT. */
export const NAV_PLATFORM_OPERATOR_ROLE = JWT_PLATFORM_OPERATOR_ROLE;

/** Roles con privilegios de administración tenant (backend). */
export const TENANT_ADMIN_JWT_ROLES = ['Admin', JWT_PLATFORM_OPERATOR_ROLE, LEGACY_JWT_PLATFORM_OPERATOR_ROLE] as const;

/** Campo JSON canónico en deployment API. */
export const DEPLOYMENT_API_PLATFORM_PANEL_FLAG = 'platformPanelEnabled' as const;

/** Campo JSON legacy en deployment API. */
export const DEPLOYMENT_API_LEGACY_PANEL_FLAG = 'superAdminPanelEnabled' as const;

/** Campo JSON canónico en menú API. */
export const NAV_API_PLATFORM_PANEL_FLAG = 'requirePlatformPanel' as const;

/** Campo JSON legacy en menú admin API. */
export const NAV_API_LEGACY_REQUIRE_PANEL_FLAG = 'requireSuperAdminPanel' as const;

/** sessionStorage/localStorage legacy (migración one-shot). */
export const LEGACY_IMPERSONATION_NAME_STORAGE = 'superadmin-impersonation-subscriber-name' as const;

/** Prefijo URL legacy — solo redirects en `platformRoutes.tsx`. */
export const PLATFORM_UI_LEGACY_PATH_PREFIX = '/superadmin' as const;

export function readPlatformPanelEnabled(obj: Record<string, unknown> | undefined): boolean {
  if (!obj) return true;
  if (typeof obj[DEPLOYMENT_API_PLATFORM_PANEL_FLAG] === 'boolean') {
    return obj[DEPLOYMENT_API_PLATFORM_PANEL_FLAG] as boolean;
  }
  const legacy = obj[DEPLOYMENT_API_LEGACY_PANEL_FLAG];
  if (typeof legacy === 'boolean') return legacy;
  return true;
}

export function readsRequirePlatformPanel(group: {
  requirePlatformPanel?: boolean;
  requireSuperAdminPanel?: boolean;
}): boolean {
  if (typeof group.requirePlatformPanel === 'boolean') return group.requirePlatformPanel;
  if (typeof group.requireSuperAdminPanel === 'boolean') return group.requireSuperAdminPanel;
  return false;
}
