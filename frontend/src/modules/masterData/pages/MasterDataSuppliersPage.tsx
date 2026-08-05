import { useCallback, useEffect, useRef, useState } from "react";
import { NoAccessPage } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import {
  ZHField,
  ZHGrid,
  ZHToggle,
  ZHFormActions,
} from "../../../components/zh/ZHForm";
import { ZHModal } from "../../../components/zh/ZHModal";
import { useI18n } from "../../../i18n/i18n";
import { useMasterDataSuppliersPage } from "./useMasterDataSuppliersPage";
import { MasterDataCompanySettingsModal } from "./MasterDataCompanySettingsModal";
import { MasterDataPartnerWizard } from "../components/MasterDataPartnerWizard";
import { MasterDataPartnerResumenTab } from "../components/MasterDataPartnerResumenTab";
import { MasterDataPartnerListTab } from "../components/MasterDataPartnerListTab";
import { useMasterDataSuppliersUiStore } from "../store/masterDataPartnerUiStore";
import { message } from "../../../lib/messages";
import type {
  CreateBusinessPartnerBody,
  SupplierClassificationBody,
  SupplierConfigBody,
  UpdateBusinessPartnerBody,
} from "../types/businessPartner.types";
import {
  SRI_PAYMENT_METHOD_CODES,
  SUPPLIER_CATEGORIES,
  SUPPLIER_GOOD_TYPES,
  SUPPLIER_RATINGS,
  SUPPLIER_RISKS,
  SUPPLIER_SEGMENTS,
  SUPPLIER_TYPES,
} from "../types/businessPartner.types";
import { paymentTermService } from "../api/paymentTermService";
import type { PaymentTermDto } from "../api/paymentTermService";
import { useSriSupplierTypes } from "../api/useSriSupplierTypes";
import "../../../styles/shared/items-catalog.css";
import "./masterdata-pages.css";

const TABS = [
  {
    id: "resumen" as const,
    labelKey: "masterdata.suppliers.tabs.resumen",
    labelFb: "Resumen",
    icon: "bar_chart_4_bars",
  },
  {
    id: "listado" as const,
    labelKey: "masterdata.suppliers.tabs.listado",
    labelFb: "Listado",
    icon: "view_list",
  },
  {
    id: "nuevo" as const,
    labelKey: "masterdata.suppliers.tabs.nuevo",
    labelFb: "Nuevo proveedor",
    icon: "add_box",
  },
] as const;

const DRAFT_KEY = "erp.masterdata.suppliers.draft";

// ── SupplierConfigModal — Config SRI operativa (S3-A: incluye método de pago + exención) ──
function SupplierConfigModal({
  bpName,
  saving,
  error,
  onClose,
  onSave,
}: {
  bpName: string;
  saving: boolean;
  error?: string | null;
  onClose: () => void;
  onSave: (body: SupplierConfigBody) => void;
}) {
  const [taxSupportCode, setTaxSupportCode] = useState("");
  const [retentionVatCode, setRetentionVatCode] = useState("");
  const [retentionIncomeCode, setRetentionIncomeCode] = useState("");
  const [paymentTermId, setPaymentTermId] = useState("");
  const [paymentMethodCode, setPaymentMethodCode] = useState("");
  const [refundProviderType, setRefundProviderType] = useState("");
  const [isRetentionExempt, setIsRetentionExempt] = useState(false);
  const [paymentTermsList, setPaymentTermsList] = useState<PaymentTermDto[]>(
    [],
  );
  const { options: supplierTypeOptions } = useSriSupplierTypes();

  useEffect(() => {
    paymentTermService
      .list()
      .then(setPaymentTermsList)
      .catch(() => {});
  }, []);

  const selectedPt = paymentTermsList.find((pt) => pt.id === paymentTermId);

  return (
    <ZHModal
      open
      onClose={onClose}
      title="Config SRI — Proveedor"
      subtitle={bpName}
      closeOnBackdrop={false}
    >
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!paymentTermId) return;
          onSave({
            paymentTermId,
            defaultTaxSupportCode: taxSupportCode || null,
            defaultRetentionVatCode: retentionVatCode || null,
            defaultRetentionIncomeCode: retentionIncomeCode || null,
            defaultPaymentMethodCode: paymentMethodCode || null,
            refundProviderTypeCode: refundProviderType || null,
            isRetentionExempt,
          });
        }}
      >
        {error && <ZHPageNotice variant="error" message={error} />}
        <ZHGrid cols={2}>
          <ZHField label="Sustento tributario">
            <input
              className="zh-input mono"
              value={taxSupportCode}
              onChange={(e) => setTaxSupportCode(e.target.value)}
              disabled={saving}
              placeholder="01"
              maxLength={2}
            />
          </ZHField>
          <ZHField label="Código ret. IVA">
            <input
              className="zh-input mono"
              value={retentionVatCode}
              onChange={(e) => setRetentionVatCode(e.target.value)}
              disabled={saving}
              placeholder="725"
              maxLength={5}
            />
          </ZHField>
          <ZHField label="Código ret. Renta">
            <input
              className="zh-input mono"
              value={retentionIncomeCode}
              onChange={(e) => setRetentionIncomeCode(e.target.value)}
              disabled={saving}
              placeholder="303"
              maxLength={5}
            />
          </ZHField>
          <ZHField label="Condición de pago *" required>
            <select
              value={paymentTermId}
              onChange={(e) => setPaymentTermId(e.target.value)}
              disabled={saving}
              required
            >
              <option value="">— Seleccionar condición de pago —</option>
              {paymentTermsList
                .filter((pt) => pt.isActive)
                .map((pt) => (
                  <option key={pt.id} value={pt.id}>
                    {pt.code} — {pt.name} ({pt.summary})
                  </option>
                ))}
            </select>
            {selectedPt && (
              <div className="md-supplier-payment-term-summary">
                Cuotas: <strong>{selectedPt.installments}</strong> · Dias entre
                cuotas: <strong>{selectedPt.daysBetweenInstallments}</strong> ·
                Total: <strong>{selectedPt.totalDays} dias</strong>
              </div>
            )}
          </ZHField>
          <ZHField label="Método de pago SRI">
            <select
              value={paymentMethodCode}
              onChange={(e) => setPaymentMethodCode(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin preferencia —</option>
              {SRI_PAYMENT_METHOD_CODES.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </ZHField>
          <ZHField label="Tipo de Proveedor">
            <select
              value={refundProviderType}
              onChange={(e) => setRefundProviderType(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin clasificar —</option>
              {supplierTypeOptions.map((o) => (
                <option key={o.code} value={o.code}>
                  {o.name}
                </option>
              ))}
            </select>
          </ZHField>
          <div className="zh-col-span-2">
            <ZHToggle
              label="Exento de retención"
              description="RISE / Microempresa / Sector publico"
              value={isRetentionExempt}
              onChange={setIsRetentionExempt}
              disabled={saving}
            />
          </div>
        </ZHGrid>
        <ZHFormActions
          onCancel={onClose}
          hideDraft
          saveButtonType="submit"
          disableSave={saving}
          labels={{
            cancel: "Cerrar",
            save: saving ? "Guardando..." : "Guardar",
          }}
        />
      </form>
    </ZHModal>
  );
}

// ── SupplierClassificationModal — Clasificación estratégica (S3-B) ───────────
function SupplierClassificationModal({
  bpName,
  saving,
  error,
  onClose,
  onSave,
}: {
  bpName: string;
  saving: boolean;
  error?: string | null;
  onClose: () => void;
  onSave: (body: SupplierClassificationBody) => void;
}) {
  const [category, setCategory] = useState("");
  const [type, setType] = useState("");
  const [risk, setRisk] = useState("");
  const [rating, setRating] = useState("");
  const [goodType, setGoodType] = useState("");
  const [segment, setSegment] = useState("");
  const [paymentPref, setPaymentPref] = useState("");

  return (
    <ZHModal
      open
      onClose={onClose}
      title="Clasificación — Proveedor"
      subtitle={bpName}
      closeOnBackdrop={false}
    >
      <form
        onSubmit={(e) => {
          e.preventDefault();
          onSave({
            supplierCategory: category || null,
            supplierType: type || null,
            supplierRisk: risk || null,
            supplierRating: rating || null,
            primaryGoodType: goodType || null,
            supplierSegment: segment || null,
            paymentMethodPreference: paymentPref || null,
          });
        }}
      >
        {error && <ZHPageNotice variant="error" message={error} />}
        <ZHGrid cols={2}>
          <ZHField label="Categoría">
            <select
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin asignar —</option>
              {SUPPLIER_CATEGORIES.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </select>
          </ZHField>
          <ZHField label="Tipo">
            <select
              value={type}
              onChange={(e) => setType(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin asignar —</option>
              {SUPPLIER_TYPES.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </select>
          </ZHField>
          <ZHField label="Riesgo">
            <select
              value={risk}
              onChange={(e) => setRisk(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin asignar —</option>
              {SUPPLIER_RISKS.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </select>
          </ZHField>
          <ZHField label="Rating">
            <select
              value={rating}
              onChange={(e) => setRating(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin calificar —</option>
              {SUPPLIER_RATINGS.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </select>
          </ZHField>
          <ZHField label="Tipo de bien">
            <select
              value={goodType}
              onChange={(e) => setGoodType(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin asignar —</option>
              {SUPPLIER_GOOD_TYPES.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </select>
          </ZHField>
          <ZHField label="Segmento estratégico">
            <select
              value={segment}
              onChange={(e) => setSegment(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin asignar —</option>
              {SUPPLIER_SEGMENTS.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </select>
          </ZHField>
          <ZHField label="Método de pago interno">
            <input
              className="zh-input"
              value={paymentPref}
              onChange={(e) => setPaymentPref(e.target.value)}
              disabled={saving}
              placeholder="Ej: Transferencia bancaria"
              maxLength={100}
            />
          </ZHField>
        </ZHGrid>
        <ZHFormActions
          onCancel={onClose}
          hideDraft
          saveButtonType="submit"
          disableSave={saving}
          labels={{
            cancel: "Cerrar",
            save: saving ? "Guardando..." : "Guardar",
          }}
        />
      </form>
    </ZHModal>
  );
}

export function MasterDataSuppliersPage() {
  const { t } = useI18n();
  const page = useMasterDataSuppliersPage();
  const ui = useMasterDataSuppliersUiStore;

  const activeTab = ui((s) => s.activeTab);
  const editingPartner = ui((s) => s.editingPartner);
  const setActiveTab = ui((s) => s.setActiveTab);
  const cancelEdit = ui((s) => s.cancelEdit);
  const addActivity = ui((s) => s.addActivity);
  const reset = ui((s) => s.reset);
  const searchRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    reset();
    return () => reset();
  }, [reset]);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        setActiveTab("listado");
        setTimeout(() => searchRef.current?.focus(), 80);
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [setActiveTab]);

  const handleCreate = useCallback(
    async (
      body: CreateBusinessPartnerBody,
      supplierConfig?: {
        refundProviderTypeCode: string;
        paymentTermId: string;
      },
    ): Promise<void> => {
      await page.createSupplier(body, supplierConfig);
      addActivity(body.legalName, "created");
      message.success("Proveedor creado correctamente.");
      setActiveTab("listado");
    },
    [page, addActivity, setActiveTab],
  );

  const handleUpdate = useCallback(
    async (body: UpdateBusinessPartnerBody): Promise<void> => {
      if (!editingPartner) return;
      await page.updateSupplier(editingPartner.id, body);
      addActivity(body.legalName, "updated");
      message.success("Proveedor actualizado correctamente.");
      cancelEdit();
    },
    [editingPartner, page, addActivity, cancelEdit],
  );

  const handleAssign = useCallback(
    async (id: string): Promise<void> => {
      await page.assignAsSupplier(id);
      const bp = page.suppliers.find((s) => s.id === id);
      addActivity(bp?.legalName ?? id, "assigned");
      message.success("Rol de proveedor asignado correctamente.");
      setActiveTab("listado");
    },
    [page, addActivity, setActiveTab],
  );

  const handleDisable = useCallback(
    async (id: string) => {
      await page.disableSupplier(id);
      const bp = page.suppliers.find((s) => s.id === id);
      if (bp) {
        addActivity(bp.legalName, "disabled");
        message.info("Proveedor desactivado.");
      }
    },
    [page, addActivity],
  );

  const handleActivate = useCallback(
    async (id: string) => {
      await page.activateSupplier(id);
      const bp = page.suppliers.find((s) => s.id === id);
      if (bp) {
        addActivity(bp.legalName, "enabled");
        message.info("Proveedor activado.");
      }
    },
    [page, addActivity],
  );

  if (!page.canView)
    return <NoAccessPage title={t("masterdata.suppliers.title")} />;

  return (
    <ErpPageTemplate
      kicker="MasterData"
      title={t("masterdata.suppliers.title")}
      subtitle={t("masterdata.suppliers.subtitle")}
    >
      {page.listError && (
        <ZHPageNotice variant="error" message={page.listError} />
      )}
      {page.inlineError && (
        <ZHPageNotice variant="error" message={page.inlineError} />
      )}

      <div className="prd-tabs" role="tablist">
        {TABS.map((tab) => {
          const active = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              aria-selected={active}
              className={`prd-tab-btn ${active ? "prd-tab-btn--active" : ""}`}
              onClick={() => setActiveTab(tab.id)}
            >
              <span className="material-symbols-outlined prd-tab-icon">
                {tab.icon}
              </span>
              {t(tab.labelKey, tab.labelFb)}
            </button>
          );
        })}
      </div>

      <div className="prd-tab-content">
        {activeTab === "resumen" && (
          <MasterDataPartnerResumenTab
            role="supplier"
            partners={page.suppliers}
            totalCount={page.totalCount}
            store={ui}
          />
        )}
        {activeTab === "listado" && (
          <MasterDataPartnerListTab
            role="supplier"
            store={ui}
            canCreate={page.canCreate}
            canUpdate={page.canUpdate}
            canDisable={page.canDisable}
            canConfigure={page.canConfigure}
            loading={page.loading}
            saving={page.saving}
            partners={page.suppliers}
            totalCount={page.totalCount}
            search={page.search}
            setSearch={page.setSearch}
            showInactive={page.showInactive}
            setShowInactive={page.setShowInactive}
            page={page.page}
            totalPages={page.totalPages}
            setPage={page.setPage}
            searchInputRef={searchRef}
            onSettings={(bp) => void page.openSettings(bp)}
            onSupplierProfile={(bp) => void page.openSupplierConfig(bp)}
            onAddAsCustomer={(id) => void page.addAsCustomer(id)}
            onActivate={handleActivate}
            onDisable={handleDisable}
          />
        )}
        {activeTab === "nuevo" && (page.canCreate || editingPartner) && (
          <MasterDataPartnerWizard
            key={editingPartner?.id ?? "create"}
            role="supplier"
            draftKey={DRAFT_KEY}
            submitting={page.saving}
            editingPartner={editingPartner}
            onSubmitCreate={handleCreate}
            onSubmitUpdate={handleUpdate}
            onAssignRole={handleAssign}
            onCancel={cancelEdit}
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
          onSave={(payload) => page.saveSettings(page.settingsBp!.id, payload)}
          onBlock={(reason) =>
            void page.blockSupplier(page.settingsBp!.id, reason)
          }
          onUnblock={() => void page.unblockSupplier(page.settingsBp!.id)}
        />
      )}

      {page.supplierConfigBp && (
        <SupplierConfigModal
          bpName={page.supplierConfigBp.bp.legalName}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeSupplierConfig}
          onSave={(body) =>
            void page.saveSupplierConfig(
              page.supplierConfigBp!.bp.id,
              page.supplierConfigBp!.roleId,
              body,
            )
          }
        />
      )}

      {page.supplierClassificationBp && (
        <SupplierClassificationModal
          bpName={page.supplierClassificationBp.bp.legalName}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeSupplierClassification}
          onSave={(body) =>
            void page.saveSupplierClassification(
              page.supplierClassificationBp!.bp.id,
              page.supplierClassificationBp!.roleId,
              body,
            )
          }
        />
      )}
    </ErpPageTemplate>
  );
}
