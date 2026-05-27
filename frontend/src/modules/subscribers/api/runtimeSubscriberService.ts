import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';

/** ERP Runtime — self-service del suscriptor (Admin). Not Platform Control Plane. */
export const RUNTIME_SUBSCRIBER_API = '/api/subscribers' as const;

const runtimeSubscriber = (subscriberId: string) =>
  `${RUNTIME_SUBSCRIBER_API}/${encodeURIComponent(subscriberId)}`;

export type RuntimeSubscriberDetailDto = {
  id: string;
  name: string;
  slug: string;
  isActive: boolean;
  createdAt: string;
  displayOrder: number;
  priority: number;
  planCode: string | null;
  enabledModules: string[];
  hasModuleRestrictions: boolean;
  preferredLanguage: string;
};

export type UpdateRuntimeSubscriberCompanyBody = {
  name: string;
  slug: string;
  displayOrder: number;
  priority: number;
  preferredLanguage?: string;
};

/** Subscriber Admin profile via runtime `/api/subscribers/*`. */
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
        displayOrder: body.displayOrder,
        priority: body.priority,
        preferredLanguage: body.preferredLanguage ?? 'es',
      })
      .then((r) => {
        const o = r.data.responseObject;
        if (!o) throw new Error('empty');
        return o;
      }),
};
