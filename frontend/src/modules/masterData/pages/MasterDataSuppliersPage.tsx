import { useCallback, useEffect, useRef, useState } from "react";
import { NoAccessPage } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import {
  ZHBtn,
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
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type {
  CreateBusinessPartnerBody,
  SupplierConfigBody,
  UpdateBusinessPartnerBody,
} from "../types/businessPartner.types";
import { paymentTermService } from "../api/paymentTermService";
import type { PaymentTermDto } from "../api/paymentTermService";
import { useSriSupplierTypes } from "../api/useSriSupplierTypes";
import { useSriPaymentMethods } from "../api/useSriPaymentMethods";
import { formatApiRequestError } from "../../lib/apiError";
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
  bpId,
  bpName,
  saving,
  error,
  onClose,
  onSave,
}: {
  bpId: string;
  bpName: string;
  saving: boolean;
  error?: string | null;
  onClose: () => void;
  onSave: (body: SupplierConfigBody) => void;
}) {
  const [taxSupportCode, setTaxSupportCode] = useState("");
  const [retentionVatCode, setRetentionVatCode] = useState("");
  const [retentionIncomeCode, setRetentionIncomeCode] = useState("");
  const [paymentMethodCode, setPaymentMethodCode] = useState("");
  const [refundProviderType, setRefundProviderType] = useState("");
  const [isRetentionExempt, setIsRetentionExempt] = useState(false);
  const [paymentTermsList, setPaymentTermsList] = useState<PaymentTermDto[]>(
    [],
  );
  const { options: supplierTypeOptions } = useSriSupplierTypes();
  const {
    options: paymentMethodOptions,
    loading: loadingPaymentMethods,
    error: paymentMethodsError,
  } = useSriPaymentMethods();

  useEffect(() => {
    paymentTermService
      .list()
      .then(setPaymentTermsList)
      .catch(() => {});
  }, []);

  // ── Default de compras/gastos por empresa activa (ADR-033) ───────────────
  // Sección independiente: fetch/save propios, separados del formulario de
  // Config SRI de arriba — un fallo en uno no afecta al otro, y cada uno
  // muestra su propio error.
  const [purchaseDefaultPtId, setPurchaseDefaultPtId] = useState("");
  const [purchaseSettingsLoading, setPurchaseSettingsLoading] =
    useState(true);
  const [purchaseSettingsSaving, setPurchaseSettingsSaving] = useState(false);
  const [purchaseSettingsError, setPurchaseSettingsError] = useState<
    string | null
  >(null);
  const [purchaseSettingsSaved, setPurchaseSettingsSaved] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setPurchaseSettingsLoading(true);
    businessPartnerFacade
      .getPurchaseSettings(bpId)
      .then((dto) => {
        if (!cancelled) setPurchaseDefaultPtId(dto.paymentTermId ?? "");
      })
      .catch((err: unknown) => {
        if (!cancelled)
          setPurchaseSettingsError(
            formatApiRequestError(err, {
              generic: "No se pudo cargar la condición predeterminada.",
            }),
          );
      })
      .finally(() => {
        if (!cancelled) setPurchaseSettingsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [bpId]);

  const handleSavePurchaseDefault = async () => {
    setPurchaseSettingsSaving(true);
    setPurchaseSettingsError(null);
    setPurchaseSettingsSaved(false);
    try {
      await businessPartnerFacade.upsertPurchaseSettings(bpId, {
        paymentTermId: purchaseDefaultPtId || null,
      });
      setPurchaseSettingsSaved(true);
    } catch (err: unknown) {
      setPurchaseSettingsError(
        formatApiRequestError(err, {
          generic: "No se pudo guardar la condición predeterminada.",
        }),
      );
    } finally {
      setPurchaseSettingsSaving(false);
    }
  };

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
          onSave({
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
          <ZHField
            label="Método de pago SRI"
            fieldError={paymentMethodsError ?? undefined}
          >
            <select
              value={paymentMethodCode}
              onChange={(e) => setPaymentMethodCode(e.target.value)}
              disabled={saving || loadingPaymentMethods}
            >
              <option value="">— Sin preferencia —</option>
              {loadingPaymentMethods ? (
                <option value="">Cargando…</option>
              ) : (
                paymentMethodOptions.map((o) => (
                  <option key={o.code} value={o.code}>
                    {o.code} — {o.name}
                  </option>
                ))
              )}
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

      <hr className="md-supplier-section-divider" />

      <div className="md-supplier-purchase-default-section">
        <h4>Condición predeterminada para compras/gastos</h4>
        {purchaseSettingsError && (
          <ZHPageNotice variant="error" message={purchaseSettingsError} />
        )}
        <ZHField
          label="Condición predeterminada para compras/gastos"
          hint="Aplica solo a la empresa activa. Si este proveedor trabaja con otra empresa, puede tener una condición distinta."
        >
          <select
            value={purchaseDefaultPtId}
            onChange={(e) => {
              setPurchaseDefaultPtId(e.target.value);
              setPurchaseSettingsSaved(false);
            }}
            disabled={purchaseSettingsLoading || purchaseSettingsSaving}
          >
            <option value="">
              — Sin default (exigir selección al crear documentos) —
            </option>
            {paymentTermsList
              .filter(
                (pt) => pt.isActive || pt.id === purchaseDefaultPtId,
              )
              .map((pt) => (
                <option key={pt.id} value={pt.id}>
                  {pt.code} — {pt.name} ({pt.summary})
                </option>
              ))}
          </select>
        </ZHField>
        <ZHBtn
          type="button"
          variant="secondary"
          size="sm"
          disabled={purchaseSettingsLoading || purchaseSettingsSaving}
          onClick={() => void handleSavePurchaseDefault()}
        >
          {purchaseSettingsSaving
            ? "Guardando..."
            : purchaseSettingsSaved
              ? "Guardado ✓"
              : "Guardar condición de compras/gastos"}
        </ZHBtn>
      </div>
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

  // CRITICAL-CONFIRMATIONS-BUSINESS-PARTNERS-04: antes se mostraba éxito aunque
  // page.disableSupplier/activateSupplier fallaran internamente (el hook no relanzaba el error).
  // Ahora sí relanza, así que message.success solo corre si el await no lanzó — y se agrega
  // confirmación previa explicando el impacto real.
  const handleDisable = useCallback(
    async (id: string) => {
      const bp = page.suppliers.find((s) => s.id === id);
      const confirmed = await message.confirm({
        title: "Desactivar proveedor",
        message: bp
          ? `"${bp.legalName}" dejará de estar disponible para operaciones futuras (nuevas compras, nuevos documentos). El histórico existente no se elimina.`
          : "El proveedor dejará de estar disponible para operaciones futuras. El histórico existente no se elimina.",
        variant: "danger",
        confirmLabel: "Desactivar",
        cancelLabel: "Cancelar",
      });
      if (!confirmed) return;

      try {
        await page.disableSupplier(id);
        if (bp) addActivity(bp.legalName, "disabled");
        message.success("Proveedor desactivado correctamente.");
      } catch (err) {
        message.error(
          formatApiRequestError(err, {
            generic: "No se pudo desactivar el proveedor.",
          }),
        );
      }
    },
    [page, addActivity],
  );

  const handleActivate = useCallback(
    async (id: string) => {
      const bp = page.suppliers.find((s) => s.id === id);
      const confirmed = await message.confirm({
        title: "Activar proveedor",
        message: bp
          ? `"${bp.legalName}" volverá a estar disponible para operaciones futuras (nuevas compras, nuevos documentos).`
          : "El proveedor volverá a estar disponible para operaciones futuras.",
        variant: "warning",
        confirmLabel: "Activar",
        cancelLabel: "Cancelar",
      });
      if (!confirmed) return;

      try {
        await page.activateSupplier(id);
        if (bp) addActivity(bp.legalName, "enabled");
        message.success("Proveedor activado correctamente.");
      } catch (err) {
        message.error(
          formatApiRequestError(err, {
            generic: "No se pudo activar el proveedor.",
          }),
        );
      }
    },
    [page, addActivity],
  );

  if (!page.canView)
    return <NoAccessPage title={t("masterdata.suppliers.title")} />;

  return (
    <ErpPageTemplate
      kicker={t("masterdata.suppliers.kicker", "Personas y empresas")}
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
            statusFilter={page.statusFilter}
            setStatusFilter={page.setStatusFilter}
            page={page.page}
            pageSize={page.pageSize}
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
          bpId={page.supplierConfigBp.bp.id}
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
    </ErpPageTemplate>
  );
}
