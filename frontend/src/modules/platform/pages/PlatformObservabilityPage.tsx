import { useCallback, useEffect, useState } from 'react';
import { platformService, type PlatformMetricsSnapshot } from '../api/platformService';
import { PlatformCrudTemplate } from '../../../templates/PlatformCrudTemplate';
import { LoadingState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { StatCard } from '../../../components/zh/StatCard';

export function PlatformObservabilityPage() {
  const [metrics, setMetrics] = useState<PlatformMetricsSnapshot | null>(null);
  const [healthIndex, setHealthIndex] = useState<{ healthChecks: string[]; prometheus: string | null } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    Promise.all([
      platformService.getPlatformMetrics(),
      platformService.getObservabilityHealthIndex(),
    ])
      .then(([m, h]) => {
        setMetrics(m ?? null);
        setHealthIndex(h ?? null);
      })
      .catch(() => setError('No se pudieron cargar datos de observability.'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { load(); }, [load]);

  return (
    <PlatformCrudTemplate title="Observability" subtitle="Métricas platform y health checks">
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
        </>
      )}
    </PlatformCrudTemplate>
  );
}
