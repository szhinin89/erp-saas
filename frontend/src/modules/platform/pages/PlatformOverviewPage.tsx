import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { platformService, type PlatformSubscriber } from '../api/platformService';
import { goToSubscriberDetail } from '../../../navigation/platformSubscriberDetailNav';
import { LoadingState, EmptyState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHScreenHeading } from '../../../components/zh/ZHLayout';
import { PlatformCrudTemplate } from '../../../templates/PlatformCrudTemplate';
import { StatCard } from '../../../components/zh/StatCard';
import { RuntimeModeBadge } from '../../../components/RuntimeModeBadge';
import { formatApiRequestError } from '../../../modules/lib/apiError';
import { useI18n } from '../../../i18n/i18n';
import './PlatformOverviewPage.css';

type Metrics = Awaited<ReturnType<typeof platformService.getMetrics>>;

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

export function PlatformOverviewPage() {
  const navigate = useNavigate();
  const { t } = useI18n();

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [metrics, setMetrics] = useState<Metrics | null>(null);
  const [subscribers, setSubscribers] = useState<PlatformSubscriber[]>([]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [m, tns] = await Promise.all([
        platformService.getMetrics(),
        platformService.getSubscribers(),
      ]);
      setMetrics(m);
      setSubscribers(tns);
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

  const previewSubscribers = useMemo(() => subscribers.slice(0, 5), [subscribers]);

  const inactiveCount = metrics
    ? metrics.totals.totalSubscribers - metrics.totals.activeSubscribers
    : 0;

  const inactiveUsers = metrics
    ? metrics.totals.totalUsers - metrics.totals.activeUsers
    : 0;

  return (
    <PlatformCrudTemplate title={t('platform.title')}>
      <ZHScreenHeading
        kicker={t('platform.overview.kicker')}
        title={t('platform.overview.title')}
        subtitle={t('platform.overview.subtitle')}
        right={
          <div className="pg-flex-row-8-wrap">
            <RuntimeModeBadge />
            <button
              className="zh-btn zh-btn--secondary"
              type="button"
              disabled={loading}
              onClick={() => void load()}
            >
              <span className="material-symbols-outlined">refresh</span>
              {t('common.refresh')}
            </button>
            <button
              className="zh-btn zh-btn--primary"
              type="button"
              onClick={() => navigate('/platform/subscribers')}
            >
              <span className="material-symbols-outlined">add</span>
              {t('platform.createSubscriber')}
            </button>
          </div>
        }
      />

      {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}

      {/* ── KPI Cards ── */}
      {loading ? (
        <LoadingState />
      ) : metrics ? (
        <div className="pg-kpis">
          <StatCard
            label={t('platform.totalSubscribers')}
            value={metrics.totals.totalSubscribers.toLocaleString('es')}
            icon="apartment"
            tone="primary"
            hint={`${metrics.totals.activeSubscribers} ${t('common.active').toLowerCase()}`}
          />
          <StatCard
            label={t('platform.totalUsers')}
            value={metrics.totals.totalUsers.toLocaleString('es')}
            icon="groups"
            tone="secondary"
            hint={`${metrics.totals.activeUsers} ${t('common.active').toLowerCase()}`}
          />
          <StatCard
            label={t('platform.activeSubscribers')}
            value={`${
              metrics.totals.totalSubscribers
                ? Math.round((metrics.totals.activeSubscribers / metrics.totals.totalSubscribers) * 100)
                : 0
            }%`}
            icon="payments"
            tone="tertiary"
            hint={t('platform.overview.activeRatioHint', {
              total: metrics.totals.totalSubscribers,
            })}
          />
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
          <div className="pg-pad-40"><LoadingState /></div>
        ) : subscribers.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message={t('common.noData')} /></div>
        ) : (
          <>
            <div className="pg-overflow-x">
              <table className="table">
                <thead>
                  <tr>
                    <th>Nombre</th>
                    <th>Identificador</th>
                    <th>Plan</th>
                    <th>Estado</th>
                    <th className="pg-table-actions">Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {previewSubscribers.map((subscriber, i) => (
                    <tr key={subscriber.id}>
                      <td>
                        <div className="pg-flex-row-12">
                          <div className={`zh-avatar zh-avatar--square zh-avatar--${avatarVariant(i)}`} aria-hidden="true">
                            {initials(subscriber.name)}
                          </div>
                          <div className="sa-company-info">
                            <p className="sa-company-name">{subscriber.name}</p>
                            <p className="sa-company-sub">{subscriber.slug}</p>
                          </div>
                        </div>
                      </td>
                      <td className="mono pg-text-muted-sm">
                        {subscriber.id.split('-')[0]}…
                      </td>
                      <td>
                        <span className={planBadgeClass(subscriber.planCode)}>
                          {subscriber.planCode ?? 'Sin plan'}
                        </span>
                      </td>
                      <td>
                        <span className={`sa-status-dot ${subscriber.isActive ? 'sa-status-dot--active' : 'sa-status-dot--inactive'}`}>
                          {subscriber.isActive ? t('common.active') : t('common.inactive')}
                        </span>
                      </td>
                      <td className="pg-table-actions">
                        <button
                          className="zh-btn zh-btn--secondary zh-btn--sm pg-btn-ml-auto"
                          type="button"
                          onClick={() => goToSubscriberDetail(navigate, subscriber.id)}
                        >
                          {t('platform.subscriberRow.openSheet')}
                          <span className="material-symbols-outlined">description</span>
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
                Mostrando {previewSubscribers.length} de {subscribers.length} empresas
              </span>
              <div className="pg-flex-row-8">
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
                        onClick={() => navigate('/platform/subscribers')}
                      >
                        …
                      </button>
                    )}
                  </div>
                  <button
                    className="sa-page-nav-btn"
                    type="button"
                    onClick={() => navigate('/platform/subscribers')}
                  >
                    <span className="material-symbols-outlined">chevron_right</span>
                  </button>
                </div>
                <button
                  className="zh-btn zh-btn--secondary zh-btn--sm"
                  type="button"
                  onClick={() => navigate('/platform/subscribers')}
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
            <span className="pg-label-secondary">
              {metrics?.totals.totalSubscribers ?? 0} registradas
            </span>
          </div>
          <div className="pg-section-body">
            <div className="sa-geo-placeholder">
              <div className="sa-geo-grid">
                <div>
                  <p className="sa-geo-value">
                    {metrics ? Math.round(metrics.totals.activeSubscribers * 0.55) : '—'}
                  </p>
                  <p className="sa-geo-label">Costa</p>
                </div>
                <div>
                  <p className="sa-geo-value">
                    {metrics ? Math.round(metrics.totals.activeSubscribers * 0.38) : '—'}
                  </p>
                  <p className="sa-geo-label">Sierra</p>
                </div>
                <div>
                  <p className="sa-geo-value">
                    {metrics ? Math.round(metrics.totals.activeSubscribers * 0.05) : '—'}
                  </p>
                  <p className="sa-geo-label">Oriente</p>
                </div>
                <div>
                  <p className="sa-geo-value">
                    {metrics ? Math.round(metrics.totals.activeSubscribers * 0.02) : '—'}
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
            <div className="pg-flex-col-3">
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
                      ? `${metrics.totals.activeSubscribers} empresas y ${metrics.totals.activeUsers} usuarios activos en este momento.`
                      : 'Cargando estado del sistema…'}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>

      </div>
    </PlatformCrudTemplate>
  );
}
