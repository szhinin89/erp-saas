/** Respuesta de GET /api/public/deployment (sin Bearer). */
export type DeploymentInfo = {
  superAdminPanelEnabled: boolean;
  maxActiveSubscribers?: number | null;
  maxIdentityUsers?: number | null;
  dedicatedSingleClientInstance?: boolean;
  maxUsersPerSubscriber?: number | null;
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
          maxActiveSubscribers: null,
          maxIdentityUsers: null,
          dedicatedSingleClientInstance: false,
          maxUsersPerSubscriber: null,
        };
        return cached;
      }
      const json: unknown = await res.json();
      const obj =
        json && typeof json === 'object'
          ? (json as { responseObject?: Record<string, unknown> }).responseObject
          : undefined;

      const readNum = (key: string): number | null => {
        if (!obj || typeof obj !== 'object' || !(key in obj)) return null;
        const v = obj[key];
        return typeof v === 'number' && Number.isFinite(v) ? v : null;
      };

      const enabled =
        obj && typeof obj === 'object' && typeof obj.superAdminPanelEnabled === 'boolean'
          ? obj.superAdminPanelEnabled
          : true;
      const dedicated =
        obj && typeof obj === 'object' && typeof obj.dedicatedSingleClientInstance === 'boolean'
          ? obj.dedicatedSingleClientInstance
          : false;

      cached = {
        superAdminPanelEnabled: enabled,
        maxActiveSubscribers: readNum('maxActiveSubscribers'),
        maxIdentityUsers: readNum('maxIdentityUsers'),
        dedicatedSingleClientInstance: dedicated,
        maxUsersPerSubscriber: readNum('maxUsersPerSubscriber'),
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

export function invalidateDeploymentConfigCache(): void {
  cached = null;
  inflight = null;
}
