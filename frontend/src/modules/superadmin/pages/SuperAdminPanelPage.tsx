import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { SuperAdminCrudTemplate } from '../../../templates/SuperAdminCrudTemplate';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { ZHDashboardScaffold } from '../../../components/zh/ZHDashboard';
import { SuperAdminPlansSection } from '../../../components/superadmin/SuperAdminPlansSection';
import { SuperAdminMenuBuilderSection } from '../../../components/superadmin/SuperAdminMenuBuilderSection';
import { useSuperAdminPanelPage } from '../useSuperAdminPanelPage';
import { SuperAdminPanelOverviewTab } from '../SuperAdminPanelOverviewTab';
import { SuperAdminPanelCompaniesTab } from '../SuperAdminPanelCompaniesTab';
import { SuperAdminPanelCreateSubscriberModal } from '../SuperAdminPanelCreateSubscriberModal';
import { SuperAdminPanelSubscriptionModal } from '../SuperAdminPanelSubscriptionModal';
import type { SuperAdminHomeTab } from '../superAdminPanelUtils';
import '../../../components/zh/ZHFormTabs.css';
import './SuperAdminPanelPage.css';

export type SuperAdminPanelPageProps = {
  /** Cuando se usa dentro de <SuperAdminLayout>: oculta pestañas y fija la sección. */
  embeddedTab?: SuperAdminHomeTab;
  shellLayout?: boolean;
};

export function SuperAdminPanelPage({ embeddedTab, shellLayout }: SuperAdminPanelPageProps = {}) {
  const panel = useSuperAdminPanelPage({ embeddedTab, shellLayout });
  const {
    t,
    navigate,
    isSuperAdmin,
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
    switching,
    openCreateSubscriber,
    openSubscriptionModal,
    handleSwitch,
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
    <SuperAdminCrudTemplate
      title={shellLayout ? t('superadmin.tabCompanies') : t('superadmin.title')}
      shellLayout={!!shellLayout}
      subtitle={isSuperAdmin && !hasSelectedSubscriber ? t('superadmin.subtitle') : undefined}
      subscriberGuardAction={
        <ZHBtn variant="primary" size="sm" onClick={() => navigate('/saas/overview')}>
          {t('superadmin.goToSubscriber')}
        </ZHBtn>
      }
    >
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}

      <ZHDashboardScaffold>
        {!shellLayout ? (
          <div className="sa-panelTabsWrap">
            <div className="zh-form-tabs sa-panelTabs" role="tablist" aria-label={t('superadmin.title')}>
              <button
                type="button"
                role="tab"
                aria-selected={homeTab === 'overview'}
                className={homeTab === 'overview' ? 'is-active' : ''}
                onClick={() => selectHomeTab('overview')}
              >
                {t('superadmin.tabOverview')}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={homeTab === 'companies'}
                className={homeTab === 'companies' ? 'is-active' : ''}
                onClick={() => selectHomeTab('companies')}
              >
                {t('superadmin.tabCompanies')}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={homeTab === 'plans'}
                className={homeTab === 'plans' ? 'is-active' : ''}
                onClick={() => selectHomeTab('plans')}
              >
                {t('superadmin.tabPlans')}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={homeTab === 'menus'}
                className={homeTab === 'menus' ? 'is-active' : ''}
                onClick={() => selectHomeTab('menus')}
              >
                {t('superadmin.tabMenus')}
              </button>
            </div>
          </div>
        ) : null}

        {homeTab === 'overview' ? (
          <SuperAdminPanelOverviewTab
            t={t}
            loading={loading}
            metrics={metrics}
            subscribers={subscribers}
            isSuperAdmin={isSuperAdmin}
            activePlans={activePlans}
            selectHomeTab={selectHomeTab}
            openCreateSubscriber={openCreateSubscriber}
          />
        ) : homeTab === 'companies' ? (
          <SuperAdminPanelCompaniesTab
            t={t}
            navigate={navigate}
            loading={loading}
            error={error}
            subscribers={subscribers}
            q={q}
            setQ={setQ}
            switching={switching}
            filtered={filtered}
            planLabelForSubscriber={planLabelForSubscriber}
            openCreateSubscriber={openCreateSubscriber}
            openSubscriptionModal={openSubscriptionModal}
            handleSwitch={handleSwitch}
          />
        ) : homeTab === 'plans' ? (
          <>{isSuperAdmin ? <SuperAdminPlansSection /> : null}</>
        ) : (
          <>{isSuperAdmin ? <SuperAdminMenuBuilderSection /> : null}</>
        )}
      </ZHDashboardScaffold>

      <SuperAdminPanelCreateSubscriberModal
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

      <SuperAdminPanelSubscriptionModal
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
    </SuperAdminCrudTemplate>
  );
}
