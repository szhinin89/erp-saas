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
  subscriberName: string;
  subscriberSlug: string;
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
  planCode?: string | null;
  enabledModules?: string[] | null;
};

export type UpdateSubscriberCompanyBody = {
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

export type UpdateSubscriberGlobalParametersBody = {
  electronicBillingTrialEnabled: boolean;
};

export type ConfigEntryDto = {
  id: string;
  subscriberId: string;
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
  subscriberId: string;
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

/** Detalle de empresa (`GET /api/subscribers/{id}`), alineado con `SubscriberDto` del backend. */
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
  // Parámetros operativos
  currency: string;
  language: string;
  timezone: string;
  invoicePrefix: string | null;
  defaultCreditDays: number;
};

export const companyService = {
  list: () =>
    api.get<ApiResponse<{ subscribers?: CompanyItem[] } | CompanyItem[]>>('/api/admin/iam/superadmin/subscribers')
      .then((r) => {
        const responseObject = r.data.responseObject;
        if (Array.isArray(responseObject)) {
          return responseObject;
        }
        if (responseObject && Array.isArray(responseObject.subscribers)) {
          return responseObject.subscribers;
        }
        return [];
      }),

  getTenant: (subscriberId: string) =>
    api
      .get<ApiResponse<TenantDetailDto>>(`/api/subscribers/${encodeURIComponent(subscriberId)}`)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  create: (req: CreateCompanyWithAdminRequest) =>
    api.post<ApiResponse<SessionResponse>>('/api/admin/iam/superadmin/subscribers', req).then((r) => r.data.responseObject),

  updateTenantCompany: (subscriberId: string, body: UpdateSubscriberCompanyBody) =>
    api
      .patch<ApiResponse<TenantDetailDto>>(`/api/subscribers/${encodeURIComponent(subscriberId)}/company`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  updateTenantOperationalSettings: (subscriberId: string, body: {
    currency: string;
    language: string;
    timezone: string;
    invoicePrefix?: string | null;
    defaultCreditDays: number;
  }) =>
    api
      .patch<ApiResponse<TenantDetailDto>>(`/api/subscribers/${encodeURIComponent(subscriberId)}/operational-settings`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  updateTenantGlobalParameters: (subscriberId: string, body: UpdateSubscriberGlobalParametersBody) =>
    api
      .patch<ApiResponse<TenantDetailDto>>(`/api/subscribers/${encodeURIComponent(subscriberId)}/global-parameters`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  resolveTenantConfig: (subscriberId: string, key: string, module?: string | null, feature?: string | null) =>
    api
      .get<ApiResponse<ResolvedConfigValueDto | null>>(`/api/superadmin/config/${encodeURIComponent(subscriberId)}/resolve`, {
        params: { key, module: module ?? undefined, feature: feature ?? undefined },
      })
      .then((r) => r.data.responseObject),

  listTenantGlobalConfig: (subscriberId: string) =>
    api
      .get<ApiResponse<ConfigEntryDto[]>>(`/api/superadmin/config/${encodeURIComponent(subscriberId)}/global`)
      .then((r) => r.data.responseObject ?? []),

  upsertTenantGlobalConfig: (subscriberId: string, body: UpsertConfigBody) =>
    api
      .put<ApiResponse<ConfigEntryDto>>(`/api/superadmin/config/${encodeURIComponent(subscriberId)}/global`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),
};

