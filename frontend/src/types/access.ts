export interface AccessibleSubscriber {
  subscriberId: string;
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
  subscribers: AccessibleSubscriber[];
}

export interface SwitchSubscriberRequest {
  subscriberId: string;
}

export interface SessionResponse {
  userId: string;
  fullName: string;
  email: string;
  subscriberId: string;
  companyId?: string | null;
  role: string;
  token: string;
  planCode: string | null;
  enabledModules: string[];
}

export interface AccessibleCompany {
  companyId: string;
  subscriberId: string;
  legalName: string;
  displayName: string;
  ruc: string;
  role: string;
}

/** Respuesta de `GET /api/admin/iam/me/permissions`. */
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
  /** Restringe el ítem por rol JWT (p. ej. operador platform); viene de `roles_csv` en BD. */
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
  /** Si true, el grupo solo aplica con panel platform habilitado. */
  requirePlatformPanel?: boolean;
  /** Alias legacy del API. */
  requireSuperAdminPanel?: boolean;
  items: SessionMenuItemDto[];
  /** `horizontal` | `vertical` desde el constructor de menú (plan/empresa). */
  menuBarLayout?: string | null;
}

