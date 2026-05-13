import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';
import type { SessionResponse, SessionMenuGroupDto } from '../types/access';

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
  hasCustomMenu?: boolean;
};

/** Coincide con SaasFeatureKind en backend (0–3). */
export type SaasFeatureKind = 0 | 1 | 2 | 3;

export type SuperAdminPlanFeature = {
  featureCode: string;
  featureName: string;
  description: string | null;
  isMetered: boolean;
  kind?: string;
  resourceRef?: string | null;
  isIncluded: boolean;
  limitPerPeriod: number | null;
};

export type SuperAdminPlan = {
  id: string;
  code: string;
  name: string;
  shortLabel?: string | null;
  isActive: boolean;
  priceAmount?: number;
  currency?: string;
  billingCycle?: string;
  isPubliclyVisible?: boolean;
  isRecommended?: boolean;
  sortOrder?: number;
  externalBillingRef?: string | null;
  features: SuperAdminPlanFeature[];
};

export type SaasPlanFeatureAdmin = {
  featureId: string;
  featureCode: string;
  featureName: string;
  isMetered: boolean;
  kind: SaasFeatureKind;
  resourceRef: string | null;
  isIncluded: boolean;
  limitPerPeriod: number | null;
};

export type SaasPlanAdmin = {
  id: string;
  code: string;
  name: string;
  shortLabel: string | null;
  isActive: boolean;
  priceAmount: number;
  currency: string;
  billingCycle: string;
  isPubliclyVisible: boolean;
  isRecommended: boolean;
  sortOrder: number;
  externalBillingRef: string | null;
  hasMenuConfig?: boolean;
  menuSidebarLayout?: string;
  features: SaasPlanFeatureAdmin[];
};

export type PlanMenuRead = {
  menuConfigJson: string | null;
  menuSidebarLayout: string;
};

export type CreateSaasPlanBody = {
  code: string;
  name: string;
  shortLabel: string | null;
  isActive: boolean;
  priceAmount: number;
  currency: string;
  billingCycle: string;
  isPubliclyVisible: boolean;
  isRecommended: boolean;
  sortOrder: number;
  externalBillingRef: string | null;
};

export type UpdateSaasPlanBody = {
  name: string;
  shortLabel: string | null;
  isActive: boolean;
  priceAmount: number;
  currency: string;
  billingCycle: string;
  isPubliclyVisible: boolean;
  externalBillingRef: string | null;
  menuSidebarLayout?: string | null;
};

export type CreateTenantWithAdminBody = {
  tenantName: string;
  tenantSlug: string;
  adminFirstName: string;
  adminLastName: string;
  adminEmail: string;
  adminPassword: string;
  /** Coincide con `PasswordResetMode` en backend: 0 Disabled, 1 Direct, 2 Email, 3 Phone. */
  passwordResetMode?: number;
  /** Si true, no crea usuario; vincula un Admin existente por email. */
  linkExistingAdmin?: boolean;
  /** Código de plan SaaS (catálogo). Opcional. */
  planCode?: string | null;
  /** Si se envía lista no vacía, restringe módulos; si se omite o [], sin JSON de restricción (todos). */
  enabledModules?: string[] | null;
};

export type UpdateTenantSubscriptionBody = {
  planCode?: string | null;
  enabledModules?: string[] | null;
};

export type SaasPublicPlan = {
  id: string;
  code: string;
  name: string;
  shortLabel: string | null;
  priceAmount: number;
  currency: string;
  billingCycle: string;
  isRecommended: boolean;
  sortOrder: number;
  features: Array<{
    code: string;
    name: string;
    description: string | null;
    isMetered: boolean;
    kind: string;
    resourceRef: string | null;
    isIncluded: boolean;
    limitPerPeriod: number | null;
  }>;
};

export type AdminNavItemRow = {
  id: string;
  parentItemId: string | null;
  routePath: string;
  labelKey: string;
  displayLabel?: string | null;
  sortOrder: number;
  moduleKey: string | null;
  permissionKey: string | null;
  permissionKeysAny: string[] | null;
  isActive: boolean;
  /** FK opcional en BD: ítem del menú ↔ definición SaaS (Plan ↔ menú). */
  saasFeatureDefinitionId?: string | null;
  children?: AdminNavItemRow[] | null;
};

export type AdminNavGroupRow = {
  id: string;
  code: string;
  icon: string;
  labelKey: string;
  sortOrder: number;
  moduleKey: string | null;
  roles: string[] | null;
  requireSuperAdminPanel: boolean;
  isActive: boolean;
  rootItems: AdminNavItemRow[];
};

export type AdminNavigationMenu = {
  groups: AdminNavGroupRow[];
};

export type FuncionalidadArbolDto = {
  id: string;
  nombre: string;
  icono: string | null;
  ruta: string | null;
  permiso: string;
  hijos: FuncionalidadArbolDto[];
};

export type NavItemSiblingOrderLevel = {
  groupId: string;
  parentItemId: string | null;
  orderedItemIds: string[];
};

export type CreateNavItemBody = {
  groupId: string;
  parentItemId: string | null;
  routePath: string;
  displayLabel: string;
  moduleKey?: string | null;
  permissionKey?: string | null;
};

export type UpdateNavItemBody = {
  displayLabel: string;
  routePath: string;
  moduleKey?: string | null;
  permissionKey?: string | null;
  /** Vacío o null = quitar vínculo con feature SaaS */
  saasFeatureDefinitionId?: string | null;
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

export type GrowthAnalyticsBucket = {
  periodStart: string;
  periodEnd: string;
  periodLabel: string;
  newTenants: number;
  newIdentityUsers: number;
  newMemberships: number;
  cumulativeTenants: number;
  cumulativeIdentityUsers: number;
  cumulativeMemberships: number;
};

export type GrowthAnalyticsResponse = {
  from: string;
  to: string;
  granularity: string;
  series: GrowthAnalyticsBucket[];
};

export type GrowthMonetaryBucket = {
  periodStart: string;
  periodEnd: string;
  periodLabel: string;
  newMrrApprox: number;
  cumulativeMrrApprox: number;
};

export type GrowthMonetaryResponse = {
  from: string;
  to: string;
  granularity: string;
  currencyHint: string;
  series: GrowthMonetaryBucket[];
};

export const superAdminService = {
  getTenants: () =>
    api.get<ApiResponse<{ tenants: SuperAdminTenant[] }>>('/api/superadmin/tenants')
      .then((r) => r.data.responseObject.tenants),

  createTenantWithAdmin: (body: CreateTenantWithAdminBody) =>
    api
      .post<ApiResponse<SessionResponse>>('/api/access/superadmin/tenants', body)
      .then((r) => r.data.responseObject),

  updateTenantSubscription: (tenantId: string, body: UpdateTenantSubscriptionBody) =>
    api
      .patch<ApiResponse<unknown>>(`/api/tenants/${encodeURIComponent(tenantId)}/subscription`, body)
      .then((r) => r.data),

  getMetrics: () =>
    api.get<ApiResponse<SuperAdminMetrics>>('/api/superadmin/metrics')
      .then((r) => r.data.responseObject),

  getGrowthAnalytics: (from: string, to: string, granularity?: string) => {
    const params: Record<string, string> = { from, to };
    if (granularity) params.granularity = granularity;
    return api
      .get<ApiResponse<GrowthAnalyticsResponse>>('/api/superadmin/growth-analytics', { params })
      .then((r) => r.data.responseObject);
  },

  getGrowthMonetaryAnalytics: (from: string, to: string, granularity?: string) => {
    const params: Record<string, string> = { from, to };
    if (granularity) params.granularity = granularity;
    return api
      .get<ApiResponse<GrowthMonetaryResponse>>('/api/superadmin/growth-analytics-monetary', { params })
      .then((r) => r.data.responseObject);
  },

  getPlansCatalog: () =>
    api.get<ApiResponse<{ plans: SuperAdminPlan[] }>>('/api/superadmin/plans')
      .then((r) => r.data.responseObject.plans),

  /** Catálogo administrable (CRUD); mismo contenido enriquecido que el catálogo de lectura. */
  listSaasPlansAdmin: () =>
    api.get<ApiResponse<{ plans: SaasPlanAdmin[] }>>('/api/superadmin/saas-plans').then((r) => r.data.responseObject.plans),

  createSaasPlan: (body: CreateSaasPlanBody) =>
    api.post<ApiResponse<{ id: string }>>('/api/superadmin/saas-plans', body).then((r) => r.data.responseObject.id),

  updateSaasPlan: (planId: string, body: UpdateSaasPlanBody) =>
    api.put<ApiResponse<Record<string, unknown>>>(`/api/superadmin/saas-plans/${planId}`, body).then((r) => r.data),

  deleteSaasPlan: (planId: string) =>
    api.delete<ApiResponse<Record<string, unknown>>>(`/api/superadmin/saas-plans/${planId}`).then((r) => r.data),

  reorderSaasPlans: (orderedPlanIds: string[]) =>
    api.put<ApiResponse<Record<string, unknown>>>('/api/superadmin/saas-plans/reorder', { orderedPlanIds }).then((r) => r.data),

  setSaasPlanRecommended: (planId: string) =>
    api.put<ApiResponse<Record<string, unknown>>>(`/api/superadmin/saas-plans/${planId}/recommended`).then((r) => r.data),

  getPlanMenu: (planId: string) =>
    api
      .get<ApiResponse<PlanMenuRead>>(`/api/superadmin/planes/${encodeURIComponent(planId)}/menu`)
      .then((r) => r.data.responseObject),

  setPlanMenuJson: (planId: string, menuConfigJson: string | null, menuSidebarLayout?: string | null) =>
    api
      .put<ApiResponse<Record<string, unknown>>>(`/api/superadmin/planes/${encodeURIComponent(planId)}/menu`, {
        menuConfigJson,
        ...(menuSidebarLayout != null ? { menuSidebarLayout } : {}),
      })
      .then((r) => r.data),

  copyPlanFrom: (
    targetPlanId: string,
    sourcePlanId: string,
    opts?: { copyMenu?: boolean },
  ) =>
    api
      .post<ApiResponse<Record<string, unknown>>>(
        `/api/superadmin/saas-plans/${encodeURIComponent(targetPlanId)}/copy-from/${encodeURIComponent(sourcePlanId)}`,
        {
          copyMenu: opts?.copyMenu ?? true,
          copyFeatures: false,
        },
      )
      .then((r) => r.data),

  getTenantResolvedMenu: (tenantId: string) =>
    api
      .get<
        ApiResponse<{
          menu: SessionMenuGroupDto[];
          hasCustomMenu: boolean;
          usedPlanMenu: boolean;
          usedGlobalFallback: boolean;
        }>
      >(`/api/superadmin/empresas/${encodeURIComponent(tenantId)}/menu`)
      .then((r) => r.data.responseObject),

  putTenantCustomMenu: (tenantId: string, menuConfigJson: string) =>
    api
      .put<ApiResponse<Record<string, unknown>>>(`/api/superadmin/empresas/${encodeURIComponent(tenantId)}/menu`, {
        menuConfigJson,
      })
      .then((r) => r.data),

  getFuncionalidadesArbol: () =>
    api
      .get<ApiResponse<FuncionalidadArbolDto[]>>('/api/superadmin/funcionalidades/arbol')
      .then((r) => r.data.responseObject ?? []),

  syncFuncionalidadesCatalogo: () =>
    api
      .post<ApiResponse<{ sincronizados: number }>>('/api/superadmin/funcionalidades/sincronizar')
      .then((r) => r.data.responseObject),

  deleteTenantCustomMenu: (tenantId: string) =>
    api
      .delete<ApiResponse<Record<string, unknown>>>(
        `/api/superadmin/empresas/${encodeURIComponent(tenantId)}/menu`,
      )
      .then((r) => r.data),

  /** Endpoint público para landing (sin token). */
  getPublicPlans: () =>
    api.get<ApiResponse<{ plans: SaasPublicPlan[] }>>('/api/public/plans').then((r) => r.data.responseObject.plans),

  switchTenant: (tenantId: string) =>
    api.post<ApiResponse<import('../types/auth').AuthResponse>>('/api/auth/switch-tenant', { tenantId })
      .then((r) => r.data.responseObject),

  getNavigationMenu: () =>
    api
      .get<ApiResponse<{ menu: AdminNavigationMenu }>>('/api/superadmin/navigation-menu')
      .then((r) => r.data.responseObject.menu),

  reorderNavigationGroups: (orderedGroupIds: string[]) =>
    api
      .put<ApiResponse<Record<string, unknown>>>('/api/superadmin/navigation-menu/groups/reorder', {
        orderedGroupIds,
      })
      .then((r) => r.data),

  reorderNavigationItemLevels: (levels: NavItemSiblingOrderLevel[]) =>
    api
      .put<ApiResponse<Record<string, unknown>>>('/api/superadmin/navigation-menu/items/reorder-levels', {
        levels,
      })
      .then((r) => r.data),

  createNavigationMenuItem: (body: CreateNavItemBody) =>
    api
      .post<ApiResponse<{ id: string }>>('/api/superadmin/navigation-menu/items', {
        groupId: body.groupId,
        parentItemId: body.parentItemId,
        routePath: body.routePath.trim(),
        displayLabel: body.displayLabel.trim(),
        moduleKey: body.moduleKey?.trim() || null,
        permissionKey: body.permissionKey?.trim() || null,
      })
      .then((r) => r.data.responseObject.id),

  updateNavigationMenuItem: (itemId: string, body: UpdateNavItemBody) =>
    api
      .put<ApiResponse<Record<string, unknown>>>(
        `/api/superadmin/navigation-menu/items/${encodeURIComponent(itemId)}`,
        {
          displayLabel: body.displayLabel.trim(),
          routePath: body.routePath.trim(),
          moduleKey: body.moduleKey?.trim() || null,
          permissionKey: body.permissionKey?.trim() || null,
          saasFeatureDefinitionId: body.saasFeatureDefinitionId?.trim() || null,
        },
      )
      .then(() => undefined),

  deleteNavigationMenuItem: (itemId: string) =>
    api
      .delete<ApiResponse<Record<string, unknown>>>(
        `/api/superadmin/navigation-menu/items/${encodeURIComponent(itemId)}`,
      )
      .then(() => undefined),
};

