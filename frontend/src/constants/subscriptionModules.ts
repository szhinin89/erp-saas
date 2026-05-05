/** Claves de módulo contratables; deben coincidir con `TenantSubscriptionCatalog` en el backend. */
export const TENANT_MODULE_KEYS = ['access', 'accounting', 'inventario', 'saas', 'ventas'] as const;

export type TenantModuleKey = (typeof TENANT_MODULE_KEYS)[number];
