/** Snapshot de `GET /api/saas/entitlements/me` — única fuente para gating de módulos en UI. */
export interface EntitlementsSnapshot {
  planCode: string | null;
  planName: string | null;
  enabledModules: string[];
  enabledFeatures: string[];
  limits: Record<string, number | null>;
  hasModuleRestrictions: boolean;
}
