import { useCallback, useEffect, useState } from 'react';
import { platformService, type LegacyEndpointUsageDashboard, type PlatformMetricsSnapshot } from '../api/platformService';
import { PlatformCrudTemplate } from '../../../templates/PlatformCrudTemplate';
import { LoadingState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { StatCard } from '../../../components/zh/StatCard';

export function PlatformObservabilityPage() {
  const [metrics, setMetrics] = useState<PlatformMetricsSnapshot | null>(null);
  const [legacy, setLegacy] = useState<LegacyEndpointUsageDashboard | null>(null);
  const [healthIndex, setHealthIndex] = useState<{ healthChecks: string[]; prometheus: string | null } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    Promise.all([
      platformService.getPlatformMetrics(),
      platformService.getLegacyEndpointUsage(),
      platformService.getObservabilityHealthIndex(),
    ])
      .then(([m, l, h]) => {
        setMetrics(m ?? null);
        setLegacy(l ?? null);
        setHealthIndex(h ?? null);
      })
      .catch(() => setError('No se pudieron cargar datos de observability.'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { load(); }, [load]);

  return (
    <PlatformCrudTemplate title="Observability" subtitle="Métricas platform, health checks y uso de endpoints legacy">
      {error && <ZHPageNotice variant="error" message={error} />}
      {loading ? <LoadingState /> : (
        <>
          {metrics && (
            <div className="pg-stat-grid">
              <StatCard label="Suscriptores" value={String(metrics.subscribers.total)} />
              <StatCard label="Activos" value={String(metrics.subscribers.active)} />
              <StatCard label="Trial" value={String(metrics.subscribers.trial)} />
              <StatCard label="Gracia" value={String(metrics.subscribers.gracePeriod)} />
              <StatCard label="Suspendidos" value={String(metrics.subscribers.suspended)} />
            </div>
          )}
          {healthIndex && (
            <div className="pg-section">
              <h3 className="pg-section-label">Health &amp; metrics</h3>
              <ul className="subtle">
                {healthIndex.healthChecks.map((h) => <li key={h}>{h}</li>)}
                {healthIndex.prometheus && <li>Prometheus: {healthIndex.prometheus}</li>}
              </ul>
            </div>
          )}
          {legacy && (
            <div className="pg-section">
              <h3 className="pg-section-label">
                Legacy strangler ({legacy.totalHits} hits · migración ~{legacy.migrationCompletePercent ?? 0}%)
              </h3>
              <div className="pg-overflow-x">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Método</th>
                      <th>Endpoint</th>
                      <th>Hits</th>
                      <th>Último acceso</th>
                      <th>Sucesor</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(legacy.topEndpoints ?? []).map((row) => (
                      <tr key={`${row.method}-${row.endpoint}`}>
                        <td className="mono">{row.method}</td>
                        <td className="mono">{row.endpoint}</td>
                        <td className="mono">{row.totalHits}</td>
                        <td className="subtle">{row.lastHitUtc ?? '—'}</td>
                        <td className="subtle">{row.successor ?? '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {(legacy.topUiRoutes?.length ?? 0) > 0 && (
                <p className="subtle pg-mt-md">
                  UI legacy: {(legacy.topUiRoutes ?? []).map((r) => `${r.routeKey} (${r.totalHits})`).join(', ')}
                </p>
              )}
              {(legacy.topMasterDataSources?.length ?? 0) > 0 && (
                <p className="subtle">
                  MasterData fallback: {(legacy.topMasterDataSources ?? []).map((r) => `${r.sourceKey} (${r.totalHits})`).join(', ')}
                </p>
              )}
            </div>
          )}
        </>
      )}
    </PlatformCrudTemplate>
  );
}
