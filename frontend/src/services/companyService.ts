import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';
import type { SessionResponse } from '../types/access';

export type CompanyItem = {
  id: string;
  name: string;
  slug: string;
  /** Si viene del API de listado SuperAdmin. */
  isActive?: boolean;
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
  /** Si true, el email debe ser de un usuario ya registrado; se le asigna Admin en la nueva empresa. */
  linkExistingAdmin?: boolean;
  passwordResetMode?: number;
};

export type UpdateTenantCompanyBody = {
  name: string;
  slug: string;
  ruc?: string | null;
  shortName?: string | null;
  tradeName?: string | null;
  dinardap?: string | null;
  logoUrl?: string | null;
  displayOrder: number;
  priority: number;
};

export type UpdateTenantGlobalParametersBody = {
  electronicBillingTrialEnabled: boolean;
};

export type ConfigEntryDto = {
  id: string;
  tenantId: string;
  scope: 'global' | 'module' | 'feature' | string;
  module: string | null;
  feature: string | null;
  key: string;
  value: string;
  dataType: string;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type ResolvedConfigValueDto = {
  tenantId: string;
  key: string;
  module: string | null;
  feature: string | null;
  scopeResolved: 'global' | 'module' | 'feature' | string;
  value: string;
  dataType: string;
};

export type UpsertConfigBody = {
  key: string;
  value: string;
  dataType: string;
};

/** Detalle de empresa (`GET /api/tenants/{id}`), alineado con `TenantDto` del backend. */
export type TenantDetailDto = {
  id: string;
  name: string;
  slug: string;
  isActive: boolean;
  createdAt: string;
  ruc: string | null;
  shortName: string | null;
  tradeName: string | null;
  dinardap: string | null;
  logoUrl: string | null;
  displayOrder: number;
  priority: number;
  electronicBillingTrialEnabled: boolean;
  planCode: string | null;
  enabledModules: string[];
  hasModuleRestrictions: boolean;
};

export const companyService = {
  list: () =>
    api.get<ApiResponse<{ tenants: CompanyItem[] }>>('/api/access/superadmin/tenants')
      .then((r) => r.data.responseObject.tenants),

  getTenant: (tenantId: string) =>
    api
      .get<ApiResponse<TenantDetailDto>>(`/api/tenants/${encodeURIComponent(tenantId)}`)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  create: (req: CreateCompanyWithAdminRequest) =>
    api.post<ApiResponse<SessionResponse>>('/api/access/superadmin/tenants', req).then((r) => r.data.responseObject),

  updateTenantCompany: (tenantId: string, body: UpdateTenantCompanyBody) =>
    api
      .patch<ApiResponse<TenantDetailDto>>(`/api/tenants/${encodeURIComponent(tenantId)}/company`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  updateTenantGlobalParameters: (tenantId: string, body: UpdateTenantGlobalParametersBody) =>
    api
      .patch<ApiResponse<TenantDetailDto>>(`/api/tenants/${encodeURIComponent(tenantId)}/global-parameters`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  resolveTenantConfig: (tenantId: string, key: string, module?: string | null, feature?: string | null) =>
    api
      .get<ApiResponse<ResolvedConfigValueDto | null>>(`/api/superadmin/config/${encodeURIComponent(tenantId)}/resolve`, {
        params: { key, module: module ?? undefined, feature: feature ?? undefined },
      })
      .then((r) => r.data.responseObject),

  listTenantGlobalConfig: (tenantId: string) =>
    api
      .get<ApiResponse<ConfigEntryDto[]>>(`/api/superadmin/config/${encodeURIComponent(tenantId)}/global`)
      .then((r) => r.data.responseObject ?? []),

  upsertTenantGlobalConfig: (tenantId: string, body: UpsertConfigBody) =>
    api
      .put<ApiResponse<ConfigEntryDto>>(`/api/superadmin/config/${encodeURIComponent(tenantId)}/global`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),
};

