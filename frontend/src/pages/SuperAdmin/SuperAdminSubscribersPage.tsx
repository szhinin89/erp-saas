import { useEffect, useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../store/authStore';
import { usePermissionsStore } from '../../store/permissionsStore';
import { superAdminService, type SuperAdminSubscriber } from '../../modules/superadmin/api/superAdminService';
import { storeImpersonationSubscriberName } from '../../modules/superadmin/superAdminPanelUtils';
import { LoadingState, EmptyState } from '../../components/PageShell';
import { ZHPageNotice } from '../../components/zh/ZHPageNotice';
import './SuperAdminSubscribersPage.css';

type TargetPage = '/saas/overview' | '/saas/companies' | '/saas/billing';

export function SuperAdminSubscribersPage() {
  const navigate  = useNavigate();
  const { login } = useAuthStore();
  const clearPerms = usePermissionsStore((s) => s.clear);

  const [items, setItems]       = useState<SuperAdminSubscriber[]>([]);
  const [loading, setLoading]   = useState(true);
  const [error, setError]       = useState<string | null>(null);
  const [search, setSearch]     = useState('');
  const [switching, setSwitching] = useState<string | null>(null);

  useEffect(() => {
    superAdminService.getSubscribers()
      .then((res) => setItems(res.data?.responseObject ?? []))
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
    setSwitching(subscriber.id);
    try {
      const auth = await superAdminService.switchSubscriber(subscriber.id);
      storeImpersonationSubscriberName(subscriber.name);
      clearPerms();
      login(auth!);
      navigate(target);
    } catch {
      setError(`No se pudo cambiar al suscriptor "${subscriber.name}".`);
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
