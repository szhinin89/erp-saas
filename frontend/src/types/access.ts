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
}

