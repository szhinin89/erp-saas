import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { superAdminService, type SuperAdminSubscriber, type SubscriberEntitlementsSnapshot } from '../api/superAdminService';
import { storeImpersonationSubscriberName } from '../superAdminPanelUtils';
import { useAuthStore } from '../../../store/authStore';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { formatApiRequestError } from '../../../modules/lib/apiError';
import { getAccessToken } from '../../../lib/session/authTokenMemory';
import { restoreSessionFromCookie } from '../../../lib/session/restoreSessionFromCookie';
import { syncSessionEntitlements } from '../../../lib/syncSessionEntitlements';
import { LoadingState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import './SuperAdminSubscriberDetailPage.css';

type TabId = 'overview' | 'entitlements' | 'actions';

const TABS: { id: TabId; label: string; icon: string }[] = [
  { id: 'overview', label: 'Resumen', icon: 'info' },
  { id: 'entitlements', label: 'Entitlements', icon: 'verified' },
  { id: 'actions', label: 'Acciones', icon: 'settings' },
];

export function SuperAdminSubscriberDetailPage() {
  const { subscriberId } = useParams<{ subscriberId: string }>();
  const navigate = useNavigate();
  const { login } = useAuthStore();
  const clearPermissions = usePermissionsStore((s) => s.clearPermissions);

  const [tab, setTab] = useState<TabId>('overview');
  const [subscriber, setSubscriber] = useState<SuperAdminSubscriber | null>(null);
  const [entitlements, setEntitlements] = useState<SubscriberEntitlementsSnapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [entLoading, setEntLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionBusy, setActionBusy] = useState(false);
  const [actionMsg, setActionMsg] = useState<string | null>(null);
  const [changePlanInput, setChangePlanInput] = useState('');

  useEffect(() => {
    if (!subscriberId) return;
    superAdminService.getSubscribers()
      .then((list) => {
        const found = list.find((s) => s.id === subscriberId);
        if (!found) { setError('Suscriptor no encontrado.'); return; }
        setSubscriber(found);
        setChangePlanInput(found.planCode ?? '');
      })
      .catch(() => setError('No se pudo cargar el suscriptor.'))
      .finally(() => setLoading(false));
  }, [subscriberId]);

  useEffect(() => {
    if (tab !== 'entitlements' || !subscriberId || entitlements) return;
    setEntLoading(true);
    superAdminService.getSubscriberEntitlements(subscriberId)
      .then((snap) => setEntitlements(snap ?? null))
      .catch(() => setError('No se pudieron cargar los entitlements.'))
      .finally(() => setEntLoading(false));
  }, [tab, subscriberId, entitlements]);

  const doAction = async (fn: () => Promise<unknown>, successMsg: string) => {
    setActionBusy(true);
    setError(null);
    setActionMsg(null);
    try {
      await fn();
      setActionMsg(successMsg);
      const list = await superAdminService.getSubscribers();
      const updated = list.find((s) => s.id === subscriberId);
      if (updated) setSubscriber(updated);
    } catch (e) {
      setError(formatApiRequestError(e, { generic: 'Error al ejecutar la acción.' }));
    } finally {
      setActionBusy(false);
    }
  };

  const handleImpersonate = async (target: '/saas/overview' | '/saas/companies' | '/saas/billing') => {
    if (!subscriber || actionBusy) return;
    const id = subscriber.id.trim();
    if (!id) { setError('ID de suscriptor inválido.'); return; }
    setActionBusy(true);
    setError(null);
    try {
      if (!getAccessToken()) {
        const restored = await restoreSessionFromCookie();
        if (!restored || !getAccessToken()) throw new Error('Sesión expirada. Vuelve a iniciar sesión.');
      }
      const auth = await superAdminService.switchSubscriber(id);
      storeImpersonationSubscriberName(subscriber.name);
      clearPermissions();
      login(auth);
      try { await syncSessionEntitlements(); } catch { /* AppLayout reintenta */ }
      navigate(target);
    } catch (e) {
      setError(formatApiRequestError(e, { generic: `No se pudo cambiar al suscriptor "${subscriber.name}".` }));
    } finally {
      setActionBusy(false);
    }
  };

  if (loading) {
    return (
      <div className="sa-detail-page">
        <div className="sa-detail-loading"><LoadingState /></div>
      </div>
    );
  }

  if (!subscriber) {
    return (
      <div className="sa-detail-page">
        <ZHPageNotice variant="error" message={error ?? 'Suscriptor no encontrado.'} />
        <button className="zh-btn zh-btn--ghost zh-btn--md" onClick={() => navigate('/superadmin/subscribers')}>
          Volver
        </button>
      </div>
    );
  }

  return (
    <div className="sa-detail-page">
      {/* Header */}
      <div className="sa-detail-header">
        <button
          className="zh-btn zh-btn--ghost zh-btn--sm sa-detail-back"
          onClick={() => navigate('/superadmin/subscribers')}
        >
          <span className="material-symbols-outlined">arrow_back</span>
          Suscriptores
        </button>
        <div className="sa-detail-title-row">
          <div className="sa-detail-avatar">{subscriber.name.charAt(0).toUpperCase()}</div>
          <div>
            <h2 className="sa-detail-name">{subscriber.name}</h2>
            <p className="sa-detail-slug subtle">{subscriber.slug}</p>
          </div>
          <div className="sa-detail-status-chips">
            <span className={subscriber.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
              {subscriber.isActive ? 'Activo' : 'Inactivo'}
            </span>
            {subscriber.planCode && (
              <span className="badge badge--blue badge--md badge--upper">{subscriber.planCode}</span>
            )}
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="sa-detail-tabs">
        {TABS.map((t) => (
          <button
            key={t.id}
            type="button"
            className={`sa-detail-tab${tab === t.id ? ' is-active' : ''}`}
            onClick={() => setTab(t.id)}
          >
            <span className="material-symbols-outlined">{t.icon}</span>
            {t.label}
          </button>
        ))}
      </div>

      {error && <ZHPageNotice variant="error" message={error} />}
      {actionMsg && <ZHPageNotice variant="success" message={actionMsg} />}

      {/* Tab: Overview */}
      {tab === 'overview' && (
        <div className="sa-detail-body">
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">Información del suscriptor</h3>
            <div className="sa-detail-grid">
              <div className="sa-detail-field">
                <span className="sa-detail-label">ID</span>
                <span className="mono sa-detail-value">{subscriber.id}</span>
              </div>
              <div className="sa-detail-field">
                <span className="sa-detail-label">Slug</span>
                <span className="mono sa-detail-value">{subscriber.slug}</span>
              </div>
              <div className="sa-detail-field">
                <span className="sa-detail-label">Plan</span>
                <span className="sa-detail-value">{subscriber.planCode ?? '—'}</span>
              </div>
              <div className="sa-detail-field">
                <span className="sa-detail-label">Estado</span>
                <span className="sa-detail-value">{subscriber.isActive ? 'Activo' : 'Inactivo'}</span>
              </div>
              <div className="sa-detail-field">
                <span className="sa-detail-label">Usuarios activos</span>
                <span className="sa-detail-value mono">{subscriber.activeUsers} / {subscriber.totalUsers}</span>
              </div>
              <div className="sa-detail-field">
                <span className="sa-detail-label">Módulos habilitados</span>
                <span className="sa-detail-value">
                  {subscriber.hasModuleRestrictions
                    ? (subscriber.enabledModules?.join(', ') || 'Ninguno')
                    : 'Todos (sin restricción)'}
                </span>
              </div>
              <div className="sa-detail-field">
                <span className="sa-detail-label">Menú personalizado</span>
                <span className="sa-detail-value">{subscriber.hasCustomMenu ? 'Sí' : 'No'}</span>
              </div>
            </div>
          </div>

          {/* Impersonation */}
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">Acceso de impersonación</h3>
            <p className="subtle sa-detail-card-desc">
              Cambia al contexto del suscriptor para ver su ERP como SuperAdmin.
            </p>
            <div className="sa-detail-impersonation-btns">
              <button
                type="button"
                className="zh-btn zh-btn--ghost zh-btn--md"
                disabled={actionBusy}
                onClick={() => void handleImpersonate('/saas/overview')}
              >
                <span className="material-symbols-outlined">summarize</span>
                Cuenta SaaS
              </button>
              <button
                type="button"
                className="zh-btn zh-btn--ghost zh-btn--md"
                disabled={actionBusy}
                onClick={() => void handleImpersonate('/saas/companies')}
              >
                <span className="material-symbols-outlined">domain</span>
                Empresas
              </button>
              <button
                type="button"
                className="zh-btn zh-btn--ghost zh-btn--md"
                disabled={actionBusy}
                onClick={() => void handleImpersonate('/saas/billing')}
              >
                <span className="material-symbols-outlined">receipt_long</span>
                Facturación
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Tab: Entitlements */}
      {tab === 'entitlements' && (
        <div className="sa-detail-body">
          {entLoading ? (
            <div className="sa-detail-loading"><LoadingState /></div>
          ) : entitlements ? (
            <div className="sa-detail-card">
              <h3 className="sa-detail-card-title">Features entitlements — Plan: {entitlements.planCode ?? '—'}</h3>
              {entitlements.features.length === 0 ? (
                <p className="subtle">Sin features registradas.</p>
              ) : (
                <div className="pg-overflow-x">
                  <table className="table">
                    <thead>
                      <tr>
                        <th>Feature</th>
                        <th>Código</th>
                        <th>Incluida</th>
                        <th>Límite</th>
                      </tr>
                    </thead>
                    <tbody>
                      {entitlements.features.map((f) => (
                        <tr key={f.featureCode}>
                          <td>{f.featureName}</td>
                          <td><span className="mono subtle">{f.featureCode}</span></td>
                          <td>
                            <span className={f.isIncluded ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                              {f.isIncluded ? 'Sí' : 'No'}
                            </span>
                          </td>
                          <td className="mono">{f.limitPerPeriod ?? '∞'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          ) : (
            <p className="subtle">Sin datos de entitlements.</p>
          )}
        </div>
      )}

      {/* Tab: Actions */}
      {tab === 'actions' && (
        <div className="sa-detail-body">
          {/* Lifecycle */}
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">Estado del suscriptor</h3>
            <div className="sa-detail-action-row">
              <button
                type="button"
                className="zh-btn zh-btn--primary zh-btn--md"
                disabled={actionBusy || subscriber.isActive}
                onClick={() => void doAction(
                  () => superAdminService.activateSubscriber(subscriber.id),
                  'Suscriptor activado correctamente.'
                )}
              >
                <span className="material-symbols-outlined">check_circle</span>
                Activar
              </button>
              <button
                type="button"
                className="zh-btn zh-btn--ghost zh-btn--md pg-btn-error-ghost"
                disabled={actionBusy || !subscriber.isActive}
                onClick={() => void doAction(
                  () => superAdminService.suspendSubscriber(subscriber.id),
                  'Suscriptor suspendido.'
                )}
              >
                <span className="material-symbols-outlined">block</span>
                Suspender
              </button>
            </div>
          </div>

          {/* Change Plan */}
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">Cambiar plan comercial</h3>
            <div className="sa-detail-action-row">
              <input
                className="zh-input"
                placeholder="Código del plan (ej. PROFESSIONAL)"
                value={changePlanInput}
                onChange={(e) => setChangePlanInput(e.target.value)}
              />
              <button
                type="button"
                className="zh-btn zh-btn--primary zh-btn--md"
                disabled={actionBusy || !changePlanInput.trim()}
                onClick={() => void doAction(
                  () => superAdminService.changePlan(subscriber.id, changePlanInput.trim()),
                  `Plan actualizado a "${changePlanInput}".`
                )}
              >
                Cambiar plan
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
