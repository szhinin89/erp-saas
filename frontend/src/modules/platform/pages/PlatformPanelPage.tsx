import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { PlatformCrudTemplate } from '../../../templates/PlatformCrudTemplate';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { ZHDashboardScaffold } from '../../../components/zh/ZHDashboard';
import { PlatformPlansSection } from '../../../components/platform/PlatformPlansSection';
import { PlatformMenuBuilderSection } from '../../../components/platform/PlatformMenuBuilderSection';
import { usePlatformPanelPage } from '../usePlatformPanelPage';
import { PlatformPanelOverviewTab } from '../PlatformPanelOverviewTab';
import { PlatformPanelCompaniesTab } from '../PlatformPanelCompaniesTab';
import { PlatformPanelCreateSubscriberModal } from '../PlatformPanelCreateSubscriberModal';
import { PlatformPanelSubscriptionModal } from '../PlatformPanelSubscriptionModal';
import type { PlatformHomeTab } from '../platformPanelUtils';
import '../../../components/zh/ZHFormTabs.css';
import './PlatformPanelPage.css';

export type PlatformPanelPageProps = {
  /** Cuando se usa dentro de <PlatformLayout>: oculta pestañas y fija la sección. */
  embeddedTab?: PlatformHomeTab;
  shellLayout?: boolean;
};

/**
 * @deprecated Shell monolítico legacy. Rutas canónicas: `PlatformOverviewPage`, `PlatformSubscribersPage`,
 * `PlatformSubscriberDetailPage`, `PlatformPlansPage`, etc. bajo `/platform/*`.
 * Conservado para re-exports y `PlatformCompaniesShellPage` (sin ruta activa en router).
 */
export function PlatformPanelPage({ embeddedTab, shellLayout }: PlatformPanelPageProps = {}) {
  const panel = usePlatformPanelPage({ embeddedTab, shellLayout });
  const {
    t,
    navigate,
    isPlatformOperator,
    hasSelectedSubscriber,
    homeTab,
    selectHomeTab,
    error,
    loading,
    metrics,
    subscribers,
    activePlans,
    filtered,
    planLabelForSubscriber,
    q,
    setQ,
    openCreateSubscriber,
    openSubscriptionModal,
    createSubscriberOpen,
    setCreateSubscriberOpen,
    createBusy,
    createError,
    createForm,
    setCreateForm,
    createPlanCode,
    setCreatePlanCode,
    createRestrictModules,
    setCreateRestrictModules,
    createModuleChecks,
    setCreateModuleChecks,
    saveCreateSubscriber,
    subModalOpen,
    setSubModalOpen,
    subModalSubscriber,
    subPlanCode,
    setSubPlanCode,
    subRestrict,
    setSubRestrict,
    subModuleChecks,
    setSubModuleChecks,
    subBusy,
    subError,
    saveSubscriptionModal,
    moduleLabel,
    slugify,
  } = panel;

  return (
    <PlatformCrudTemplate
      title={shellLayout ? t('platform.tabCompanies') : t('platform.title')}
      shellLayout={!!shellLayout}
      subtitle={isPlatformOperator && !hasSelectedSubscriber ? t('platform.subtitle') : undefined}
      subscriberGuardAction={
        <ZHBtn variant="primary" size="sm" onClick={() => navigate('/saas/overview')}>
          {t('platform.goToSubscriber')}
        </ZHBtn>
      }
    >
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}

      <ZHDashboardScaffold>
        {!shellLayout ? (
          <div className="sa-panelTabsWrap">
            <div className="zh-form-tabs sa-panelTabs" role="tablist" aria-label={t('platform.title')}>
              <button
                type="button"
                role="tab"
                aria-selected={homeTab === 'overview'}
                className={homeTab === 'overview' ? 'is-active' : ''}
                onClick={() => selectHomeTab('overview')}
              >
                {t('platform.tabOverview')}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={homeTab === 'companies'}
                className={homeTab === 'companies' ? 'is-active' : ''}
                onClick={() => selectHomeTab('companies')}
              >
                {t('platform.tabCompanies')}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={homeTab === 'plans'}
                className={homeTab === 'plans' ? 'is-active' : ''}
                onClick={() => selectHomeTab('plans')}
              >
                {t('platform.tabPlans')}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={homeTab === 'menus'}
                className={homeTab === 'menus' ? 'is-active' : ''}
                onClick={() => selectHomeTab('menus')}
              >
                {t('platform.tabMenus')}
              </button>
            </div>
          </div>
        ) : null}

        {homeTab === 'overview' ? (
          <PlatformPanelOverviewTab
            t={t}
            loading={loading}
            metrics={metrics}
            subscribers={subscribers}
            isPlatformOperator={isPlatformOperator}
            activePlans={activePlans}
            selectHomeTab={selectHomeTab}
            openCreateSubscriber={openCreateSubscriber}
          />
        ) : homeTab === 'companies' ? (
          <PlatformPanelCompaniesTab
            t={t}
            navigate={navigate}
            loading={loading}
            error={error}
            subscribers={subscribers}
            q={q}
            setQ={setQ}
            filtered={filtered}
            planLabelForSubscriber={planLabelForSubscriber}
            openCreateSubscriber={openCreateSubscriber}
            openSubscriptionModal={openSubscriptionModal}
          />
        ) : homeTab === 'plans' ? (
          <>{isPlatformOperator ? <PlatformPlansSection /> : null}</>
        ) : (
          <>{isPlatformOperator ? <PlatformMenuBuilderSection /> : null}</>
        )}
      </ZHDashboardScaffold>

      <PlatformPanelCreateSubscriberModal
        t={t}
        createSubscriberOpen={createSubscriberOpen}
        setCreateSubscriberOpen={setCreateSubscriberOpen}
        createBusy={createBusy}
        createError={createError}
        createForm={createForm}
        setCreateForm={setCreateForm}
        createPlanCode={createPlanCode}
        setCreatePlanCode={setCreatePlanCode}
        createRestrictModules={createRestrictModules}
        setCreateRestrictModules={setCreateRestrictModules}
        createModuleChecks={createModuleChecks}
        setCreateModuleChecks={setCreateModuleChecks}
        activePlans={activePlans}
        moduleLabel={moduleLabel}
        saveCreateSubscriber={saveCreateSubscriber}
        slugify={slugify}
      />

      <PlatformPanelSubscriptionModal
        t={t}
        subModalOpen={subModalOpen}
        setSubModalOpen={setSubModalOpen}
        subModalSubscriber={subModalSubscriber}
        subPlanCode={subPlanCode}
        setSubPlanCode={setSubPlanCode}
        subRestrict={subRestrict}
        setSubRestrict={setSubRestrict}
        subModuleChecks={subModuleChecks}
        setSubModuleChecks={setSubModuleChecks}
        subBusy={subBusy}
        subError={subError}
        activePlans={activePlans}
        moduleLabel={moduleLabel}
        saveSubscriptionModal={saveSubscriptionModal}
      />
    </PlatformCrudTemplate>
  );
}
