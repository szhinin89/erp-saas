/** Claves de módulo contratables; deben coincidir con `TenantSubscriptionCatalog` en el backend. */
export const TENANT_MODULE_KEYS = ['catalog', 'accounting', 'saas', 'access'] as const;

export type TenantModuleKey = (typeof TENANT_MODULE_KEYS)[number];
