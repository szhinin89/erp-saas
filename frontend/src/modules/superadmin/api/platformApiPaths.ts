/** Canonical Platform Control Plane API paths (English technical naming). */
export const PLATFORM_API = {
  auth: '/api/platform/auth',
  subscribers: '/api/platform/subscribers',
  plans: '/api/platform/plans',
  users: '/api/platform/users',
  metrics: '/api/platform/metrics',
  audit: '/api/platform/audit',
  config: '/api/platform/config',
  billing: '/api/platform/billing',
  observability: '/api/platform/observability',
  navigationMenu: '/api/platform/navigation-menu',
  features: '/api/platform/features',
} as const;

/** Legacy routes kept for compatibility — emit Deprecation headers from backend. */
export const LEGACY_PLATFORM_API = {
  superadmin: '/api/superadmin',
  iamSuperadminSubscribers: '/api/admin/iam/superadmin/subscribers',
  subscriberSubscription: '/api/subscribers',
  superadminConfig: '/api/superadmin/config',
  authSuperadminLogin: '/api/auth/superadmin-login',
} as const;

/** Canonical Super Admin UI routes. */
export const SUPERADMIN_UI = {
  overview: '/superadmin/overview',
  subscribers: '/superadmin/subscribers',
  subscriberDetail: (id: string) => `/superadmin/subscribers/${encodeURIComponent(id.trim())}`,
  plans: '/superadmin/plans',
  users: '/superadmin/users',
  billing: '/superadmin/billing',
  observability: '/superadmin/observability',
  audit: '/superadmin/audit',
} as const;

/** Legacy UI routes → canonical targets (for redirects). */
export const LEGACY_SUPERADMIN_UI_REDIRECTS: Record<string, string> = {
  '/companies': SUPERADMIN_UI.subscribers,
  '/superadmin/companies': SUPERADMIN_UI.subscribers,
  '/superadmin/menu-plans': `${SUPERADMIN_UI.plans}?tab=menu`,
  '/superadmin/menu-builder': `${SUPERADMIN_UI.plans}?tab=menu`,
  '/superadmin/navigation-menu': `${SUPERADMIN_UI.plans}?tab=menu`,
  '/superadmin/features': `${SUPERADMIN_UI.plans}?tab=plans`,
  '/superadmin/growth': SUPERADMIN_UI.overview,
  '/superadmin/forms': SUPERADMIN_UI.overview,
};
