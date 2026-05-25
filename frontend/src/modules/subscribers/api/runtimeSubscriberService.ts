import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';

/** ERP Runtime — tenant self-service (Admin). Not Platform Control Plane. */
export const RUNTIME_SUBSCRIBER_API = '/api/subscribers' as const;

const runtimeSubscriber = (subscriberId: string) =>
  `${RUNTIME_SUBSCRIBER_API}/${encodeURIComponent(subscriberId)}`;

export type RuntimeSubscriberDetailDto = {
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

export type UpdateRuntimeSubscriberCompanyBody = {
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

/** Subscriber Admin profile + operational settings via runtime `/api/subscribers/*`. */
export const runtimeSubscriberService = {
  getSubscriber: (subscriberId: string) =>
    api
      .get<ApiResponse<RuntimeSubscriberDetailDto>>(runtimeSubscriber(subscriberId))
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),

  updateSubscriberCompany: (subscriberId: string, body: UpdateRuntimeSubscriberCompanyBody) =>
    api
      .patch<ApiResponse<RuntimeSubscriberDetailDto>>(`${runtimeSubscriber(subscriberId)}/company`, {
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

  updateSubscriberOperationalSettings: (
    subscriberId: string,
    body: {
      currency: string;
      language: string;
      timezone: string;
      invoicePrefix?: string | null;
      defaultCreditDays: number;
    },
  ) =>
    api
      .patch<ApiResponse<RuntimeSubscriberDetailDto>>(`${runtimeSubscriber(subscriberId)}/operational-settings`, body)
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),
};
