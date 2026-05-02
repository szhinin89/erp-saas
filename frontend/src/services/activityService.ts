import { api } from '../lib/api';
import type { ApiResponse } from '../types/api';

export type UserActivityDto = {
  id: string;
  module: string;
  action: string;
  entityType?: string | null;
  entityId?: string | null;
  description?: string | null;
  createdAt: string;
  userEmail?: string | null;
  userFullName?: string | null;
};

function getList<T>(url: string) {
  return api.get<ApiResponse<T>>(url).then((r) => r.data.responseObject);
}

export const activityService = {
  my: (opts?: { module?: string; page?: number; pageSize?: number }) => {
    const q = new URLSearchParams();
    if (opts?.module) q.set('module', opts.module);
    if (opts?.page) q.set('page', String(opts.page));
    if (opts?.pageSize) q.set('pageSize', String(opts.pageSize));
    const qs = q.toString();
    return getList<UserActivityDto[]>(`/api/activity/my${qs ? `?${qs}` : ''}`);
  },

  /** Últimos movimientos sobre una entidad (tenant), cualquier usuario. */
  forEntity: (opts: { entityType: string; entityId: string; take?: number }) => {
    const q = new URLSearchParams();
    q.set('entityType', opts.entityType);
    q.set('entityId', opts.entityId);
    if (opts.take != null) q.set('take', String(opts.take));
    return getList<UserActivityDto[]>(`/api/activity/entity?${q.toString()}`);
  },
};

