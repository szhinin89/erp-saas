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
  settings: '/api/platform/settings',
} as const;

/** Canonical Platform Control Plane UI routes (shell at `/superadmin/*`). */
export const PLATFORM_UI = {
  overview: '/superadmin/overview',
  subscribers: '/superadmin/subscribers',
  subscriberDetail: (id: string) => `/superadmin/subscribers/${encodeURIComponent(id.trim())}`,
  plans: '/superadmin/plans',
  users: '/superadmin/users',
  billing: '/superadmin/billing',
  observability: '/superadmin/observability',
  audit: '/superadmin/audit',
} as const;
