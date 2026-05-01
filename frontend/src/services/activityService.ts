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
};

