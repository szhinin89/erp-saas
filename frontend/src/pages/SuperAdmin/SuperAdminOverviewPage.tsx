import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../store/authStore';
import { usePermissionsStore } from '../../store/permissionsStore';
import { superAdminService, type SuperAdminSubscriber } from '../../services/superAdminService';
import { LoadingState, EmptyState } from '../../components/PageShell';
import { ZHPageNotice } from '../../components/zh/ZHPageNotice';
import { formatApiRequestError } from '../../modules/lib/apiError';
import { useI18n } from '../../i18n/i18n';
import './SuperAdminOverviewPage.css';

type Metrics = Awaited<ReturnType<typeof superAdminService.getMetrics>>;

const AVATAR_VARIANTS = ['primary', 'secondary', 'tertiary', 'neutral'] as const;

function avatarVariant(index: number) {
  return AVATAR_VARIANTS[index % AVATAR_VARIANTS.length];
}

function initials(name: string) {
  return name.trim().split(/\s+/).slice(0, 2).map((w) => w[0]?.toUpperCase() ?? '').join('');
}

function planBadgeClass(planCode: string | null | undefined): string {
  const c = (planCode ?? '').toLowerCase();
  if (c.includes('ent'))   return 'badge sa-plan-badge--enterprise';
  if (c.includes('pro'))   return 'badge sa-plan-badge--pro';
  if (c.includes('bus'))   return 'badge sa-plan-badge--business';
  if (c.includes('start')) return 'badge sa-plan-badge--starter';
  if (c)                   return 'badge sa-plan-badge--default';
  return                          'badge sa-plan-badge--default';
}

function storeImpersonationSubscriberName(name: string) {
  localStorage.setItem('superadmin-impersonation-subscriber-name', name);
}

export function SuperAdminOverviewPage() {
  const navigate = useNavigate();
  const { t } = useI18n();
  const { login } = useAuthStore();
  const clearPermissions = usePermissionsStore((s) => s.clearPermissions);

  const [loading,   setLoading]   = useState(true);
  const [error,     setError]     = useState('');
  const [metrics,   setMetrics]   = useState<Metrics | null>(null);
  const [subscribers,   setTenants]   = useState<SuperAdminSubscriber[]>([]);
  const [switching, setSwitching] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [m, tns] = await Promise.all([
        superAdminService.getMetrics(),
        superAdminService.getTenants(),
      ]);
      setMetrics(m);
      setTenants(tns);
    } catch (e) {
      setError(formatApiRequestError(e, {
        offline: t('common.apiUnreachable'),
        generic: t('common.errorGeneric'),
      }));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => { void load(); }, [load]);

  const previewTenants = useMemo(() => subscribers.slice(0, 5), [subscribers]);

  const handleSwitch = async (tenant: SuperAdminSubscriber) => {
    if (!tenant.isActive || switching) return;
    setSwitching(tenant.id);
    setError('');
    try {
      const auth = await superAdminService.switchTenant(tenant.id);
      storeImpersonationSubscriberName(tenant.name);
      clearPermissions();
      login(auth);
      navigate('/dashboard');
    } catch (e) {
      setError(formatApiRequestError(e, {
        offline: t('common.apiUnreachable'),
        generic: t('common.errorGeneric'),
      }));
    } finally {
      setSwitching(null);
    }
  };

  const inactiveCount = metrics
    ? metrics.totals.totalTenants - metrics.totals.activeTenants
    : 0;

  const inactiveUsers = metrics
    ? metrics.totals.totalUsers - metrics.totals.activeUsers
    : 0;

  return (
    <div className="pg-page">

      {/* ── Page Header ── */}
      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">SuperAdmin</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Administración Global</span>
          </nav>
          <h1 className="pg-title">Administración Global</h1>
          <p className="pg-subtitle">Panel de control para la gestión de empresas, planes y usuarios.</p>
        </div>
        <div className="pg-header-right">
          <button
            className="zh-btn zh-btn--secondary"
            type="button"
            disabled={loading}
            onClick={() => void load()}
          >
            <span className="material-symbols-outlined">refresh</span>
            Actualizar
          </button>
          <button
            className="zh-btn zh-btn--primary"
            type="button"
            onClick={() => navigate('/superadmin/companies')}
          >
            <span className="material-symbols-outlined">add</span>
            Nueva Empresa
          </button>
        </div>
      </div>

      {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}

      {/* ── KPI Cards (3-col horizontal) ── */}
      {loading ? (
        <LoadingState />
      ) : metrics ? (
        <div className="sa-overview-kpis">

          {/* Total Empresas */}
          <div className="sa-kpi-h">
            <div className="sa-kpi-h-left">
              <p className="sa-kpi-h-label">Total Empresas</p>
              <p className="sa-kpi-h-value">{metrics.totals.totalTenants.toLocaleString('es')}</p>
              <div className="sa-kpi-h-trend sa-kpi-h-trend--up">
                <span className="material-symbols-outlined">trending_up</span>
                <span>{metrics.totals.activeTenants} activas</span>
              </div>
            </div>
            <div className="sa-kpi-h-icon sa-kpi-h-icon--primary">
              <span className="material-symbols-outlined">apartment</span>
            </div>
          </div>

          {/* Usuarios Totales */}
          <div className="sa-kpi-h">
            <div className="sa-kpi-h-left">
              <p className="sa-kpi-h-label">Usuarios Totales</p>
              <p className="sa-kpi-h-value">{metrics.totals.totalUsers.toLocaleString('es')}</p>
              <div className="sa-kpi-h-trend sa-kpi-h-trend--up">
                <span className="material-symbols-outlined">trending_up</span>
                <span>{metrics.totals.activeUsers} activos</span>
              </div>
            </div>
            <div className="sa-kpi-h-icon sa-kpi-h-icon--secondary">
              <span className="material-symbols-outlined">groups</span>
            </div>
          </div>

          {/* Ratio activas */}
          <div className="sa-kpi-h">
            <div className="sa-kpi-h-left">
              <p className="sa-kpi-h-label">Empresas Activas</p>
              <p className="sa-kpi-h-value">
                {metrics.totals.totalTenants
                  ? Math.round((metrics.totals.activeTenants / metrics.totals.totalTenants) * 100)
                  : 0}%
              </p>
              <div className="sa-kpi-h-trend sa-kpi-h-trend--neutral">
                <span className="material-symbols-outlined">horizontal_rule</span>
                <span>de {metrics.totals.totalTenants} registradas</span>
              </div>
            </div>
            <div className="sa-kpi-h-icon sa-kpi-h-icon--tertiary">
              <span className="material-symbols-outlined">payments</span>
            </div>
          </div>
        </div>
      ) : null}

      {/* ── Companies Table Card ── */}
      <div className="card">

        {/* Header */}
        <div className="card-header">
          <h4 className="sa-companies-title">Empresas Registradas</h4>
          <div className="sa-companies-actions">
            <button className="zh-btn zh-btn--ghost zh-btn--sm" type="button">
              <span className="material-symbols-outlined">filter_list</span>
              Filtros
            </button>
            <button className="zh-btn zh-btn--ghost zh-btn--sm" type="button">
              <span className="material-symbols-outlined">download</span>
              Exportar
            </button>
          </div>
        </div>

        {/* Table */}
        {loading ? (
          <div style={{ padding: '40px' }}><LoadingState /></div>
        ) : subscribers.length === 0 ? (
          <div style={{ padding: '40px' }}><EmptyState message={t('common.noData')} /></div>
        ) : (
          <>
            <div style={{ overflowX: 'auto' }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Nombre</th>
                    <th>Identificador</th>
                    <th>Plan</th>
                    <th>Estado</th>
                    <th style={{ textAlign: 'right' }}>Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {previewTenants.map((tenant, i) => (
                    <tr key={tenant.id}>
                      <td>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                          <div className={`zh-avatar zh-avatar--square zh-avatar--${avatarVariant(i)}`} aria-hidden="true">
                            {initials(tenant.name)}
                          </div>
                          <div className="sa-company-info">
                            <p className="sa-company-name">{tenant.name}</p>
                            <p className="sa-company-sub">{tenant.slug}</p>
                          </div>
                        </div>
                      </td>
                      <td className="mono" style={{ fontSize: '12px', color: 'var(--color-text-secondary)' }}>
                        {tenant.id.split('-')[0]}…
                      </td>
                      <td>
                        <span className={planBadgeClass(tenant.planCode)}>
                          {tenant.planCode ?? 'Sin plan'}
                        </span>
                      </td>
                      <td>
                        <span className={`sa-status-dot ${tenant.isActive ? 'sa-status-dot--active' : 'sa-status-dot--inactive'}`}>
                          {tenant.isActive ? t('common.active') : t('common.inactive')}
                        </span>
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <button
                          className="zh-btn zh-btn--primary zh-btn--sm"
                          type="button"
                          disabled={!tenant.isActive || !!switching}
                          onClick={() => void handleSwitch(tenant)}
                          style={{ marginLeft: 'auto' }}
                        >
                          {switching === tenant.id ? t('superadmin.switching') : 'Entrar como empresa'}
                          <span className="material-symbols-outlined">login</span>
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Table footer */}
            <div className="pg-table-footer">
              <span className="pg-table-timestamp">
                Mostrando {previewTenants.length} de {subscribers.length} empresas
              </span>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <div className="sa-page-pagination">
                  <button className="sa-page-nav-btn" type="button" disabled>
                    <span className="material-symbols-outlined">chevron_left</span>
                  </button>
                  <div className="sa-page-nums">
                    <button className="sa-page-num-btn is-active" type="button">1</button>
                    {subscribers.length > 5 && (
                      <button
                        className="sa-page-num-btn"
                        type="button"
                        onClick={() => navigate('/superadmin/companies')}
                      >
                        …
                      </button>
                    )}
                  </div>
                  <button
                    className="sa-page-nav-btn"
                    type="button"
                    onClick={() => navigate('/superadmin/companies')}
                  >
                    <span className="material-symbols-outlined">chevron_right</span>
                  </button>
                </div>
                <button
                  className="zh-btn zh-btn--secondary zh-btn--sm"
                  type="button"
                  onClick={() => navigate('/superadmin/companies')}
                >
                  Ver todas
                </button>
              </div>
            </div>
          </>
        )}
      </div>

      {/* ── Bottom 2-col grid ── */}
      <div className="sa-support-grid">

        {/* Distribución geográfica (decorativa, proporcional a datos reales) */}
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">map</span>
              <span className="pg-section-label">Distribución de Empresas</span>
            </div>
            <span style={{ fontSize: 'var(--text-label-sm-size)', color: 'var(--color-text-secondary)' }}>
              {metrics?.totals.totalTenants ?? 0} registradas
            </span>
          </div>
          <div className="pg-section-body">
            <div className="sa-geo-placeholder">
              <div className="sa-geo-grid">
                <div>
                  <p className="sa-geo-value">
                    {metrics ? Math.round(metrics.totals.activeTenants * 0.55) : '—'}
                  </p>
                  <p className="sa-geo-label">Costa</p>
                </div>
                <div>
                  <p className="sa-geo-value">
                    {metrics ? Math.round(metrics.totals.activeTenants * 0.38) : '—'}
                  </p>
                  <p className="sa-geo-label">Sierra</p>
                </div>
                <div>
                  <p className="sa-geo-value">
                    {metrics ? Math.round(metrics.totals.activeTenants * 0.05) : '—'}
                  </p>
                  <p className="sa-geo-label">Oriente</p>
                </div>
                <div>
                  <p className="sa-geo-value">
                    {metrics ? Math.round(metrics.totals.activeTenants * 0.02) : '—'}
                  </p>
                  <p className="sa-geo-label">Insular</p>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Alertas del sistema */}
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">notifications_active</span>
              <span className="pg-section-label">Alertas del Sistema</span>
            </div>
          </div>
          <div className="pg-section-body">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
              {inactiveCount > 0 && (
                <div className="sa-alert-item sa-alert-item--error">
                  <span className="material-symbols-outlined sa-alert-icon sa-alert-icon--error">report</span>
                  <div>
                    <p className="sa-alert-title">Empresas inactivas</p>
                    <p className="sa-alert-body">
                      {inactiveCount} {inactiveCount === 1 ? 'empresa registrada sin' : 'empresas registradas sin'} acceso activo.
                    </p>
                  </div>
                </div>
              )}
              {inactiveUsers > 0 && (
                <div className="sa-alert-item sa-alert-item--warning">
                  <span className="material-symbols-outlined sa-alert-icon sa-alert-icon--warning">warning</span>
                  <div>
                    <p className="sa-alert-title">Usuarios sin actividad</p>
                    <p className="sa-alert-body">
                      {inactiveUsers} {inactiveUsers === 1 ? 'usuario' : 'usuarios'} sin sesión activa registrada.
                    </p>
                  </div>
                </div>
              )}
              <div className="sa-alert-item sa-alert-item--info">
                <span className="material-symbols-outlined sa-alert-icon sa-alert-icon--info">info</span>
                <div>
                  <p className="sa-alert-title">Estado del sistema</p>
                  <p className="sa-alert-body">
                    {metrics
                      ? `${metrics.totals.activeTenants} empresas y ${metrics.totals.activeUsers} usuarios activos en este momento.`
                      : 'Cargando estado del sistema…'}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>

      </div>

    </div>
  );
}
