/** Claves canónicas (inglés); alineadas con `SubscriberSubscriptionCatalog.CanonicalModuleKeys` en el backend. */
export const SUBSCRIBER_MODULE_KEYS = [
  'access',
  'accounting',
  'expenses',
  'inventory',
  'logistics',
  'payroll',
  'purchases',
  'sales',
  'saas',
] as const;

/** Alias legacy (español) → clave canónica para comparar con API. */
export const LEGACY_MODULE_KEY_ALIASES: Record<string, SubscriberModuleKey> = {
  ventas: 'sales',
  inventario: 'inventory',
  compras: 'purchases',
  gastos: 'expenses',
  logistica: 'logistics',
  rrhh: 'payroll',
};

export function normalizeModuleKey(key: string): string {
  const k = key.trim().toLowerCase();
  return LEGACY_MODULE_KEY_ALIASES[k] ?? k;
}

export function moduleKeysMatch(apiKey: string, canonicalKey: string): boolean {
  return normalizeModuleKey(apiKey) === normalizeModuleKey(canonicalKey);
}

export type SubscriberModuleKey = (typeof SUBSCRIBER_MODULE_KEYS)[number];

/** @deprecated Use SUBSCRIBER_MODULE_KEYS */
export const TENANT_MODULE_KEYS = SUBSCRIBER_MODULE_KEYS;

/** @deprecated Use SubscriberModuleKey */
export type TenantModuleKey = SubscriberModuleKey;
