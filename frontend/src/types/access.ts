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
  /** Si viene de BD, sustituye a la etiqueta i18n al mostrar en la barra. */
  displayLabel?: string | null;
  sortOrder: number;
  moduleKey: string | null;
  permissionKey: string | null;
  permissionKeysAny: string[] | null;
  /** Restringe el ítem por rol (p. ej. `SuperAdmin`); viene de `roles_csv` en BD. */
  itemRoles?: string[] | null;
  /** Submenú recursivo (misma definición en BD). */
  children?: SessionMenuItemDto[] | null;
  /** Icono (p. ej. clase FontAwesome) en menús de plan / JSON personalizado. */
  icon?: string | null;
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

