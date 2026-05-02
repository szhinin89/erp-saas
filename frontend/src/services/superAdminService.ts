import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export type SuperAdminTenant = {
  id: string;
  name: string;
  slug: string;
  isActive: boolean;
  createdAt: string;
  totalUsers: number;
  activeUsers: number;
  planCode?: string | null;
  enabledModules?: string[];
  hasModuleRestrictions?: boolean;
};

export type SuperAdminPlanFeature = {
  featureCode: string;
  featureName: string;
  description: string | null;
  isMetered: boolean;
  isIncluded: boolean;
  limitPerPeriod: number | null;
};

export type SuperAdminPlan = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  features: SuperAdminPlanFeature[];
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

  getPlansCatalog: () =>
    api.get<ApiResponse<{ plans: SuperAdminPlan[] }>>('/api/superadmin/plans')
      .then((r) => r.data.responseObject.plans),

  switchTenant: (tenantId: string) =>
    api.post<ApiResponse<import('../types/auth').AuthResponse>>('/api/auth/switch-tenant', { tenantId })
      .then((r) => r.data.responseObject),
};

