/** Respuesta de GET /api/public/deployment (sin Bearer). */
export type DeploymentInfo = {
  superAdminPanelEnabled: boolean;
  /** Tope de empresas activas en la instancia; null/undefined = sin límite declarado. */
  maxActiveTenants?: number | null;
  /** Tope de usuarios globales (Identity); null/undefined = sin límite declarado. */
  maxIdentityUsers?: number | null;
  /** Instancia dedicada a un solo cliente: no se permiten empresas/RUC ilimitadas. */
  dedicatedSingleClientInstance?: boolean;
  /** Tope de usuarios con membresía activa por empresa (tenant); null = sin límite declarado. */
  maxUsersPerTenant?: number | null;
};

let cached: DeploymentInfo | null = null;
let inflight: Promise<DeploymentInfo> | null = null;

function resolveApiBase(): string {
  const v = (import.meta.env.VITE_API_URL as string | undefined)?.trim() ?? '';
  return v.length > 0 ? v : '';
}

/**
 * Obtiene la configuración pública del despliegue (cacheada en memoria por pestaña).
 */
export async function fetchDeploymentConfig(): Promise<DeploymentInfo> {
  if (cached) return cached;
  if (!inflight) {
    inflight = (async () => {
      const base = resolveApiBase();
      const url = `${base}/api/public/deployment`;
      const res = await fetch(url, { credentials: 'omit' });
      if (!res.ok) {
        cached = {
          superAdminPanelEnabled: true,
          maxActiveTenants: null,
          maxIdentityUsers: null,
          dedicatedSingleClientInstance: false,
          maxUsersPerTenant: null,
        };
        return cached;
      }
      const json: unknown = await res.json();
      const obj =
        json && typeof json === 'object'
          ? (json as { responseObject?: Record<string, unknown> }).responseObject
          : undefined;
      const enabled =
        obj && typeof obj === 'object' && typeof obj.superAdminPanelEnabled === 'boolean'
          ? obj.superAdminPanelEnabled
          : true;
      let maxTenants: number | null | undefined;
      if (obj && typeof obj === 'object' && 'maxActiveTenants' in obj) {
        const m = obj.maxActiveTenants;
        maxTenants = typeof m === 'number' && Number.isFinite(m) ? m : null;
      }
      let maxUsers: number | null | undefined;
      if (obj && typeof obj === 'object' && 'maxIdentityUsers' in obj) {
        const u = obj.maxIdentityUsers;
        maxUsers = typeof u === 'number' && Number.isFinite(u) ? u : null;
      }
      const dedicated =
        obj && typeof obj === 'object' && typeof obj.dedicatedSingleClientInstance === 'boolean'
          ? obj.dedicatedSingleClientInstance
          : false;
      let maxPerTenant: number | null | undefined;
      if (obj && typeof obj === 'object' && 'maxUsersPerTenant' in obj) {
        const p = obj.maxUsersPerTenant;
        maxPerTenant = typeof p === 'number' && Number.isFinite(p) ? p : null;
      }
      cached = {
        superAdminPanelEnabled: enabled,
        maxActiveTenants: maxTenants ?? null,
        maxIdentityUsers: maxUsers ?? null,
        dedicatedSingleClientInstance: dedicated,
        maxUsersPerTenant: maxPerTenant ?? null,
      };
      return cached;
    })();
  }
  try {
    return await inflight;
  } finally {
    inflight = null;
  }
}

export function getDeploymentConfigCached(): DeploymentInfo | null {
  return cached;
}

/** Tras cambiar cuotas en servidor, invalida la caché para el próximo `fetchDeploymentConfig`. */
export function invalidateDeploymentConfigCache(): void {
  cached = null;
  inflight = null;
}
