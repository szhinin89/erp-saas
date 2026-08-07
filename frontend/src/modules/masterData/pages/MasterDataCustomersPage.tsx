import { useCallback, useEffect, useRef, useState } from "react";
import { NoAccessPage } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHField, ZHGrid, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import { useI18n } from "../../../i18n/i18n";
import { useMasterDataCustomersPage } from "./useMasterDataCustomersPage";
import { MasterDataCompanySettingsModal } from "./MasterDataCompanySettingsModal";
import { MasterDataPartnerWizard } from "../components/MasterDataPartnerWizard";
import { MasterDataPartnerResumenTab } from "../components/MasterDataPartnerResumenTab";
import { MasterDataPartnerListTab } from "../components/MasterDataPartnerListTab";
import { useMasterDataCustomersUiStore } from "../store/masterDataPartnerUiStore";
import { message } from "../../../lib/messages";
import type {
  CreateBusinessPartnerBody,
  CustomerConfigBody,
  UpdateBusinessPartnerBody,
} from "../types/businessPartner.types";
import {
  CUSTOMER_CATEGORIES,
  CUSTOMER_CLASSIFICATIONS,
  CUSTOMER_SEGMENTS,
  CREDIT_RATINGS,
  INVOICE_FORMATS,
  LOYALTY_TIERS,
} from "../types/businessPartner.types";
import "../../../styles/shared/items-catalog.css";
import "./masterdata-pages.css";

// ── CustomerConfigModal — CRM-ready classification modal ─────────────────────
function CustomerConfigModal({
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
  onSave: (body: CustomerConfigBody) => void;
}) {
  const [category, setCategory] = useState("");
  const [segment, setSegment] = useState("");
  const [zone, setZone] = useState("");
  const [rating, setRating] = useState("");
  const [loyalty, setLoyalty] = useState("");
  const [invoiceFormat, setInvoiceFormat] = useState("");
  const [classification, setClassification] = useState("");

  const handleSave = (e: React.FormEvent) => {
    e.preventDefault();
    onSave({
      customerCategory: category || null,
      customerSegment: segment || null,
      salesZone: zone.trim() || null,
      creditRating: rating || null,
      loyaltyTier: loyalty || null,
      preferredInvoiceFormat: invoiceFormat || null,
      customerClassification: classification || null,
    });
  };

  return (
    <ZHModal
      open
      onClose={onClose}
      title="Perfil Cliente — CRM"
      subtitle={bpName}
    >
      <form onSubmit={handleSave}>
        {error && <ZHPageNotice variant="error" message={error} />}
        <ZHGrid cols={2}>
          <ZHField label="Categoría">
            <ZhSelect
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin asignar —</option>
              {CUSTOMER_CATEGORIES.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label="Segmento">
            <ZhSelect
              value={segment}
              onChange={(e) => setSegment(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin asignar —</option>
              {CUSTOMER_SEGMENTS.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label="Zona de ventas">
            <ZhTextInput
              className="zh-input"
              value={zone}
              onChange={(e) => setZone(e.target.value)}
              placeholder="Norte, Sur, Centro…"
              maxLength={100}
              disabled={saving}
            />
          </ZHField>
          <ZHField label="Rating crediticio">
            <ZhSelect
              value={rating}
              onChange={(e) => setRating(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin calificar —</option>
              {CREDIT_RATINGS.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label="Nivel de fidelización">
            <ZhSelect
              value={loyalty}
              onChange={(e) => setLoyalty(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin nivel —</option>
              {LOYALTY_TIERS.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label="Formato de factura">
            <ZhSelect
              value={invoiceFormat}
              onChange={(e) => setInvoiceFormat(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin preferencia —</option>
              {INVOICE_FORMATS.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label="Clasificación comercial">
            <ZhSelect
              value={classification}
              onChange={(e) => setClassification(e.target.value)}
              disabled={saving}
            >
              <option value="">— Sin clasificar —</option>
              {CUSTOMER_CLASSIFICATIONS.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </ZhSelect>
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

const TABS = [
  {
    id: "resumen" as const,
    labelKey: "masterdata.customers.tabs.resumen",
    labelFb: "Resumen",
    icon: "bar_chart_4_bars",
  },
  {
    id: "listado" as const,
    labelKey: "masterdata.customers.tabs.listado",
    labelFb: "Listado",
    icon: "view_list",
  },
  {
    id: "nuevo" as const,
    labelKey: "masterdata.customers.tabs.nuevo",
    labelFb: "Nuevo cliente",
    icon: "add_box",
  },
] as const;

const DRAFT_KEY = "erp.masterdata.customers.draft";

export function MasterDataCustomersPage() {
  const { t } = useI18n();
  const page = useMasterDataCustomersPage();
  const ui = useMasterDataCustomersUiStore;

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
    async (body: CreateBusinessPartnerBody): Promise<void> => {
      await page.createCustomer(body);
      addActivity(body.legalName, "created");
      message.success(
        t(
          "masterdata.customers.created.success",
          "Cliente creado correctamente.",
        ),
      );
      setActiveTab("listado");
    },
    [page, addActivity, setActiveTab, t],
  );

  const handleUpdate = useCallback(
    async (body: UpdateBusinessPartnerBody): Promise<void> => {
      if (!editingPartner) return;
      await page.updateCustomer(editingPartner.id, body);
      addActivity(body.legalName, "updated");
      message.success(
        t(
          "masterdata.customers.updated.success",
          "Cliente actualizado correctamente.",
        ),
      );
      cancelEdit();
    },
    [editingPartner, page, addActivity, cancelEdit, t],
  );

  const handleAssign = useCallback(
    async (id: string): Promise<void> => {
      await page.assignAsCustomer(id);
      const bp = page.customers.find((c) => c.id === id);
      addActivity(bp?.legalName ?? id, "assigned");
      message.success(
        t(
          "masterdata.customers.assigned.success",
          "Rol de cliente asignado correctamente.",
        ),
      );
      setActiveTab("listado");
    },
    [page, addActivity, setActiveTab, t],
  );

  const handleDisable = useCallback(
    async (id: string) => {
      await page.disableCustomer(id);
      const bp = page.customers.find((c) => c.id === id);
      if (bp) {
        addActivity(bp.legalName, "disabled");
        message.info("Cliente desactivado.");
      }
    },
    [page, addActivity],
  );

  const handleActivate = useCallback(
    async (id: string) => {
      await page.activateCustomer(id);
      const bp = page.customers.find((c) => c.id === id);
      if (bp) {
        addActivity(bp.legalName, "enabled");
        message.info("Cliente activado.");
      }
    },
    [page, addActivity],
  );

  if (!page.canView)
    return <NoAccessPage title={t("masterdata.customers.title")} />;

  return (
    <ErpPageTemplate
      kicker="MasterData"
      title={t("masterdata.customers.title")}
      subtitle={t("masterdata.customers.subtitle")}
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
            role="customer"
            partners={page.customers}
            totalCount={page.totalCount}
            store={ui}
          />
        )}
        {activeTab === "listado" && (
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
            onSettings={(bp) => void page.openSettings(bp)}
            onSupplierProfile={
              page.canUpdate
                ? (bp) => void page.openCustomerConfig(bp)
                : undefined
            }
            onAddAsSupplier={(id) => void page.addAsSupplier(id)}
            onActivate={handleActivate}
            onDisable={handleDisable}
          />
        )}
        {activeTab === "nuevo" && (page.canCreate || editingPartner) && (
          <MasterDataPartnerWizard
            key={editingPartner?.id ?? "create"}
            role="customer"
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
            void page.blockCustomer(page.settingsBp!.id, reason)
          }
          onUnblock={() => void page.unblockCustomer(page.settingsBp!.id)}
        />
      )}

      {page.customerConfigBp && (
        <CustomerConfigModal
          bpName={page.customerConfigBp.bp.legalName}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeCustomerConfig}
          onSave={(body) =>
            void page.saveCustomerConfig(
              page.customerConfigBp!.bp.id,
              page.customerConfigBp!.roleId,
              body,
            )
          }
        />
      )}
    </ErpPageTemplate>
  );
}
