import { ZHBtn } from '../../components/zh/ZHForm';
import { ZHCard } from '../../components/zh/ZHCard';
import { ZHInlineRowRight } from '../../components/zh/ZHLayout';
import { LoadingState } from '../../components/PageShell';
import { ZHKpiPanel } from '../../components/zh/ZHDashboard';
import { PlatformGrowthSection } from '../../components/platform/PlatformGrowthSection';
import type { PlatformPanelPageState } from './usePlatformPanelPage';

type Props = Pick<
  PlatformPanelPageState,
  | 't'
  | 'loading'
  | 'metrics'
  | 'subscribers'
  | 'isPlatformOperator'
  | 'activePlans'
  | 'selectHomeTab'
  | 'openCreateSubscriber'
>;

export function PlatformPanelOverviewTab({
  t,
  loading,
  metrics,
  subscribers,
  isPlatformOperator,
  activePlans,
  selectHomeTab,
  openCreateSubscriber,
}: Props) {
  return (
    <div className="sa-overviewKpi">
      {!loading && subscribers.length === 0 && isPlatformOperator ? (
        <ZHCard title={t('platform.welcomeNoSubscribersTitle')}>
          <p className="subtle sa-welcome-note">{t('platform.welcomeNoSubscribersBody')}</p>
          <ZHInlineRowRight>
            <ZHBtn variant="secondary" size="sm" onClick={() => selectHomeTab('plans')}>
              {t('platform.welcomeGoPlans')}
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="sm"
              onClick={() => {
                selectHomeTab('companies');
                openCreateSubscriber();
              }}
              disabled={activePlans.length === 0}
              title={activePlans.length === 0 ? t('platform.createSubscriber.error.noPlans') : undefined}
            >
              {t('platform.welcomeCreateFirstCompany')}
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
          title={t('platform.metrics')}
          items={[
            { label: t('platform.totalSubscribers'), value: String(metrics.totals.totalSubscribers), tone: 'neutral' },
            { label: t('platform.activeSubscribers'), value: String(metrics.totals.activeSubscribers), tone: 'info' },
            { label: t('platform.totalUsers'), value: String(metrics.totals.totalUsers), tone: 'neutral' },
            { label: t('platform.activeUsers'), value: String(metrics.totals.activeUsers), tone: 'success' },
          ]}
        />
      ) : null}
      {isPlatformOperator ? <PlatformGrowthSection /> : null}
      <ZHCard title={t('platform.overview.hubTitle')}>
        <p className="subtle sa-overviewHubIntro">{t('platform.overview.hubIntro')}</p>
        <div className="sa-overviewHubGrid" role="list" aria-label={t('platform.overview.hubAria')}>
          <article className="sa-overviewHubCard" role="listitem">
            <div className="sa-overviewHubCardTop">
              <div className="sa-overviewHubCardTitle">🏢 {t('platform.tabCompanies')}</div>
              {metrics ? <span className="badge badge--gray">{metrics.totals.totalSubscribers} subscribers</span> : null}
            </div>
            <p className="subtle sa-overviewHubCardBody">Gestiona empresas, suscripciones y acceso a subscriber.</p>
            <ZHBtn className="sa-overviewHubCardAction" variant="secondary" size="sm" onClick={() => selectHomeTab('companies')}>
              Ir a empresas
            </ZHBtn>
          </article>
          <article className="sa-overviewHubCard" role="listitem">
            <div className="sa-overviewHubCardTop">
              <div className="sa-overviewHubCardTitle">💳 {t('platform.tabPlans')}</div>
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
              <div className="sa-overviewHubCardTitle">🧭 {t('platform.shell.menuAndPlans')}</div>
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
