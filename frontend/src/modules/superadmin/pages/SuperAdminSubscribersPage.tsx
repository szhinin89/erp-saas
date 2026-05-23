import { useEffect, useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../../store/authStore';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { superAdminService, type SuperAdminSubscriber } from '../api/superAdminService';
import { storeImpersonationSubscriberName } from '../superAdminPanelUtils';
import { formatApiRequestError } from '../../../modules/lib/apiError';
import { getAccessToken } from '../../../lib/session/authTokenMemory';
import { restoreSessionFromCookie } from '../../../lib/session/restoreSessionFromCookie';
import { syncSessionEntitlements } from '../../../lib/syncSessionEntitlements';
import { LoadingState, EmptyState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import './SuperAdminSubscribersPage.css';

type TargetPage = '/saas/overview' | '/saas/companies' | '/saas/billing';

function resolveSubscriberId(subscriber: SuperAdminSubscriber): string {
  const raw = subscriber.id ?? (subscriber as { Id?: string }).Id ?? '';
  return raw.trim();
}

export function SuperAdminSubscribersPage() {
  const navigate  = useNavigate();
  const { login } = useAuthStore();
  const clearPermissions = usePermissionsStore((s) => s.clearPermissions);

  const [items, setItems]       = useState<SuperAdminSubscriber[]>([]);
  const [loading, setLoading]   = useState(true);
  const [error, setError]       = useState<string | null>(null);
  const [search, setSearch]     = useState('');
  const [switching, setSwitching] = useState<string | null>(null);

  useEffect(() => {
    superAdminService.getSubscribers()
      .then((list) => setItems(list ?? []))
      .catch(() => setError('No se pudo cargar la lista de suscriptores.'))
      .finally(() => setLoading(false));
  }, []);

  const filtered = useMemo(() =>
    items.filter((s) =>
      !search ||
      s.name.toLowerCase().includes(search.toLowerCase()) ||
      s.slug.toLowerCase().includes(search.toLowerCase()) ||
      (s.planCode ?? '').toLowerCase().includes(search.toLowerCase())
    ), [items, search]);

  const handleSwitch = async (subscriber: SuperAdminSubscriber, target: TargetPage) => {
    if (switching) return;
    const subscriberId = resolveSubscriberId(subscriber);
    if (!subscriberId) {
      setError('Identificador de suscriptor inválido.');
      return;
    }
    setSwitching(subscriber.id);
    setError('');
    try {
      if (!getAccessToken()) {
        const restored = await restoreSessionFromCookie();
        if (!restored || !getAccessToken()) {
          throw new Error('Sesión expirada. Vuelve a iniciar sesión.');
        }
      }
      const auth = await superAdminService.switchSubscriber(subscriberId);
      storeImpersonationSubscriberName(subscriber.name);
      clearPermissions();
      login(auth);
      try {
        await syncSessionEntitlements();
      } catch {
        // AppLayout reintenta permisos/menú si hace falta.
      }
      navigate(target);
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic: `No se pudo cambiar al suscriptor "${subscriber.name}".`,
        }),
      );
    } finally {
      setSwitching(null);
    }
  };

  return (
    <div className="sa-subscribers-page">
      <div className="sa-subscribers-header">
        <div>
          <h2 className="sa-subscribers-title">Suscriptores</h2>
          <p className="sa-subscribers-sub">
            Selecciona un suscriptor y abre su vista SaaS, Empresas o Facturación.
          </p>
        </div>
        <div className="sa-subscribers-search">
          <span className="material-symbols-outlined">search</span>
          <input
            type="text"
            placeholder="Buscar por nombre, slug o plan…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </div>

      {error && <ZHPageNotice variant="error" message={error} />}

      {loading ? (
        <div className="sa-subscribers-center"><LoadingState /></div>
      ) : filtered.length === 0 ? (
        <div className="sa-subscribers-center">
          <EmptyState message={items.length === 0 ? 'Sin suscriptores registrados.' : 'Sin resultados.'} />
        </div>
      ) : (
        <div className="pg-overflow-x">
          <table className="table">
            <thead>
              <tr>
                <th>Suscriptor</th>
                <th>Plan</th>
                <th>Usuarios</th>
                <th>Estado</th>
                <th className="pg-th-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((s) => {
                const busy = switching === s.id;
                return (
                  <tr key={s.id} className={!s.isActive ? 'pg-row-inactive' : undefined}>
                    <td>
                      <div className="sa-sub-name">{s.name}</div>
                      <div className="sa-sub-slug subtle">{s.slug}</div>
                    </td>
                    <td>
                      {s.planCode
                        ? <span className="badge badge--blue badge--md badge--upper">{s.planCode}</span>
                        : <span className="subtle">—</span>}
                    </td>
                    <td>
                      <span className="mono">{s.activeUsers}</span>
                      <span className="subtle"> / {s.totalUsers}</span>
                    </td>
                    <td>
                      <span className={s.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                        {s.isActive ? 'Activo' : 'Inactivo'}
                      </span>
                    </td>
                    <td className="pg-td-right">
                      <div className="sa-sub-actions">
                        <button
                          type="button"
                          className="zh-btn zh-btn--primary zh-btn--sm"
                          title="Ficha del suscriptor"
                          onClick={() => navigate(`/superadmin/subscribers/${encodeURIComponent(s.id)}`)}
                        >
                          <span className="material-symbols-outlined">open_in_new</span>
                          Ficha
                        </button>
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          title="Cuenta SaaS"
                          disabled={busy}
                          onClick={() => void handleSwitch(s, '/saas/overview')}
                        >
                          <span className="material-symbols-outlined">summarize</span>
                          Cuenta
                        </button>
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          title="Empresas"
                          disabled={busy}
                          onClick={() => void handleSwitch(s, '/saas/companies')}
                        >
                          <span className="material-symbols-outlined">domain</span>
                          Empresas
                        </button>
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          title="Facturación"
                          disabled={busy}
                          onClick={() => void handleSwitch(s, '/saas/billing')}
                        >
                          <span className="material-symbols-outlined">receipt_long</span>
                          Facturación
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <div className="sa-subscribers-footer subtle">
        {filtered.length} de {items.length} suscriptores
      </div>
    </div>
  );
}
