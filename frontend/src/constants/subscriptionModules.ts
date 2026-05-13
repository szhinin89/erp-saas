/** Claves de módulo contratables; deben coincidir con `TenantSubscriptionCatalog.AllModuleKeys` en el backend. */
export const TENANT_MODULE_KEYS = [
  'access',
  'accounting',
  'compras',
  'gastos',
  'inventario',
  'rrhh',
  'saas',
  'ventas',
] as const;

export type TenantModuleKey = (typeof TENANT_MODULE_KEYS)[number];
