import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';
import type { SessionResponse, SessionMenuGroupDto } from '../../../types/access';
import { readEnvelopePayload } from '../../lib/apiEnvelope';
import { normalizeAuthResponse } from '../../auth/normalizeAuthResponse';
import { PLATFORM_API } from './platformApiPaths';

/** Rutas canónicas Platform Layer (SaaS / subscribers). */
export const PLATFORM_SUBSCRIBERS_API = PLATFORM_API.subscribers;

export function parsePlatformSubscriberList(
  responseObject: PlatformSubscriber[] | { subscribers?: PlatformSubscriber[] } | null | undefined,
): PlatformSubscriber[] {
  if (!responseObject) return [];
  if (Array.isArray(responseObject)) return responseObject;
  if (Array.isArray(responseObject.subscribers)) return responseObject.subscribers;
  return [];
}

export type PlatformSubscriber = {
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

export type PlatformPlanFeatureEntry = {
  featureCode: string;
  featureName: string;
  description: string | null;
  isMetered: boolean;
  kind?: string;
  resourceRef?: string | null;
  isIncluded: boolean;
  limitPerPeriod: number | null;
};

export type PlatformPlanCatalogEntry = {
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
  features: PlatformPlanFeatureEntry[];
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

export type PlatformOverviewMetrics = {
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

export type EffectiveCommercialLimit = {
  limitCode: string;
  limitValue: number;
  periodType: string;
  isHardLimit: boolean;
  commercialPlanId: string;
  planCode?: string | null;
  isUnlimited?: boolean;
};

/** Snapshot de entitlements comerciales (GET /api/platform/subscribers/{id}/entitlements). */
export type SubscriberEntitlementsSnapshot = {
  planCode?: string | null;
  planName?: string | null;
  enabledModules: string[];
  enabledFeatures: string[];
  limits: Record<string, number | null>;
  hasModuleRestrictions: boolean;
  commercialLimits?: Record<string, EffectiveCommercialLimit> | null;
  subscriberId?: string | null;
  billingAccountStatus?: string | null;
  version?: number;
  resolvedAtUtc?: string | null;
};

export type PlatformMetricsSnapshot = {
  subscribers: {
    total: number;
    active: number;
    trial: number;
    gracePeriod: number;
    suspended: number;
    inactive: number;
  };
  plans: { byPlan: Array<{ planCode: string; count: number }> };
  lifecycle: {
    activeCount: number;
    trialCount: number;
    gracePeriodCount: number;
    suspendedCount: number;
    inactiveCount: number;
  };
};

export type PlatformAuditEntry = {
  id: string;
  actorUserId?: string | null;
  actorEmail?: string | null;
  action: string;
  targetSubscriberId?: string | null;
  resourceType?: string | null;
  resourceId?: string | null;
  notes?: string | null;
  createdAtUtc: string;
};

export type PlatformUserRow = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  platformRole?: string | null;
  isActive: boolean;
};

export type LegacyEndpointUsageSummary = {
  endpoint: string;
  method: string;
  totalHits: number;
  lastHitUtc?: string | null;
  successor?: string | null;
  lastCallerIp?: string | null;
};

export type LegacyEndpointUsageDashboard = {
  topEndpoints: LegacyEndpointUsageSummary[];
  topUiRoutes?: Array<{ routeKey: string; totalHits: number; lastHitUtc?: string | null; lastReferrer?: string | null }>;
  topMasterDataSources?: Array<{ sourceKey: string; totalHits: number; lastHitUtc?: string | null; lastDetail?: string | null }>;
  recentHits: Array<{
    endpoint: string;
    method: string;
    successor?: string | null;
    atUtc: string;
    callerIp?: string | null;
  }>;
  totalEndpointHits?: number;
  totalUiHits?: number;
  totalMasterDataHits?: number;
  totalHits: number;
  migrationCompletePercent?: number;
};

/** Platform Control Plane HTTP client — canonical `/api/platform/*` only. */
export const platformService = {
  getSubscribers: () =>
    api
      .get<ApiResponse<PlatformSubscriber[]>>(PLATFORM_API.subscribers)
      .then((r) => parsePlatformSubscriberList(r.data.responseObject)),

  createSubscriberWithAdmin: (body: CreateSubscriberWithAdminBody) =>
    api
      .post<ApiResponse<SessionResponse>>(PLATFORM_API.subscribers, body)
      .then((r) => r.data.responseObject),

  getSubscriberEntitlements: (subscriberId: string) =>
    api
      .get<ApiResponse<SubscriberEntitlementsSnapshot>>(
        `${PLATFORM_API.subscribers}/${encodeURIComponent(subscriberId)}/entitlements`,
      )
      .then((r) => r.data.responseObject),

  activateSubscriber: (subscriberId: string, notes?: string) =>
    api
      .patch<ApiResponse<unknown>>(
        `${PLATFORM_API.subscribers}/${encodeURIComponent(subscriberId)}/activate`,
        notes ? { notes } : {},
      )
      .then((r) => r.data),

  suspendSubscriber: (subscriberId: string, reason?: string) =>
    api
      .patch<ApiResponse<unknown>>(
        `${PLATFORM_API.subscribers}/${encodeURIComponent(subscriberId)}/suspend`,
        reason ? { reason } : {},
      )
      .then((r) => r.data),

  changePlan: (subscriberId: string, newPlanCode: string, notes?: string) =>
    api
      .patch<ApiResponse<unknown>>(
        `${PLATFORM_API.subscribers}/${encodeURIComponent(subscriberId)}/plan`,
        { newPlanCode, ...(notes ? { notes } : {}) },
      )
      .then((r) => r.data),

  /** @deprecated Usar changePlan — módulos pendientes de endpoint platform. */
  updateSubscriberSubscription: async (subscriberId: string, body: UpdateSubscriberSubscriptionBody) => {
    if (body.planCode?.trim()) {
      await api.patch(`${PLATFORM_API.subscribers}/${encodeURIComponent(subscriberId)}/plan`, {
        newPlanCode: body.planCode.trim(),
      });
    }
  },

  /** Overview KPIs derivados de platform metrics + listado suscriptores. */
  getMetrics: async (): Promise<PlatformOverviewMetrics> => {
    const [metricsRes, subsRes] = await Promise.all([
      api.get<ApiResponse<PlatformMetricsSnapshot>>(PLATFORM_API.metrics),
      api.get<ApiResponse<PlatformSubscriber[]>>(PLATFORM_API.subscribers),
    ]);
    const platform = metricsRes.data.responseObject;
    const subs = parsePlatformSubscriberList(subsRes.data.responseObject);
    const totalUsers = subs.reduce((acc, s) => acc + (s.totalUsers ?? 0), 0);
    const activeUsers = subs.reduce((acc, s) => acc + (s.activeUsers ?? 0), 0);
    const recent = [...subs]
      .sort((a, b) => (b.createdAt ?? '').localeCompare(a.createdAt ?? ''))
      .slice(0, 8)
      .map((s) => ({
        id: s.id,
        name: s.name,
        slug: s.slug,
        isActive: s.isActive,
        createdAt: s.createdAt,
      }));
    return {
      totals: {
        totalSubscribers: platform?.subscribers.total ?? subs.length,
        activeSubscribers: platform?.subscribers.active ?? subs.filter((s) => s.isActive).length,
        totalUsers,
        activeUsers,
      },
      recentSubscribers: recent,
    };
  },

  getPlatformMetrics: () =>
    api
      .get<ApiResponse<PlatformMetricsSnapshot>>(PLATFORM_API.metrics)
      .then((r) => r.data.responseObject),

  getPlatformAudit: (limit = 100, subscriberId?: string) => {
    const params: Record<string, string | number> = { limit };
    if (subscriberId) params.subscriberId = subscriberId;
    return api
      .get<ApiResponse<{ entries: PlatformAuditEntry[] }>>(PLATFORM_API.audit, { params })
      .then((r) => r.data.responseObject?.entries ?? []);
  },

  getGrowthAnalytics: (from: string, to: string, granularity?: string) => {
    const params: Record<string, string> = { from, to };
    if (granularity) params.granularity = granularity;
    return api
      .get<ApiResponse<GrowthAnalyticsResponse>>(`${PLATFORM_API.metrics}/growth-analytics`, { params })
      .then((r) => r.data.responseObject);
  },

  getGrowthMonetaryAnalytics: (from: string, to: string, granularity?: string) => {
    const params: Record<string, string> = { from, to };
    if (granularity) params.granularity = granularity;
    return api
      .get<ApiResponse<GrowthMonetaryResponse>>(`${PLATFORM_API.metrics}/growth-analytics-monetary`, { params })
      .then((r) => r.data.responseObject);
  },

  getPlansCatalog: () =>
    api.get<ApiResponse<{ plans: PlatformPlanCatalogEntry[] }>>(PLATFORM_API.plans)
      .then((r) => r.data.responseObject.plans),

  /** Catálogo administrable (CRUD); canonical: /api/platform/plans. */
  listCommercialPlansAdmin: () =>
    api.get<ApiResponse<{ plans: CommercialPlanAdmin[] }>>(PLATFORM_API.plans).then((r) => r.data.responseObject.plans),

  createCommercialPlan: (body: CreateCommercialPlanBody) =>
    api.post<ApiResponse<{ id: string }>>(PLATFORM_API.plans, body).then((r) => r.data.responseObject.id),

  updateCommercialPlan: (planId: string, body: UpdateCommercialPlanBody) =>
    api.put<ApiResponse<Record<string, unknown>>>(`${PLATFORM_API.plans}/${planId}`, body).then((r) => r.data),

  deleteCommercialPlan: (planId: string) =>
    api.delete<ApiResponse<Record<string, unknown>>>(`${PLATFORM_API.plans}/${planId}`).then((r) => r.data),

  reorderCommercialPlans: (orderedPlanIds: string[]) =>
    api.put<ApiResponse<Record<string, unknown>>>(`${PLATFORM_API.plans}/reorder`, { orderedPlanIds }).then((r) => r.data),

  setCommercialPlanRecommended: (planId: string) =>
    api.put<ApiResponse<Record<string, unknown>>>(`${PLATFORM_API.plans}/${planId}/recommended`).then((r) => r.data),

  getPlanMenu: (planId: string) =>
    api
      .get<ApiResponse<PlanMenuRead>>(`${PLATFORM_API.plans}/${encodeURIComponent(planId)}/menu`)
      .then((r) => r.data.responseObject),

  setPlanMenuJson: (planId: string, menuConfigJson: string | null, menuSidebarLayout?: string | null) =>
    api
      .put<ApiResponse<Record<string, unknown>>>(`${PLATFORM_API.plans}/${encodeURIComponent(planId)}/menu`, {
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
        `${PLATFORM_API.plans}/${encodeURIComponent(targetPlanId)}/copy-from/${encodeURIComponent(sourcePlanId)}`,
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
      .get<ApiResponse<FuncionalidadArbolDto[]>>(`${PLATFORM_API.features}/tree`)
      .then((r) => r.data.responseObject ?? []),

  syncFuncionalidadesCatalogo: () =>
    api
      .post<ApiResponse<{ synced: number }>>(`${PLATFORM_API.features}/sync`)
      .then((r) => r.data.responseObject),

  getNavigationMenu: () =>
    api
      .get<ApiResponse<{ menu: AdminNavigationMenu }>>(PLATFORM_API.navigationMenu)
      .then((r) => r.data.responseObject.menu),

  reorderNavigationGroups: (orderedGroupIds: string[]) =>
    api
      .put<ApiResponse<Record<string, unknown>>>(`${PLATFORM_API.navigationMenu}/groups/reorder`, {
        orderedGroupIds,
      })
      .then((r) => r.data),

  reorderNavigationItemLevels: (levels: NavItemSiblingOrderLevel[]) =>
    api
      .put<ApiResponse<Record<string, unknown>>>(`${PLATFORM_API.navigationMenu}/items/reorder-levels`, {
        levels,
      })
      .then((r) => r.data),

  createNavigationMenuItem: (body: CreateNavItemBody) =>
    api
      .post<ApiResponse<{ id: string }>>(`${PLATFORM_API.navigationMenu}/items`, {
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
        `${PLATFORM_API.navigationMenu}/items/${encodeURIComponent(itemId)}`,
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
        `${PLATFORM_API.navigationMenu}/items/${encodeURIComponent(itemId)}`,
      )
      .then(() => undefined),

  getSubscriberTenantUsers: (subscriberId: string) =>
    api
      .get<ApiResponse<{ users: Array<{ id: string; email: string; firstName: string; lastName: string; isActive: boolean; userType: string }> }>>(
        `${PLATFORM_API.subscribers}/${encodeURIComponent(subscriberId)}/users`,
      )
      .then((r) => r.data.responseObject?.users ?? []),

  getPlatformBillingSummary: () =>
    api
      .get<ApiResponse<{ totals: { subscribers: number; suspended: number; gracePeriod: number; trial: number; overdueAccounts: number }; note?: string }>>(
        `${PLATFORM_API.billing}/summary`,
      )
      .then((r) => r.data.responseObject),

  getPlatformUsers: () =>
    api
      .get<ApiResponse<{ users: PlatformUserRow[]; activePlatformUsers: number }>>(PLATFORM_API.users)
      .then((r) => r.data.responseObject),

  revokePlatformUserSessions: (userId: string, subscriberId: string) =>
    api
      .delete<ApiResponse<string>>(`${PLATFORM_API.users}/${encodeURIComponent(userId)}/sessions`, {
        params: { subscriberId },
      })
      .then((r) => r.data.responseObject),

  getLegacyEndpointUsage: () =>
    api
      .get<ApiResponse<LegacyEndpointUsageDashboard>>(`${PLATFORM_API.observability}/legacy-endpoints`)
      .then((r) => r.data.responseObject),

  getObservabilityHealthIndex: () =>
    api
      .get<ApiResponse<{ healthChecks: string[]; prometheus: string | null; correlationHeader: string }>>(
        `${PLATFORM_API.observability}/health-index`,
      )
      .then((r) => r.data.responseObject),

  getObservabilityDashboard: () =>
    api
      .get<ApiResponse<Record<string, unknown>>>(`${PLATFORM_API.observability}/dashboard`)
      .then((r) => r.data.responseObject),

  getPlatformBillingInvoices: (take = 50) =>
    api
      .get<ApiResponse<{ invoices: Array<Record<string, unknown>> }>>(`${PLATFORM_API.billing}/invoices`, {
        params: { take },
      })
      .then((r) => r.data.responseObject?.invoices ?? []),

  getPlatformBillingOverdue: () =>
    api
      .get<ApiResponse<{ accounts: Array<Record<string, unknown>> }>>(`${PLATFORM_API.billing}/overdue`)
      .then((r) => r.data.responseObject?.accounts ?? []),

  getPlatformImpersonationHistory: () =>
    api
      .get<ApiResponse<{ events: Array<Record<string, unknown>> }>>(`${PLATFORM_API.users}/impersonation-history`)
      .then((r) => r.data.responseObject?.events ?? []),

  deleteSubscriberCustomMenu: (subscriberId: string) =>
    api
      .delete<ApiResponse<Record<string, unknown>>>(
        `${PLATFORM_SUBSCRIBERS_API}/${encodeURIComponent(subscriberId)}/menu`,
      )
      .then((r) => r.data),

  getPublicPlans: () =>
    api.get<ApiResponse<{ plans: SaasPublicPlan[] }>>('/api/public/plans').then((r) => r.data.responseObject.plans),

  switchSubscriber: async (subscriberId: string) => {
    const trimmed = subscriberId.trim();
    if (!trimmed) throw new Error('Identificador de suscriptor inválido.');

    const res = await api.post<ApiResponse<Record<string, unknown>>>(
      '/api/auth/switch-subscriber',
      { subscriberId: trimmed },
    );

    const envelope = res.data;
    if (envelope && envelope.success === false) {
      throw new Error(envelope.message?.trim() || 'No se pudo cambiar de suscriptor.');
    }

    const session = normalizeAuthResponse(readEnvelopePayload<Record<string, unknown> | null>(envelope));
    if (!session.token) throw new Error('La sesión no incluyó token de acceso.');
    return session;
  },
};

