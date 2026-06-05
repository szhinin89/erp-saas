import { useCallback, useEffect, useRef, useState } from 'react';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { useI18n } from '../../../i18n/i18n';
import { useMasterDataSuppliersPage } from './useMasterDataSuppliersPage';
import { MasterDataCompanySettingsModal } from './MasterDataCompanySettingsModal';
import { MasterDataPartnerWizard } from '../components/MasterDataPartnerWizard';
import { MasterDataPartnerResumenTab } from '../components/MasterDataPartnerResumenTab';
import { MasterDataPartnerListTab } from '../components/MasterDataPartnerListTab';
import { MasterDataPartnerToast } from '../components/MasterDataPartnerToast';
import { useMasterDataSuppliersUiStore } from '../store/masterDataPartnerUiStore';
import type {
  CreateBusinessPartnerBody,
  SupplierClassificationBody,
  SupplierConfigBody,
  UpdateBusinessPartnerBody,
} from '../types/businessPartner.types';
import {
  SRI_PAYMENT_METHOD_CODES,
  SUPPLIER_CATEGORIES, SUPPLIER_GOOD_TYPES, SUPPLIER_RATINGS,
  SUPPLIER_RISKS, SUPPLIER_SEGMENTS, SUPPLIER_TYPES,
} from '../types/businessPartner.types';
import '../../../styles/shared/products-catalog.css';
import './masterdata-pages.css';

const TABS = [
  { id: 'resumen' as const, labelKey: 'masterdata.suppliers.tabs.resumen', labelFb: 'Resumen',          icon: 'bar_chart_4_bars' },
  { id: 'listado' as const, labelKey: 'masterdata.suppliers.tabs.listado', labelFb: 'Listado',          icon: 'view_list' },
  { id: 'nuevo'   as const, labelKey: 'masterdata.suppliers.tabs.nuevo',   labelFb: 'Nuevo proveedor',  icon: 'add_box' },
] as const;

const DRAFT_KEY = 'erp.masterdata.suppliers.draft';

// ── SupplierConfigModal — Config SRI operativa (S3-A: incluye método de pago + exención) ──
function SupplierConfigModal({
  bpName, saving, error, onClose, onSave,
}: {
  bpName: string; saving: boolean; error?: string | null;
  onClose: () => void;
  onSave: (body: SupplierConfigBody) => void;
}) {
  const [taxSupportCode,       setTaxSupportCode]       = useState('');
  const [retentionVatCode,     setRetentionVatCode]     = useState('');
  const [retentionIncomeCode,  setRetentionIncomeCode]  = useState('');
  const [paymentTerms,         setPaymentTerms]         = useState('');
  const [paymentMethodCode,    setPaymentMethodCode]    = useState('');
  const [isRetentionExempt,    setIsRetentionExempt]    = useState(false);

  return (
    <div className="md-modal-backdrop" role="dialog" aria-modal="true">
      <form className="md-modal" onSubmit={(e) => {
        e.preventDefault();
        onSave({
          defaultTaxSupportCode:      taxSupportCode      || null,
          defaultRetentionVatCode:    retentionVatCode    || null,
          defaultRetentionIncomeCode: retentionIncomeCode || null,
          paymentTerms:               paymentTerms        || null,
          defaultPaymentMethodCode:   paymentMethodCode   || null,
          isRetentionExempt,
        });
      }}>
        <h2>Config SRI — Proveedor</h2>
        <p className="md-modal-sub">{bpName}</p>
        {error && <ZHPageNotice variant="error" message={error} className="md-modal-notice" />}
        <div className="pg-form-grid pg-form-grid--2">
          <ZHField label="Sustento tributario">
            <input className="zh-input mono" value={taxSupportCode}
              onChange={(e) => setTaxSupportCode(e.target.value)} disabled={saving} placeholder="01" maxLength={2} />
          </ZHField>
          <ZHField label="Código ret. IVA">
            <input className="zh-input mono" value={retentionVatCode}
              onChange={(e) => setRetentionVatCode(e.target.value)} disabled={saving} placeholder="725" maxLength={5} />
          </ZHField>
          <ZHField label="Código ret. Renta">
            <input className="zh-input mono" value={retentionIncomeCode}
              onChange={(e) => setRetentionIncomeCode(e.target.value)} disabled={saving} placeholder="303" maxLength={5} />
          </ZHField>
          <ZHField label="Condición de pago">
            <input className="zh-input" value={paymentTerms}
              onChange={(e) => setPaymentTerms(e.target.value)} disabled={saving} placeholder="30 días" maxLength={200} />
          </ZHField>
          <ZHField label="Método de pago SRI">
            <select className="zh-input" value={paymentMethodCode}
              onChange={(e) => setPaymentMethodCode(e.target.value)} disabled={saving}>
              <option value="">— Sin preferencia —</option>
              {SRI_PAYMENT_METHOD_CODES.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </ZHField>
          <ZHField label="Exento de retención">
            <label className="md-page-check">
              <input type="checkbox" checked={isRetentionExempt}
                onChange={(e) => setIsRetentionExempt(e.target.checked)} disabled={saving} />
              RISE / Microempresa / Sector público
            </label>
          </ZHField>
        </div>
        <div className="md-modal-actions">
          <ZHBtn variant="ghost" type="button" onClick={onClose} disabled={saving}>Cerrar</ZHBtn>
          <ZHBtn variant="primary" type="submit" disabled={saving}>{saving ? 'Guardando…' : 'Guardar'}</ZHBtn>
        </div>
      </form>
    </div>
  );
}

// ── SupplierClassificationModal — Clasificación estratégica (S3-B) ───────────
function SupplierClassificationModal({
  bpName, saving, error, onClose, onSave,
}: {
  bpName: string; saving: boolean; error?: string | null;
  onClose: () => void;
  onSave: (body: SupplierClassificationBody) => void;
}) {
  const [category,   setCategory]   = useState('');
  const [type,       setType]       = useState('');
  const [risk,       setRisk]       = useState('');
  const [rating,     setRating]     = useState('');
  const [goodType,   setGoodType]   = useState('');
  const [segment,    setSegment]    = useState('');
  const [paymentPref, setPaymentPref] = useState('');

  return (
    <div className="md-modal-backdrop" role="dialog" aria-modal="true">
      <form className="md-modal" onSubmit={(e) => {
        e.preventDefault();
        onSave({
          supplierCategory:       category    || null,
          supplierType:           type        || null,
          supplierRisk:           risk        || null,
          supplierRating:         rating      || null,
          primaryGoodType:        goodType    || null,
          supplierSegment:        segment     || null,
          paymentMethodPreference: paymentPref || null,
        });
      }}>
        <h2>Clasificación — Proveedor</h2>
        <p className="md-modal-sub">{bpName}</p>
        {error && <ZHPageNotice variant="error" message={error} className="md-modal-notice" />}
        <div className="pg-form-grid pg-form-grid--2">
          <ZHField label="Categoría">
            <select className="zh-input" value={category} onChange={(e) => setCategory(e.target.value)} disabled={saving}>
              <option value="">— Sin asignar —</option>
              {SUPPLIER_CATEGORIES.map((v) => <option key={v} value={v}>{v}</option>)}
            </select>
          </ZHField>
          <ZHField label="Tipo">
            <select className="zh-input" value={type} onChange={(e) => setType(e.target.value)} disabled={saving}>
              <option value="">— Sin asignar —</option>
              {SUPPLIER_TYPES.map((v) => <option key={v} value={v}>{v}</option>)}
            </select>
          </ZHField>
          <ZHField label="Riesgo">
            <select className="zh-input" value={risk} onChange={(e) => setRisk(e.target.value)} disabled={saving}>
              <option value="">— Sin asignar —</option>
              {SUPPLIER_RISKS.map((v) => <option key={v} value={v}>{v}</option>)}
            </select>
          </ZHField>
          <ZHField label="Rating">
            <select className="zh-input" value={rating} onChange={(e) => setRating(e.target.value)} disabled={saving}>
              <option value="">— Sin calificar —</option>
              {SUPPLIER_RATINGS.map((v) => <option key={v} value={v}>{v}</option>)}
            </select>
          </ZHField>
          <ZHField label="Tipo de bien">
            <select className="zh-input" value={goodType} onChange={(e) => setGoodType(e.target.value)} disabled={saving}>
              <option value="">— Sin asignar —</option>
              {SUPPLIER_GOOD_TYPES.map((v) => <option key={v} value={v}>{v}</option>)}
            </select>
          </ZHField>
          <ZHField label="Segmento estratégico">
            <select className="zh-input" value={segment} onChange={(e) => setSegment(e.target.value)} disabled={saving}>
              <option value="">— Sin asignar —</option>
              {SUPPLIER_SEGMENTS.map((v) => <option key={v} value={v}>{v}</option>)}
            </select>
          </ZHField>
          <ZHField label="Método de pago interno">
            <input className="zh-input" value={paymentPref}
              onChange={(e) => setPaymentPref(e.target.value)} disabled={saving}
              placeholder="Ej: Transferencia bancaria" maxLength={100} />
          </ZHField>
        </div>
        <div className="md-modal-actions">
          <ZHBtn variant="ghost" type="button" onClick={onClose} disabled={saving}>Cerrar</ZHBtn>
          <ZHBtn variant="primary" type="submit" disabled={saving}>{saving ? 'Guardando…' : 'Guardar'}</ZHBtn>
        </div>
      </form>
    </div>
  );
}

export function MasterDataSuppliersPage() {
  const { t } = useI18n();
  const page = useMasterDataSuppliersPage();
  const ui   = useMasterDataSuppliersUiStore;

  const activeTab      = ui((s) => s.activeTab);
  const editingPartner = ui((s) => s.editingPartner);
  const setActiveTab   = ui((s) => s.setActiveTab);
  const cancelEdit     = ui((s) => s.cancelEdit);
  const showToast      = ui((s) => s.showToast);
  const addActivity    = ui((s) => s.addActivity);
  const reset          = ui((s) => s.reset);
  const searchRef      = useRef<HTMLInputElement>(null);

  useEffect(() => { reset(); return () => reset(); }, [reset]);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault(); setActiveTab('listado');
        setTimeout(() => searchRef.current?.focus(), 80);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [setActiveTab]);

  const handleCreate = useCallback(
    async (body: CreateBusinessPartnerBody) => {
      const ok = await page.createSupplier(body);
      if (!ok) return false;
      addActivity(body.legalName, 'created');
      showToast('Proveedor creado correctamente.', 'success');
      setActiveTab('listado');
      return true;
    },
    [page, addActivity, showToast, setActiveTab],
  );

  const handleUpdate = useCallback(
    async (body: UpdateBusinessPartnerBody) => {
      if (!editingPartner) return false;
      const ok = await page.updateSupplier(editingPartner.id, body);
      if (!ok) return false;
      addActivity(body.legalName, 'updated');
      showToast('Proveedor actualizado correctamente.', 'success');
      cancelEdit();
      return true;
    },
    [editingPartner, page, addActivity, showToast, cancelEdit],
  );

  const handleAssign = useCallback(
    async (id: string) => {
      const ok = await page.assignAsSupplier(id);
      if (!ok) return false;
      const bp = page.suppliers.find((s) => s.id === id);
      addActivity(bp?.legalName ?? id, 'assigned');
      showToast('Rol de proveedor asignado correctamente.', 'success');
      setActiveTab('listado');
      return true;
    },
    [page, addActivity, showToast, setActiveTab],
  );

  const handleDisable = useCallback(
    async (id: string) => {
      await page.disableSupplier(id);
      const bp = page.suppliers.find((s) => s.id === id);
      if (bp) { addActivity(bp.legalName, 'disabled'); showToast('Proveedor desactivado.', 'info'); }
    },
    [page, addActivity, showToast],
  );

  const handleActivate = useCallback(
    async (id: string) => {
      await page.activateSupplier(id);
      const bp = page.suppliers.find((s) => s.id === id);
      if (bp) { addActivity(bp.legalName, 'enabled'); showToast('Proveedor activado.', 'info'); }
    },
    [page, addActivity, showToast],
  );

  if (!page.canView) return <NoAccessPage title={t('masterdata.suppliers.title')} />;

  return (
    <ErpPageTemplate kicker="MasterData"
      title={t('masterdata.suppliers.title')}
      subtitle={t('masterdata.suppliers.subtitle')}>

      {page.listError   && <ZHPageNotice variant="error" message={page.listError} />}
      {page.inlineError && <ZHPageNotice variant="error" message={page.inlineError} />}

      <div className="prd-tabs" role="tablist">
        {TABS.map((tab) => {
          const active = activeTab === tab.id;
          return (
            <button key={tab.id} type="button" role="tab" aria-selected={active}
              className={`prd-tab-btn ${active ? 'prd-tab-btn--active' : ''}`}
              onClick={() => setActiveTab(tab.id)}>
              <span className="material-symbols-outlined prd-tab-icon">{tab.icon}</span>
              {t(tab.labelKey, tab.labelFb)}
            </button>
          );
        })}
      </div>

      <div className="prd-tab-content">
        {activeTab === 'resumen' && (
          <MasterDataPartnerResumenTab role="supplier" partners={page.suppliers}
            totalCount={page.totalCount} store={ui} />
        )}
        {activeTab === 'listado' && (
          <MasterDataPartnerListTab
            role="supplier" store={ui}
            canCreate={page.canCreate} canUpdate={page.canUpdate}
            canDisable={page.canDisable} canConfigure={page.canConfigure}
            loading={page.loading} saving={page.saving}
            partners={page.suppliers} totalCount={page.totalCount}
            search={page.search} setSearch={page.setSearch}
            showInactive={page.showInactive} setShowInactive={page.setShowInactive}
            page={page.page} totalPages={page.totalPages} setPage={page.setPage}
            searchInputRef={searchRef}
            onSettings={(bp) => void page.openSettings(bp)}
            onSupplierProfile={(bp) => void page.openSupplierConfig(bp)}
            onAddAsCustomer={(id) => void page.addAsCustomer(id)}
            onActivate={handleActivate}
            onDisable={handleDisable}
          />
        )}
        {activeTab === 'nuevo' && (page.canCreate || editingPartner) && (
          <MasterDataPartnerWizard
            key={editingPartner?.id ?? 'create'}
            role="supplier" draftKey={DRAFT_KEY}
            submitting={page.saving} wizardError={page.modalError}
            editingPartner={editingPartner}
            onSubmitCreate={handleCreate} onSubmitUpdate={handleUpdate}
            onAssignRole={handleAssign} onCancel={cancelEdit}
          />
        )}
      </div>

      {page.settingsBp && page.canConfigure && (
        <MasterDataCompanySettingsModal
          partner={page.settingsBp}
          initialSettings={page.settingsData}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeSettings}
          onSave={(payload) => void page.saveSettings(page.settingsBp!.id, payload)}
          onBlock={(reason) => void page.blockSupplier(page.settingsBp!.id, reason)}
          onUnblock={() => void page.unblockSupplier(page.settingsBp!.id)}
        />
      )}

      {page.supplierConfigBp && (
        <SupplierConfigModal
          bpName={page.supplierConfigBp.bp.legalName}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeSupplierConfig}
          onSave={(body) => void page.saveSupplierConfig(
            page.supplierConfigBp!.bp.id,
            page.supplierConfigBp!.roleId,
            body
          )}
        />
      )}

      {page.supplierClassificationBp && (
        <SupplierClassificationModal
          bpName={page.supplierClassificationBp.bp.legalName}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeSupplierClassification}
          onSave={(body) => void page.saveSupplierClassification(
            page.supplierClassificationBp!.bp.id,
            page.supplierClassificationBp!.roleId,
            body
          )}
        />
      )}

      <MasterDataPartnerToast store={ui} />
    </ErpPageTemplate>
  );
}
