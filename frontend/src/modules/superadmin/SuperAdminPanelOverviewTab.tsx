import { ZHBtn } from '../../components/zh/ZHForm';
import { ZHCard } from '../../components/zh/ZHCard';
import { ZHInlineRowRight } from '../../components/zh/ZHLayout';
import { LoadingState } from '../../components/PageShell';
import { ZHKpiPanel } from '../../components/zh/ZHDashboard';
import { SuperAdminGrowthSection } from '../../components/superadmin/SuperAdminGrowthSection';
import type { SuperAdminPanelPageState } from './useSuperAdminPanelPage';

type Props = Pick<
  SuperAdminPanelPageState,
  | 't'
  | 'loading'
  | 'metrics'
  | 'subscribers'
  | 'isSuperAdmin'
  | 'activePlans'
  | 'selectHomeTab'
  | 'openCreateSubscriber'
>;

export function SuperAdminPanelOverviewTab({
  t,
  loading,
  metrics,
  subscribers,
  isSuperAdmin,
  activePlans,
  selectHomeTab,
  openCreateSubscriber,
}: Props) {
  return (
    <div className="sa-overviewKpi">
      {!loading && subscribers.length === 0 && isSuperAdmin ? (
        <ZHCard title={t('superadmin.welcomeNoSubscribersTitle')}>
          <p className="subtle sa-welcome-note">{t('superadmin.welcomeNoSubscribersBody')}</p>
          <ZHInlineRowRight>
            <ZHBtn variant="secondary" size="sm" onClick={() => selectHomeTab('plans')}>
              {t('superadmin.welcomeGoPlans')}
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="sm"
              onClick={() => {
                selectHomeTab('companies');
                openCreateSubscriber();
              }}
              disabled={activePlans.length === 0}
              title={activePlans.length === 0 ? t('superadmin.createSubscriber.error.noPlans') : undefined}
            >
              {t('superadmin.welcomeCreateFirstCompany')}
            </ZHBtn>
          </ZHInlineRowRight>
        </ZHCard>
      ) : null}
      {loading ? (
        <ZHCard>
          <LoadingState />
        </ZHCard>
      ) : metrics ? (
        <ZHKpiPanel
          title={t('superadmin.metrics')}
          items={[
            { label: t('superadmin.totalSubscribers'), value: String(metrics.totals.totalSubscribers), tone: 'neutral' },
            { label: t('superadmin.activeSubscribers'), value: String(metrics.totals.activeSubscribers), tone: 'info' },
            { label: t('superadmin.totalUsers'), value: String(metrics.totals.totalUsers), tone: 'neutral' },
            { label: t('superadmin.activeUsers'), value: String(metrics.totals.activeUsers), tone: 'success' },
          ]}
        />
      ) : null}
      {isSuperAdmin ? <SuperAdminGrowthSection /> : null}
      <ZHCard title="Dashboard SuperAdmin">
        <p className="subtle sa-overviewHubIntro">Acceso rápido a todos los módulos de administración global.</p>
        <div className="sa-overviewHubGrid" role="list" aria-label="Módulos de SuperAdmin">
          <article className="sa-overviewHubCard" role="listitem">
            <div className="sa-overviewHubCardTop">
              <div className="sa-overviewHubCardTitle">🏢 {t('superadmin.tabCompanies')}</div>
              {metrics ? <span className="badge badge--gray">{metrics.totals.totalSubscribers} subscribers</span> : null}
            </div>
            <p className="subtle sa-overviewHubCardBody">Gestiona empresas, suscripciones y acceso a subscriber.</p>
            <ZHBtn className="sa-overviewHubCardAction" variant="secondary" size="sm" onClick={() => selectHomeTab('companies')}>
              Ir a empresas
            </ZHBtn>
          </article>
          <article className="sa-overviewHubCard" role="listitem">
            <div className="sa-overviewHubCardTop">
              <div className="sa-overviewHubCardTitle">💳 {t('superadmin.tabPlans')}</div>
              {activePlans.length > 0 ? <span className="badge badge--gray">{activePlans.length} activos</span> : null}
            </div>
            <p className="subtle sa-overviewHubCardBody">Configura planes, precios y asignaciones comerciales.</p>
            <ZHBtn className="sa-overviewHubCardAction" variant="secondary" size="sm" onClick={() => selectHomeTab('plans')}>
              Ir a planes
            </ZHBtn>
          </article>
          <article className="sa-overviewHubCard" role="listitem">
            <div className="sa-overviewHubCardTop">
              <div className="sa-overviewHubCardTitle">🧰 Configuración mínima</div>
              <span className="badge badge--gray">Simplificado</span>
            </div>
            <p className="subtle sa-overviewHubCardBody">El sistema usa plan comercial sin configuración granular de features.</p>
            <ZHBtn className="sa-overviewHubCardAction" variant="secondary" size="sm" onClick={() => selectHomeTab('menus')}>
              Ir a configuración
            </ZHBtn>
          </article>
          <article className="sa-overviewHubCard" role="listitem">
            <div className="sa-overviewHubCardTop">
              <div className="sa-overviewHubCardTitle">🧭 {t('superadmin.shell.menuAndPlans')}</div>
              {metrics ? <span className="badge badge--gray">{metrics.totals.activeUsers} usuarios activos</span> : null}
            </div>
            <p className="subtle sa-overviewHubCardBody">Define menú maestro, activaciones por plan y vista previa.</p>
            <ZHBtn className="sa-overviewHubCardAction" variant="primary" size="sm" onClick={() => selectHomeTab('menus')}>
              Ir a menú y planes
            </ZHBtn>
          </article>
        </div>
      </ZHCard>
    </div>
  );
}
