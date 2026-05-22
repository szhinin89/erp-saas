import { PageShell, EmptyState, LoadingState, NoAccessPage } from '../../../components/PageShell';
import { EntityAuditPanel } from '../../../components/EntityAuditPanel';
import { ZHFormSection, ZHGrid, ZHField, ZHBtn } from '../../../components/zh/ZHForm';
import { ZHCard } from '../../../components/zh/ZHCard';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { NavigationMenuEditorPanel } from '../../../components/superadmin/NavigationMenuEditorPanel';
import { ConfigManagementPanel } from '../../../components/config/ConfigManagementPanel';
import { FeatureGate } from '../../config';
import { useSuperAdminGate } from '../../../hooks/useSuperAdminGate';
import { CompaniesPageDataTab } from './CompaniesPageDataTab';
import { ELECTRONIC_BILLING_TRIAL_KEY, useCompaniesPage } from './useCompaniesPage';
import './CompaniesPage.css';

function CompaniesPage() {
  const page = useCompaniesPage();
  const { isSuperAdmin } = useSuperAdminGate();

  if (!isSuperAdmin) {
    return <NoAccessPage title={page.t('companies.title')} />;
  }

  const {
    t,
    subscriberId,
    maxActiveSubscribers,
    maxIdentityUsers,
    items,
    listQuery,
    setListQuery,
    loading,
    error,
    creating,
    tab,
    setTab,
    auditSubscriberId,
    auditRefreshKey,
    detailSubscriberId,
    subscriberDetail,
    detailLoading,
    detailError,
    detailSaving,
    detailSaveError,
    detailSaveOk,
    globalParamSaving,
    globalParamError,
    globalParamOk,
    electronicBillingTrialEnabled,
    setElectronicBillingTrialEnabled,
    globalParamScopeResolved,
    globalConfigCount,
    stockNegativeEnabled,
    filtered,
    linkExistingAdmin,
    register,
    errors,
    registerDetail,
    detailFieldErrors,
    refresh,
    clearSubscriberDetailView,
    selectSubscriberRow,
    submit,
    saveTenantCompanyDetail,
    saveGlobalParameters,
  } = page;

  return (
    <PageShell kicker={t('app.nav.group.home')} title={t('companies.title')} subtitle={t('companies.subtitle')}>
      <ZHCard
        title={t('companies.title')}
        actions={
          tab === 'data' ? (
            <ZHBtn variant="secondary" size="sm" type="button" onClick={() => void refresh()} disabled={loading}>
              {t('companies.refresh')}
            </ZHBtn>
          ) : null
        }
      >
        {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
        <div className="zh-form-tabs" role="tablist">
          <button type="button" className={tab === 'data' ? 'is-active' : ''} onClick={() => setTab('data')}>
            {t('common.formTab.data')}
          </button>
          <button type="button" className={tab === 'globalParameters' ? 'is-active' : ''} onClick={() => setTab('globalParameters')}>
            {t('companies.tabGlobalParameters')}
          </button>
          <button type="button" className={tab === 'mainNavigation' ? 'is-active' : ''} onClick={() => setTab('mainNavigation')}>
            {t('companies.tabMainNavigation')}
          </button>
          <button type="button" className={tab === 'audit' ? 'is-active' : ''} onClick={() => setTab('audit')}>
            {t('common.formTab.audit')}
          </button>
        </div>

        {tab === 'mainNavigation' ? (
          <div className="companies-nav-menu-tab">
            <p className="zh-help-text companies-nav-menu-intro">{t('superadmin.navigationMenu.subtitle')}</p>
            <NavigationMenuEditorPanel />
          </div>
        ) : null}

        {tab === 'globalParameters' ? (
          !detailSubscriberId ? (
            <EmptyState message={t('audit.pickRow')} />
          ) : detailLoading ? (
            <LoadingState />
          ) : detailError ? (
            <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={detailError} />
          ) : subscriberDetail ? (
            !subscriberDetail.planCode?.trim() ? (
              <ZHPageNotice
                variant="warning"
                message={t('companies.globalParams.noPlanTitle')}
                detail={t('companies.globalParams.noPlanDetail')}
              />
            ) : (
              <div className="companies-inner-stack">
                <ZHCard title={t('companies.globalParams.title')}>
                  <form
                    onSubmit={(e) => {
                      e.preventDefault();
                      void saveGlobalParameters();
                    }}
                  >
                    {globalParamOk ? (
                      <ZHPageNotice variant="success" message={t('companies.globalParams.saveSuccess')} />
                    ) : null}
                    {globalParamError ? (
                      <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={globalParamError} />
                    ) : null}
                    <ZHPageNotice
                      variant="info"
                      message={t('companies.globalParams.modelTitle')}
                      detail={`${t('companies.globalParams.modelDetail')} ${t('companies.globalParams.structuredKeyExample')}: ${ELECTRONIC_BILLING_TRIAL_KEY}`}
                    />
                    <FeatureGate feature="ui.cliente.mostrar_ruc" fallback={<ZHPageNotice variant="warning" message="Feature flag ui.cliente.mostrar_ruc está desactivada en este subscriber." />}>
                      <ZHPageNotice variant="info" message="Feature flag ui.cliente.mostrar_ruc activa para el subscriber actual." />
                    </FeatureGate>
                    <ZHPageNotice
                      variant={stockNegativeEnabled ? 'warning' : 'info'}
                      message={
                        stockNegativeEnabled
                          ? 'ventas.stock.permitir_negativo = true (se permite stock negativo).'
                          : 'ventas.stock.permitir_negativo = false (stock negativo bloqueado).'
                      }
                    />
                    <ZHFormSection title={t('companies.globalParams.entitiesTitle')}>
                      <ZHGrid cols={3}>
                        <ZHField label={t('companies.globalParams.entity.global.title')} hint={t('companies.globalParams.entity.global.hint')}>
                          <div className="companies-readonly-value">{t('companies.globalParams.entity.global.help')}</div>
                        </ZHField>
                        <ZHField label={t('companies.globalParams.entity.module.title')} hint={t('companies.globalParams.entity.module.hint')}>
                          <div className="companies-readonly-value">{t('companies.globalParams.entity.module.help')}</div>
                        </ZHField>
                        <ZHField label={t('companies.globalParams.entity.feature.title')} hint={t('companies.globalParams.entity.feature.hint')}>
                          <div className="companies-readonly-value">{t('companies.globalParams.entity.feature.help')}</div>
                        </ZHField>
                      </ZHGrid>
                    </ZHFormSection>
                    <ZHFormSection title={t('companies.globalParams.sectionBilling')}>
                      <ZHField
                        label={t('companies.globalParams.electronicBillingTrial.label')}
                        hint={`${t('companies.globalParams.electronicBillingTrial.hint')} · ${t('companies.globalParams.structuredKeyLabel')}: ${ELECTRONIC_BILLING_TRIAL_KEY}`}
                      >
                        <label className="companies-checkbox-label">
                          <input
                            type="checkbox"
                            checked={electronicBillingTrialEnabled}
                            disabled={globalParamSaving}
                            onChange={(e) => setElectronicBillingTrialEnabled(e.target.checked)}
                          />
                          <span>{t('companies.globalParams.electronicBillingTrial.help')}</span>
                        </label>
                      </ZHField>
                      <ZHGrid cols={2}>
                        <ZHField label={t('companies.globalParams.activeScopeLabel')}>
                          <div className="companies-readonly-value">
                            {globalParamScopeResolved
                              ? t(`companies.globalParams.scope.${globalParamScopeResolved}`)
                              : t('companies.globalParams.scope.notFound')}
                          </div>
                        </ZHField>
                        <ZHField label={t('companies.globalParams.totalGlobalKeysLabel')}>
                          <div className="companies-readonly-value">{globalConfigCount}</div>
                        </ZHField>
                      </ZHGrid>
                    </ZHFormSection>
                    <div className="zh-form-actions-row zh-form-actions-row--end">
                      <ZHBtn variant="primary" size="sm" type="submit" disabled={globalParamSaving}>
                        {globalParamSaving ? t('companies.globalParams.saving') : t('companies.globalParams.save')}
                      </ZHBtn>
                    </div>
                  </form>
                </ZHCard>
                <ConfigManagementPanel
                  subscriberId={detailSubscriberId}
                  canManage={isSuperAdmin}
                  title="CRUD de configuración por alcance"
                  subtitle="Permite crear y editar claves estructuradas para Global, Module y Feature con validación por tipo."
                />
              </div>
            )
          ) : null
        ) : null}

        {tab === 'data' ? (
          <CompaniesPageDataTab
            items={items}
            filtered={filtered}
            listQuery={listQuery}
            setListQuery={setListQuery}
            loading={loading}
            creating={creating}
            detailSubscriberId={detailSubscriberId}
            subscriberDetail={subscriberDetail}
            detailLoading={detailLoading}
            detailError={detailError}
            detailSaving={detailSaving}
            detailSaveOk={detailSaveOk}
            detailSaveError={detailSaveError}
            subscriberId={subscriberId}
            maxActiveSubscribers={maxActiveSubscribers}
            maxIdentityUsers={maxIdentityUsers}
            linkExistingAdmin={linkExistingAdmin}
            register={register}
            errors={errors}
            registerDetail={registerDetail}
            detailFieldErrors={detailFieldErrors}
            onClearDetail={clearSubscriberDetailView}
            onSelectRow={selectSubscriberRow}
            onSubmitCreate={submit}
            onSubmitDetail={saveTenantCompanyDetail}
          />
        ) : null}

        {tab === 'audit' ? (
          auditSubscriberId ? (
            <EntityAuditPanel
              entityType="Subscriber"
              entityId={auditSubscriberId}
              take={10}
              refreshKey={auditRefreshKey}
            />
          ) : (
            <EmptyState message={t('audit.pickRow')} />
          )
        ) : null}
      </ZHCard>
    </PageShell>
  );
}

export { CompaniesPage };
export default CompaniesPage;
