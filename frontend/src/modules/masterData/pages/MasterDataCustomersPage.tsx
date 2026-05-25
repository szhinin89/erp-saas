import { useCallback, useEffect, useRef } from 'react';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../i18n/i18n';
import { useMasterDataCustomersPage } from './useMasterDataCustomersPage';
import { MasterDataCompanySettingsModal } from './MasterDataCompanySettingsModal';
import { MasterDataCustomerNotesModal } from './MasterDataCustomerNotesModal';
import { MasterDataPartnerWizard } from '../components/MasterDataPartnerWizard';
import { MasterDataPartnerResumenTab } from '../components/MasterDataPartnerResumenTab';
import { MasterDataPartnerListTab } from '../components/MasterDataPartnerListTab';
import { MasterDataPartnerToast } from '../components/MasterDataPartnerToast';
import { useMasterDataCustomersUiStore } from '../store/masterDataPartnerUiStore';
import type { CreateBusinessPartnerBody, UpdateBusinessPartnerBody } from '../types/businessPartner.types';
import '../../../pages/ProductsPage.css';
import './masterdata-pages.css';

const TABS = [
  { id: 'resumen' as const, labelKey: 'masterdata.customers.tabs.resumen', labelFb: 'Resumen', icon: 'bar_chart_4_bars' },
  { id: 'listado' as const, labelKey: 'masterdata.customers.tabs.listado', labelFb: 'Listado', icon: 'view_list' },
  { id: 'nuevo' as const, labelKey: 'masterdata.customers.tabs.nuevo', labelFb: 'Nuevo cliente', icon: 'add_box' },
] as const;

const DRAFT_KEY = 'erp.masterdata.customers.draft';

export function MasterDataCustomersPage() {
  const { t } = useI18n();
  const page = useMasterDataCustomersPage();
  const ui = useMasterDataCustomersUiStore;

  const activeTab = ui((s) => s.activeTab);
  const editingPartner = ui((s) => s.editingPartner);
  const setActiveTab = ui((s) => s.setActiveTab);
  const cancelEdit = ui((s) => s.cancelEdit);
  const showToast = ui((s) => s.showToast);
  const addActivity = ui((s) => s.addActivity);
  const reset = ui((s) => s.reset);

  const searchRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    reset();
    return () => reset();
  }, [reset]);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        setActiveTab('listado');
        setTimeout(() => searchRef.current?.focus(), 80);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [setActiveTab]);

  const handleCreate = useCallback(
    async (body: CreateBusinessPartnerBody) => {
      const ok = await page.createCustomer(body);
      if (!ok) return false;
      addActivity(body.legalName, 'created');
      showToast(t('masterdata.customers.created.success', 'Cliente creado correctamente.'), 'success');
      setActiveTab('listado');
      return true;
    },
    [page, addActivity, showToast, setActiveTab, t],
  );

  const handleUpdate = useCallback(
    async (body: UpdateBusinessPartnerBody) => {
      if (!editingPartner) return false;
      const ok = await page.updateCustomer(editingPartner.id, body);
      if (!ok) return false;
      addActivity(body.legalName, 'updated');
      showToast(t('masterdata.customers.updated.success', 'Cliente actualizado correctamente.'), 'success');
      cancelEdit();
      return true;
    },
    [editingPartner, page, addActivity, showToast, cancelEdit, t],
  );

  const handleAssign = useCallback(
    async (id: string) => {
      const ok = await page.assignAsCustomer(id);
      if (!ok) return false;
      const bp = page.customers.find((c) => c.id === id);
      addActivity(bp?.legalName ?? id, 'assigned');
      showToast(t('masterdata.customers.assigned.success', 'Rol de cliente asignado correctamente.'), 'success');
      setActiveTab('listado');
      return true;
    },
    [page, addActivity, showToast, setActiveTab, t],
  );

  const handleDisable = useCallback(
    async (id: string) => {
      await page.disableCustomer(id);
      const bp = page.customers.find((c) => c.id === id);
      if (bp) {
        addActivity(bp.legalName, 'disabled');
        showToast(t('masterdata.customers.disabled.success', 'Cliente desactivado.'), 'info');
      }
    },
    [page, addActivity, showToast, t],
  );

  const handleActivate = useCallback(
    async (id: string) => {
      await page.activateCustomer(id);
      const bp = page.customers.find((c) => c.id === id);
      if (bp) {
        addActivity(bp.legalName, 'enabled');
        showToast(t('masterdata.customers.enabled.success', 'Cliente activado.'), 'info');
      }
    },
    [page, addActivity, showToast, t],
  );

  if (!page.canView) {
    return <NoAccessPage title={t('masterdata.customers.title')} />;
  }

  return (
    <ErpPageTemplate
      kicker="MasterData"
      title={t('masterdata.customers.title')}
      subtitle={t('masterdata.customers.subtitle')}
    >
      {page.listError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={page.listError} />}
      {page.inlineError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={page.inlineError} />}

      <div className="prd-tabs" role="tablist" aria-label={t('masterdata.customers.tabs.aria', 'Secciones de clientes')}>
        {TABS.map((tab) => {
          const active = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              aria-selected={active}
              className={`prd-tab-btn ${active ? 'prd-tab-btn--active' : ''}`}
              onClick={() => setActiveTab(tab.id)}
            >
              <span className="material-symbols-outlined prd-tab-icon">{tab.icon}</span>
              {t(tab.labelKey, tab.labelFb)}
            </button>
          );
        })}
      </div>

      <div className="prd-tab-content">
        {activeTab === 'resumen' && (
          <MasterDataPartnerResumenTab
            role="customer"
            partners={page.customers}
            totalCount={page.totalCount}
            store={ui}
          />
        )}
        {activeTab === 'listado' && (
          <MasterDataPartnerListTab
            role="customer"
            store={ui}
            canCreate={page.canCreate}
            canUpdate={page.canUpdate}
            canDisable={page.canDisable}
            canConfigure={page.canConfigure}
            loading={page.loading}
            saving={page.saving}
            partners={page.customers}
            totalCount={page.totalCount}
            search={page.search}
            setSearch={page.setSearch}
            showInactive={page.showInactive}
            setShowInactive={page.setShowInactive}
            page={page.page}
            totalPages={page.totalPages}
            setPage={page.setPage}
            searchInputRef={searchRef}
            onNotes={page.openNotes}
            onSettings={page.openSettings}
            onAddAsSupplier={page.addAsSupplier}
            onActivate={handleActivate}
            onDisable={handleDisable}
          />
        )}
        {activeTab === 'nuevo' && (page.canCreate || editingPartner) && (
          <MasterDataPartnerWizard
            key={editingPartner?.id ?? 'create'}
            role="customer"
            draftKey={DRAFT_KEY}
            submitting={page.saving}
            wizardError={page.modalError}
            editingPartner={editingPartner}
            onSubmitCreate={handleCreate}
            onSubmitUpdate={handleUpdate}
            onAssignRole={handleAssign}
            onCancel={cancelEdit}
          />
        )}
      </div>

      {page.notesBp && (
        <MasterDataCustomerNotesModal
          partner={page.notesBp}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeNotes}
          onSave={(notes) => void page.saveNotes(page.notesBp!.id, notes)}
        />
      )}

      {page.settingsBp && page.canConfigure && (
        <MasterDataCompanySettingsModal
          partner={page.settingsBp}
          initialSettings={page.settingsData}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeSettings}
          onSave={(payload) => void page.saveCompanySettings(page.settingsBp!.id, payload)}
        />
      )}

      <MasterDataPartnerToast store={ui} />
    </ErpPageTemplate>
  );
}
