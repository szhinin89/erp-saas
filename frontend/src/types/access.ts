export interface AccessibleTenant {
  tenantId: string;
  name: string;
  slug: string;
  role: string;
}

export interface BootstrapLoginRequest {
  email: string;
  password: string;
}

export interface BootstrapLoginResponse {
  userId: string;
  fullName: string;
  email: string;
  bootstrapToken: string;
  tenants: AccessibleTenant[];
}

export interface SwitchTenantRequest {
  tenantId: string;
}

export interface SessionResponse {
  userId: string;
  fullName: string;
  email: string;
  tenantId: string;
  role: string;
  token: string;
  planCode: string | null;
  enabledModules: string[];
}

/** Respuesta de `GET /api/access/me/permissions`. */
export interface MyPermissionsResponse {
  permissions: string[];
  planCode: string | null;
  enabledModules: string[];
}

/** Ítem de menú en `GET /api/access/me/menu` (definición en BD). */
export interface SessionMenuItemDto {
  routePath: string;
  labelKey: string;
  sortOrder: number;
  moduleKey: string | null;
  permissionKey: string | null;
  permissionKeysAny: string[] | null;
  /** Submenú recursivo (misma definición en BD). */
  children?: SessionMenuItemDto[] | null;
}

/** Grupo de menú en `GET /api/access/me/menu`. */
export interface SessionMenuGroupDto {
  code: string;
  icon: string;
  labelKey: string;
  sortOrder: number;
  moduleKey: string | null;
  roles: string[] | null;
  requireSuperAdminPanel: boolean;
  items: SessionMenuItemDto[];
}

