import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';
import type { SessionResponse } from '../types/access';

export type CompanyItem = {
  id: string;
  name: string;
  slug: string;
  planCode?: string | null;
  enabledModules?: string[];
  /** Si es false, el tenant no tiene JSON de módulos (equivalente a «todos los módulos»). */
  hasModuleRestrictions?: boolean;
};

export type CreateCompanyWithAdminRequest = {
  tenantName: string;
  tenantSlug: string;
  ruc?: string | null;
  shortName?: string | null;
  tradeName?: string | null;
  dinardap?: string | null;
  logoUrl?: string | null;
  displayOrder?: number;
  priority?: number;
  adminFirstName: string;
  adminLastName: string;
  adminEmail: string;
  adminPassword: string;
  passwordResetMode?: number;
};

export type UpdateTenantSubscriptionBody = {
  planCode?: string | null;
  /** `null` o lista vacía en servidor = sin restricción (todos los módulos). Lista explícita = solo esos. */
  enabledModules?: string[] | null;
};

export const companyService = {
  list: () =>
    api.get<ApiResponse<{ tenants: CompanyItem[] }>>('/api/access/superadmin/tenants')
      .then((r) => r.data.responseObject.tenants),

  create: (req: CreateCompanyWithAdminRequest) =>
    api.post<ApiResponse<SessionResponse>>('/api/access/superadmin/tenants', req).then((r) => r.data.responseObject),

  updateSubscription: (tenantId: string, body: UpdateTenantSubscriptionBody) =>
    api
      .patch<ApiResponse<unknown>>(`/api/tenants/${tenantId}/subscription`, body)
      .then((r) => r.data.responseObject),
};

