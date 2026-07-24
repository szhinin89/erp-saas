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
  companyId?: string | null;
  role: string;
  token: string;
}

export interface AccessibleCompany {
  companyId: string;
  tenantId: string;
  legalName: string;
  displayName: string;
  ruc: string;
  role: string;
}

/**
 * Ítem de menú retornado por `GET /api/me/menu`.
 * El backend ya filtró por permisos — el frontend solo renderiza.
 */
export interface NavMenuItemDto {
  id: string;
  labelKey: string;
  displayLabel?: string | null;
  routePath: string;
  children: NavMenuItemDto[];
}

/**
 * Grupo de menú retornado por `GET /api/me/menu`.
 * El backend ya filtró por permisos — el frontend solo renderiza.
 */
export interface NavMenuGroupDto {
  id: string;
  code: string;
  icon: string;
  labelKey: string;
  items: NavMenuItemDto[];
}


