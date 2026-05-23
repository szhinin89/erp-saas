import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';
import { PLATFORM_API } from './platformApiPaths';

const platformSubscriber = (subscriberId: string) =>
  `${PLATFORM_API.subscribers}/${encodeURIComponent(subscriberId)}`;

const platformConfig = (subscriberId: string) =>
  `${PLATFORM_API.config}/${encodeURIComponent(subscriberId)}`;

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

/** Platform Control Plane — canonical `/api/platform/subscribers` + config. */
export const subscriberService = {
  getSubscriber: (subscriberId: string) =>
    api
      .get<ApiResponse<SubscriberDetailDto>>(platformSubscriber(subscriberId))
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  updateSubscriberCompany: (subscriberId: string, body: UpdateSubscriberCompanyBody) =>
    api
      .patch<ApiResponse<SubscriberDetailDto>>(`${platformSubscriber(subscriberId)}/company`, {
        name: body.name,
        slug: body.slug,
        ruc: body.ruc,
        shortName: body.shortName,
        tradeName: body.tradeName,
        dinardap: body.dinardap,
        logoUrl: body.logoUrl,
        displayOrder: body.displayOrder,
        priority: body.priority,
      })
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  updateSubscriberGlobalParameters: (subscriberId: string, body: UpdateSubscriberGlobalParametersBody) =>
    api
      .patch<ApiResponse<SubscriberDetailDto>>(`${platformSubscriber(subscriberId)}/global-parameters`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  resolveSubscriberConfig: (subscriberId: string, key: string, module?: string | null, feature?: string | null) =>
    api
      .get<ApiResponse<ResolvedConfigValueDto | null>>(`${platformConfig(subscriberId)}/resolve`, {
        params: { key, module: module ?? undefined, feature: feature ?? undefined },
      })
      .then((r) => r.data.responseObject),

  listSubscriberGlobalConfig: (subscriberId: string) =>
    api
      .get<ApiResponse<ConfigEntryDto[]>>(`${platformConfig(subscriberId)}/global`)
      .then((r) => r.data.responseObject ?? []),

  upsertSubscriberGlobalConfig: (subscriberId: string, body: UpsertConfigBody) =>
    api
      .put<ApiResponse<ConfigEntryDto>>(`${platformConfig(subscriberId)}/global`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),
};
