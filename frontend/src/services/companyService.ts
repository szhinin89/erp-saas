import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';
import type { SessionResponse } from '../types/access';
import { PLATFORM_SUBSCRIBERS_API, parsePlatformSubscriberList, type SuperAdminSubscriber } from './superAdminService';

export type CompanyItem = {
  id: string;
  name: string;
  slug: string;
  /** Si viene del API de listado SuperAdmin. */
  isActive?: boolean;
  planCode?: string | null;
  enabledModules?: string[];
  /** Si es false, el subscriber no tiene JSON de módulos (equivalente a «todos los módulos»). */
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

/** Detalle de suscriptor SaaS (`GET /api/subscribers/{id}`), alineado con `SubscriberDto` del backend. */
export type SubscriberDetailDto = {
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
  currency: string;
  language: string;
  timezone: string;
  invoicePrefix: string | null;
  defaultCreditDays: number;
};

export const companyService = {
  list: () =>
    api
      .get<ApiResponse<SuperAdminSubscriber[]>>(PLATFORM_SUBSCRIBERS_API)
      .then((r) => {
        const rows = parsePlatformSubscriberList(r.data.responseObject);
        return rows.map(
          (s): CompanyItem => ({
            id: s.id,
            name: s.name,
            slug: s.slug,
            isActive: s.isActive,
            planCode: s.planCode,
            enabledModules: s.enabledModules,
            hasModuleRestrictions: s.hasModuleRestrictions,
          }),
        );
      }),

  getSubscriber: (subscriberId: string) =>
    api
      .get<ApiResponse<SubscriberDetailDto>>(`/api/subscribers/${encodeURIComponent(subscriberId)}`)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  create: (req: CreateCompanyWithAdminRequest) =>
    api.post<ApiResponse<SessionResponse>>(PLATFORM_SUBSCRIBERS_API, req).then((r) => r.data.responseObject),

  updateSubscriberCompany: (subscriberId: string, body: UpdateSubscriberCompanyBody) =>
    api
      .patch<ApiResponse<SubscriberDetailDto>>(`/api/subscribers/${encodeURIComponent(subscriberId)}/company`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  updateSubscriberOperationalSettings: (subscriberId: string, body: {
    currency: string;
    language: string;
    timezone: string;
    invoicePrefix?: string | null;
    defaultCreditDays: number;
  }) =>
    api
      .patch<ApiResponse<SubscriberDetailDto>>(`/api/subscribers/${encodeURIComponent(subscriberId)}/operational-settings`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  updateSubscriberGlobalParameters: (subscriberId: string, body: UpdateSubscriberGlobalParametersBody) =>
    api
      .patch<ApiResponse<SubscriberDetailDto>>(`/api/subscribers/${encodeURIComponent(subscriberId)}/global-parameters`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  resolveSubscriberConfig: (subscriberId: string, key: string, module?: string | null, feature?: string | null) =>
    api
      .get<ApiResponse<ResolvedConfigValueDto | null>>(`/api/superadmin/config/${encodeURIComponent(subscriberId)}/resolve`, {
        params: { key, module: module ?? undefined, feature: feature ?? undefined },
      })
      .then((r) => r.data.responseObject),

  listSubscriberGlobalConfig: (subscriberId: string) =>
    api
      .get<ApiResponse<ConfigEntryDto[]>>(`/api/superadmin/config/${encodeURIComponent(subscriberId)}/global`)
      .then((r) => r.data.responseObject ?? []),

  upsertSubscriberGlobalConfig: (subscriberId: string, body: UpsertConfigBody) =>
    api
      .put<ApiResponse<ConfigEntryDto>>(`/api/superadmin/config/${encodeURIComponent(subscriberId)}/global`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),
};
