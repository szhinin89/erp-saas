import { api } from '../lib/api';
import type { ApiResponse } from '../types/api';

export type SuperAdminTenant = {
  id: string;
  name: string;
  slug: string;
};

export type SuperAdminMetrics = {
  totals: {
    totalTenants: number;
    activeTenants: number;
    totalUsers: number;
    activeUsers: number;
  };
  recentTenants: Array<{
    id: string;
    name: string;
    slug: string;
    isActive: boolean;
    createdAt: string;
  }>;
};

export const superAdminService = {
  getTenants: () =>
    api.get<ApiResponse<{ tenants: SuperAdminTenant[] }>>('/api/superadmin/tenants')
      .then((r) => r.data.responseObject.tenants),

  getMetrics: () =>
    api.get<ApiResponse<SuperAdminMetrics>>('/api/superadmin/metrics')
      .then((r) => r.data.responseObject),

  switchTenant: (tenantId: string) =>
    api.post<ApiResponse<import('../types/auth').AuthResponse>>('/api/auth/switch-tenant', { tenantId })
      .then((r) => r.data.responseObject),
};

