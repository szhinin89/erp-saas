import { useCallback, useEffect, useState } from 'react';
import { superAdminService, type PlatformAuditEntry } from '../api/superAdminService';
import { SuperAdminCrudTemplate } from '../../../templates/SuperAdminCrudTemplate';
import { LoadingState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';

export function SuperAdminAuditPage() {
  const [entries, setEntries] = useState<PlatformAuditEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    superAdminService
      .getPlatformAudit(200)
      .then((rows) => setEntries(rows ?? []))
      .catch(() => setError('No se pudo cargar la auditoría platform.'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <SuperAdminCrudTemplate
      title="Audit log"
      subtitle="Eventos append-only del control plane (GET /api/platform/audit)."
    >
      <div className="pg-section-header">
        <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" onClick={load}>
          <span className="material-symbols-outlined pg-icon-16">refresh</span>
          Actualizar
        </button>
      </div>
      {error && <ZHPageNotice variant="error" message={error} />}
      {loading ? (
        <LoadingState />
      ) : entries.length === 0 ? (
        <p className="subtle">Sin eventos registrados.</p>
      ) : (
        <div className="pg-overflow-x">
          <table className="table">
            <thead>
              <tr>
                <th>Fecha (UTC)</th>
                <th>Acción</th>
                <th>Actor</th>
                <th>Suscriptor</th>
                <th>Notas</th>
              </tr>
            </thead>
            <tbody>
              {entries.map((e) => (
                <tr key={e.id}>
                  <td className="mono subtle">{new Date(e.createdAtUtc).toISOString()}</td>
                  <td><span className="badge badge--blue badge--upper">{e.action}</span></td>
                  <td>{e.actorEmail ?? e.actorUserId ?? '—'}</td>
                  <td className="mono subtle">{e.targetSubscriberId ?? '—'}</td>
                  <td>{e.notes ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </SuperAdminCrudTemplate>
  );
}
