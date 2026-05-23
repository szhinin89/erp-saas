/**
 * Contrato auth platform ↔ backend (única fuente de verdad en frontend).
 * Valores wire legacy: solo lectura de BD/tokens/URLs antiguos — no usar en nombres de producto.
 */
const LEGACY_WIRE = {
  jwtRole: 'SuperAdmin',
  deploymentPanelFlag: 'superAdminPanelEnabled',
  navRequirePanelFlag: 'requireSuperAdminPanel',
  impersonationStorageKey: 'superadmin-impersonation-subscriber-name',
  uiPathPrefix: '/superadmin',
} as const;

export const JWT_PLATFORM_OPERATOR_ROLE = 'PlatformOperator' as const;

export type JwtPlatformOperatorRole = typeof JWT_PLATFORM_OPERATOR_ROLE;

export function isJwtPlatformOperatorRole(role: string | null | undefined): boolean {
  const r = (role ?? '').trim();
  return r === JWT_PLATFORM_OPERATOR_ROLE || r === LEGACY_WIRE.jwtRole;
}

/** Rol en arrays de navegación (`roles`, `itemRoles`) alineado con JWT. */
export const NAV_PLATFORM_OPERATOR_ROLE = JWT_PLATFORM_OPERATOR_ROLE;

/** Roles con privilegios de administración tenant (backend). */
export const TENANT_ADMIN_JWT_ROLES = ['Admin', JWT_PLATFORM_OPERATOR_ROLE, LEGACY_WIRE.jwtRole] as const;

/** Campo JSON canónico en deployment API. */
export const DEPLOYMENT_API_PLATFORM_PANEL_FLAG = 'platformPanelEnabled' as const;

/** Campo JSON canónico en menú API. */
export const NAV_API_PLATFORM_PANEL_FLAG = 'requirePlatformPanel' as const;

/** sessionStorage/localStorage legacy (migración one-shot). */
export const LEGACY_IMPERSONATION_NAME_STORAGE = LEGACY_WIRE.impersonationStorageKey;

/** Prefijo URL legacy — solo redirects en `platformRoutes.tsx`. */
export const PLATFORM_UI_LEGACY_PATH_PREFIX = LEGACY_WIRE.uiPathPrefix;

export function readPlatformPanelEnabled(obj: Record<string, unknown> | undefined): boolean {
  if (!obj) return true;
  if (typeof obj[DEPLOYMENT_API_PLATFORM_PANEL_FLAG] === 'boolean') {
    return obj[DEPLOYMENT_API_PLATFORM_PANEL_FLAG] as boolean;
  }
  const legacy = obj[LEGACY_WIRE.deploymentPanelFlag];
  if (typeof legacy === 'boolean') return legacy;
  return true;
}

export function readsRequirePlatformPanel(group: Record<string, unknown> | undefined): boolean {
  if (!group) return false;
  if (typeof group[NAV_API_PLATFORM_PANEL_FLAG] === 'boolean') {
    return group[NAV_API_PLATFORM_PANEL_FLAG] as boolean;
  }
  const legacy = group[LEGACY_WIRE.navRequirePanelFlag];
  if (typeof legacy === 'boolean') return legacy;
  return false;
}
