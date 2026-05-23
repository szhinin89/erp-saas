import { useCallback, useEffect, useState } from 'react';
import { superAdminService, type PlatformUserRow } from '../api/superAdminService';
import { SuperAdminCrudTemplate } from '../../../templates/SuperAdminCrudTemplate';
import { LoadingState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn } from '../../../components/zh/ZHForm';

export function SuperAdminUsersPage() {
  const [users, setUsers] = useState<PlatformUserRow[]>([]);
  const [activeCount, setActiveCount] = useState(0);
  const [impersonation, setImpersonation] = useState<Array<Record<string, unknown>>>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    Promise.all([superAdminService.getPlatformUsers(), superAdminService.getPlatformImpersonationHistory()])
      .then(([page, events]) => {
        setUsers(page?.users ?? []);
        setActiveCount(page?.activePlatformUsers ?? 0);
        setImpersonation(events);
      })
      .catch(() => setError('No se pudieron cargar usuarios platform.'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { load(); }, [load]);

  const revokePlatformSessions = async (userId: string) => {
    setBusyId(userId);
    try {
      await superAdminService.revokePlatformUserSessions(userId, '00000000-0000-0000-0000-000000000000');
      load();
    } catch {
      setError('No se pudieron revocar sesiones.');
    } finally {
      setBusyId(null);
    }
  };

  return (
    <SuperAdminCrudTemplate title="Platform users" subtitle={`Operadores globales · activos: ${activeCount}`}>
      {error && <ZHPageNotice variant="error" message={error} />}
      {loading ? <LoadingState /> : (
        <>
          <div className="pg-overflow-x">
            <table className="table">
              <thead>
                <tr>
                  <th>Email</th>
                  <th>Nombre</th>
                  <th>Rol platform</th>
                  <th>Estado</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr key={u.id}>
                    <td>{u.email}</td>
                    <td>{u.firstName} {u.lastName}</td>
                    <td><span className="badge badge--blue">{u.platformRole ?? '—'}</span></td>
                    <td>{u.isActive ? 'Activo' : 'Inactivo'}</td>
                    <td>
                      <ZHBtn
                        variant="ghost"
                        size="sm"
                        disabled={busyId === u.id}
                        onClick={() => void revokePlatformSessions(u.id)}
                      >
                        Revocar sesiones
                      </ZHBtn>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {users.length === 0 && <p className="subtle">Sin usuarios platform registrados.</p>}
          </div>
          <div className="pg-section pg-mt-md">
            <h3 className="pg-section-label">Impersonation history</h3>
            {impersonation.length === 0 ? (
              <p className="subtle">Sin eventos registrados.</p>
            ) : (
              <ul className="subtle">
                {impersonation.slice(0, 10).map((e) => (
                  <li key={String(e.id)}>
                    {String(e.actorEmail ?? '—')} → {String(e.targetSubscriberId ?? '—')} · {String(e.createdAtUtc ?? '')}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </>
      )}
      <p className="subtle pg-mt-md">Roles: SuperAdmin, Support, BillingAdmin (BillingManager), Auditor.</p>
    </SuperAdminCrudTemplate>
  );
}
