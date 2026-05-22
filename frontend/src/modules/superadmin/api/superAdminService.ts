import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';
import type { SessionResponse, SessionMenuGroupDto } from '../../../types/access';
import { readEnvelopePayload } from '../../lib/apiEnvelope';
import { normalizeAuthResponse } from '../../auth/normalizeAuthResponse';
import type { AuthResponse } from '../../../types/auth';

/** Rutas canónicas Platform Layer (SaaS / subscribers). */
export const PLATFORM_SUBSCRIBERS_API = '/api/platform/subscribers';

export function parsePlatformSubscriberList(
  responseObject: SuperAdminSubscriber[] | { subscribers?: SuperAdminSubscriber[] } | null | undefined,
): SuperAdminSubscriber[] {
  if (!responseObject) return [];
  if (Array.isArray(responseObject)) return responseObject;
  if (Array.isArray(responseObject.subscribers)) return responseObject.subscribers;
  return [];
}

export type SuperAdminSubscriber = {
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

/** Coincide con PlatformFeatureKind en backend (0–3). */
export type PlatformFeatureKind = 0 | 1 | 2 | 3;

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

export type CommercialPlanFeatureAdmin = {
  featureId: string;
  featureCode: string;
  featureName: string;
  isMetered: boolean;
  kind: PlatformFeatureKind;
  resourceRef: string | null;
  isIncluded: boolean;
  limitPerPeriod: number | null;
};

export type CommercialPlanAdmin = {
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
  features: CommercialPlanFeatureAdmin[];
};

export type PlanMenuRead = {
  menuConfigJson: string | null;
  menuSidebarLayout: string;
};

export type CreateCommercialPlanBody = {
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

export type UpdateCommercialPlanBody = {
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

export type CreateSubscriberWithAdminBody = {
  subscriberName: string;
  subscriberSlug: string;
  adminFirstName: string;
  adminLastName: string;
  adminEmail: string;
  adminPassword: string;
  /** RUC ecuatoriano (13 dígitos). Opcional: la API genera TMP-EC-* provisional. */
  ruc?: string | null;
  /** ISO 3166-1 alpha-3, p. ej. ECU */
  countryCode?: string | null;
  /** IANA timezone, p. ej. America/Guayaquil */
  timezone?: string | null;
  /** Coincide con `PasswordResetMode` en backend: 0 Disabled, 1 Direct, 2 Email, 3 Phone. */
  passwordResetMode?: number;
  /** Si true, no crea usuario; vincula un Admin existente por email. */
  linkExistingAdmin?: boolean;
  /** Código de plan SaaS (catálogo). Opcional. */
  planCode?: string | null;
  /** Si se envía lista no vacía, restringe módulos; si se omite o [], sin JSON de restricción (todos). */
  enabledModules?: string[] | null;
};

export type UpdateSubscriberSubscriptionBody = {
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
  name: string;
  icon: string | null;
  path: string | null;
  permission: string;
  children: FuncionalidadArbolDto[];
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
    totalSubscribers: number;
    activeSubscribers: number;
    totalUsers: number;
    activeUsers: number;
  };
  recentSubscribers: Array<{
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
  newSubscribers: number;
  newIdentityUsers: number;
  newCompanyUserMemberships: number;
  cumulativeSubscribers: number;
  cumulativeIdentityUsers: number;
  cumulativeCompanyUserMemberships: number;
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
  getSubscribers: () =>
    api
      .get<ApiResponse<SuperAdminSubscriber[]>>('/api/platform/subscribers')
      .then((r) => parsePlatformSubscriberList(r.data.responseObject)),

  createSubscriberWithAdmin: (body: CreateSubscriberWithAdminBody) =>
    api
      .post<ApiResponse<SessionResponse>>('/api/platform/subscribers', body)
      .then((r) => r.data.responseObject),

  updateSubscriberSubscription: (subscriberId: string, body: UpdateSubscriberSubscriptionBody) =>
    api
      .patch<ApiResponse<unknown>>(`/api/subscribers/${encodeURIComponent(subscriberId)}/subscription`, body)
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
  listCommercialPlansAdmin: () =>
    api.get<ApiResponse<{ plans: CommercialPlanAdmin[] }>>('/api/superadmin/commercial-plans').then((r) => r.data.responseObject.plans),

  createCommercialPlan: (body: CreateCommercialPlanBody) =>
    api.post<ApiResponse<{ id: string }>>('/api/superadmin/commercial-plans', body).then((r) => r.data.responseObject.id),

  updateCommercialPlan: (planId: string, body: UpdateCommercialPlanBody) =>
    api.put<ApiResponse<Record<string, unknown>>>(`/api/superadmin/commercial-plans/${planId}`, body).then((r) => r.data),

  deleteCommercialPlan: (planId: string) =>
    api.delete<ApiResponse<Record<string, unknown>>>(`/api/superadmin/commercial-plans/${planId}`).then((r) => r.data),

  reorderCommercialPlans: (orderedPlanIds: string[]) =>
    api.put<ApiResponse<Record<string, unknown>>>('/api/superadmin/commercial-plans/reorder', { orderedPlanIds }).then((r) => r.data),

  setCommercialPlanRecommended: (planId: string) =>
    api.put<ApiResponse<Record<string, unknown>>>(`/api/superadmin/commercial-plans/${planId}/recommended`).then((r) => r.data),

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
        `/api/superadmin/commercial-plans/${encodeURIComponent(targetPlanId)}/copy-from/${encodeURIComponent(sourcePlanId)}`,
        {
          copyMenu: opts?.copyMenu ?? true,
          copyFeatures: false,
        },
      )
      .then((r) => r.data),

  getSubscriberResolvedMenu: (subscriberId: string) =>
    api
      .get<
        ApiResponse<{
          menu: SessionMenuGroupDto[];
          hasCustomMenu: boolean;
          usedPlanMenu: boolean;
          usedGlobalFallback: boolean;
        }>
      >(`${PLATFORM_SUBSCRIBERS_API}/${encodeURIComponent(subscriberId)}/menu`)
      .then((r) => r.data.responseObject),

  putSubscriberCustomMenu: (subscriberId: string, menuConfigJson: string) =>
    api
      .put<ApiResponse<Record<string, unknown>>>(
        `${PLATFORM_SUBSCRIBERS_API}/${encodeURIComponent(subscriberId)}/menu`,
        { menuConfigJson },
      )
      .then((r) => r.data),

  getFuncionalidadesArbol: () =>
    api
      .get<ApiResponse<FuncionalidadArbolDto[]>>('/api/superadmin/AppFeatures/arbol')
      .then((r) => r.data.responseObject ?? []),

  syncFuncionalidadesCatalogo: () =>
    api
      .post<ApiResponse<{ sincronizados: number }>>('/api/superadmin/AppFeatures/sincronizar')
      .then((r) => r.data.responseObject),

  deleteSubscriberCustomMenu: (subscriberId: string) =>
    api
      .delete<ApiResponse<Record<string, unknown>>>(
        `${PLATFORM_SUBSCRIBERS_API}/${encodeURIComponent(subscriberId)}/menu`,
      )
      .then((r) => r.data),

  /** Endpoint público para landing (sin token). */
  getPublicPlans: () =>
    api.get<ApiResponse<{ plans: SaasPublicPlan[] }>>('/api/public/plans').then((r) => r.data.responseObject.plans),

  switchSubscriber: async (subscriberId: string) => {
    const trimmed = subscriberId.trim();
    if (!trimmed) {
      throw new Error('Identificador de suscriptor inválido.');
    }

    const res = await api.post<ApiResponse<Record<string, unknown>>>(
      '/api/auth/switch-subscriber',
      { subscriberId: trimmed },
    );

    const envelope = res.data;
    if (envelope && envelope.success === false) {
      throw new Error(envelope.message?.trim() || 'No se pudo cambiar de suscriptor.');
    }

    const session = normalizeAuthResponse(readEnvelopePayload<Record<string, unknown> | null>(envelope));
    if (!session.token) {
      throw new Error('La sesión no incluyó token de acceso.');
    }

    return session;
  },

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

