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
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
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
  SupplierRetentionDefaultDto,
  SupplierRoleConfigDto,
  UpdateBusinessPartnerBody,
} from "../types/businessPartner.types";
import { paymentTermService } from "../api/paymentTermService";
import type { PaymentTermDto } from "../api/paymentTermService";
import { useSriSupplierTypes } from "../api/useSriSupplierTypes";
import { useSriPaymentMethods } from "../api/useSriPaymentMethods";
import { useSriTaxSupportCodes } from "../api/useSriTaxSupportCodes";
import { useSriRetentionCodes } from "../api/useSriRetentionCodes";
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

/**
 * Un valor guardado que ya no exista o esté inactivo en el catálogo no se descarta ni se
 * reemplaza silenciosamente (perdería el dato real sin que el usuario lo note) — se detecta
 * aquí para mostrar una opción sintética deshabilitada + fieldError, exigiendo reselección.
 */
function isOrphanCatalogValue(
  value: string,
  options: readonly { code: string }[],
  loading: boolean,
): boolean {
  return value !== "" && !loading && !options.some((o) => o.code === value);
}

const ORPHAN_CODE_MESSAGE =
  "Este código ya no está activo en el catálogo. Selecciona una opción válida.";

// ── SupplierConfigModal — Config SRI operativa (S3-A: incluye método de pago + exención) ──
function SupplierConfigModal({
  bpId,
  bpName,
  initialConfig,
  saving,
  error,
  onClose,
  onSave,
}: {
  bpId: string;
  bpName: string;
  initialConfig: SupplierRoleConfigDto | null;
  saving: boolean;
  error?: string | null;
  onClose: () => void;
  onSave: (body: SupplierConfigBody) => void;
}) {
  const [taxSupportCode, setTaxSupportCode] = useState(
    initialConfig?.defaultTaxSupportCode ?? "",
  );
  const [paymentMethodCode, setPaymentMethodCode] = useState(
    initialConfig?.defaultPaymentMethodCode ?? "",
  );
  const [refundProviderType, setRefundProviderType] = useState(
    initialConfig?.refundProviderTypeCode ?? "",
  );
  const [isRetentionExempt, setIsRetentionExempt] = useState(
    initialConfig?.isRetentionExempt ?? false,
  );
  const [isRequiredToKeepAccounting, setIsRequiredToKeepAccounting] =
    useState(initialConfig?.isRequiredToKeepAccounting ?? false);
  const [paymentTermsList, setPaymentTermsList] = useState<PaymentTermDto[]>(
    [],
  );
  const { options: supplierTypeOptions } = useSriSupplierTypes();
  const {
    options: paymentMethodOptions,
    loading: loadingPaymentMethods,
    error: paymentMethodsError,
  } = useSriPaymentMethods();
  const {
    options: taxSupportOptions,
    loading: loadingTaxSupport,
    error: taxSupportFetchError,
  } = useSriTaxSupportCodes();

  const taxSupportOrphan = isOrphanCatalogValue(
    taxSupportCode,
    taxSupportOptions,
    loadingTaxSupport,
  );

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
            defaultPaymentMethodCode: paymentMethodCode || null,
            refundProviderTypeCode: refundProviderType || null,
            isRetentionExempt,
            isRequiredToKeepAccounting,
          });
        }}
      >
        {error && <ZHPageNotice variant="error" message={error} />}
        <ZHGrid cols={2}>
          <ZHField
            label="Sustento tributario predeterminado"
            fieldError={
              taxSupportFetchError ??
              (taxSupportOrphan ? ORPHAN_CODE_MESSAGE : undefined)
            }
          >
            <select
              value={taxSupportCode}
              onChange={(e) => setTaxSupportCode(e.target.value)}
              disabled={saving || loadingTaxSupport}
            >
              <option value="">— Sin definir —</option>
              {loadingTaxSupport ? (
                <option value="">Cargando…</option>
              ) : (
                <>
                  {taxSupportOrphan && (
                    <option value={taxSupportCode} disabled>
                      {taxSupportCode} — (código no vigente)
                    </option>
                  )}
                  {taxSupportOptions.map((o) => (
                    <option key={o.code} value={o.code}>
                      {o.code} — {o.name}
                    </option>
                  ))}
                </>
              )}
            </select>
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
          <div className="zh-col-span-2">
            <ZHToggle
              label="Obligado a llevar contabilidad"
              description="Dato tributario del proveedor. Por ahora se usa como información para evaluación de retenciones."
              value={isRequiredToKeepAccounting}
              onChange={setIsRequiredToKeepAccounting}
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

      <hr className="md-supplier-section-divider" />

      <SupplierRetentionDefaultsSection bpId={bpId} disabled={saving} />
    </ZHModal>
  );
}

// ── SupplierRetentionDefaultsSection — RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01 ──
// Lista dinámica de retenciones predeterminadas por proveedor+empresa activa. Reemplaza los
// antiguos selects fijos "Código ret. IVA/Renta predeterminado" (máx. 1 código por impuesto,
// tenant-wide). Mismo patrón de sección independiente que "Condición predeterminada para
// compras/gastos" arriba: fetch/save propios, desacoplada del form de Config SRI.
function SupplierRetentionDefaultsSection({
  bpId,
  disabled,
}: {
  bpId: string;
  disabled?: boolean;
}) {
  const [rows, setRows] = useState<SupplierRetentionDefaultDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [newTaxType, setNewTaxType] = useState<"IVA" | "RENTA">("IVA");
  const [newCodeId, setNewCodeId] = useState("");
  const [adding, setAdding] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);

  const { options: vatOptions, loading: loadingVat } =
    useSriRetentionCodes("IVA");
  const { options: incomeOptions, loading: loadingIncome } =
    useSriRetentionCodes("RENTA");
  const catalogOptions = newTaxType === "IVA" ? vatOptions : incomeOptions;
  const loadingCatalog = newTaxType === "IVA" ? loadingVat : loadingIncome;

  const load = useCallback(() => {
    setLoading(true);
    businessPartnerFacade
      .getRetentionDefaults(bpId)
      .then(setRows)
      .catch((err: unknown) =>
        setError(
          formatApiRequestError(err, {
            generic: "No se pudieron cargar las retenciones predeterminadas.",
          }),
        ),
      )
      .finally(() => setLoading(false));
  }, [bpId]);

  useEffect(() => {
    load();
  }, [load]);

  const availableCodes = catalogOptions.filter(
    (o) => !rows.some((r) => r.sriRetentionCodeId === o.id),
  );

  const sortedRows = [...rows].sort((a, b) => a.displayOrder - b.displayOrder);

  const handleAdd = async () => {
    if (!newCodeId) return;
    setAdding(true);
    setError(null);
    try {
      const created = await businessPartnerFacade.addRetentionDefault(bpId, {
        sriRetentionCodeId: newCodeId,
      });
      setRows((prev) => [...prev, created]);
      setNewCodeId("");
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic: "No se pudo agregar la retención predeterminada.",
        }),
      );
    } finally {
      setAdding(false);
    }
  };

  const handleToggleActive = async (
    row: SupplierRetentionDefaultDto,
    next: boolean,
  ) => {
    setBusyId(row.id);
    setError(null);
    try {
      const updated = await businessPartnerFacade.setRetentionDefaultState(
        bpId,
        row.id,
        { isActive: next, displayOrder: row.displayOrder },
      );
      setRows((prev) => prev.map((r) => (r.id === row.id ? updated : r)));
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic: "No se pudo actualizar la retención predeterminada.",
        }),
      );
    } finally {
      setBusyId(null);
    }
  };

  const handleMove = async (row: SupplierRetentionDefaultDto, direction: -1 | 1) => {
    const index = sortedRows.findIndex((r) => r.id === row.id);
    const other = sortedRows[index + direction];
    if (!other) return;
    setBusyId(row.id);
    setError(null);
    try {
      const [updatedRow, updatedOther] = await Promise.all([
        businessPartnerFacade.setRetentionDefaultState(bpId, row.id, {
          isActive: row.isActive,
          displayOrder: other.displayOrder,
        }),
        businessPartnerFacade.setRetentionDefaultState(bpId, other.id, {
          isActive: other.isActive,
          displayOrder: row.displayOrder,
        }),
      ]);
      setRows((prev) =>
        prev.map((r) =>
          r.id === updatedRow.id
            ? updatedRow
            : r.id === updatedOther.id
              ? updatedOther
              : r,
        ),
      );
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic: "No se pudo reordenar la retención predeterminada.",
        }),
      );
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="md-supplier-purchase-default-section">
      <h4>Retenciones predeterminadas</h4>
      <p className="md-supplier-section-hint">
        Se proponen automáticamente al calcular retenciones en Compras/Gastos
        para este proveedor en la empresa activa. El usuario puede
        modificarlas antes de confirmar. La retención final emitida se guarda
        siempre en el documento de retención, nunca aquí.
      </p>
      {error && <ZHPageNotice variant="error" message={error} />}

      {loading ? (
        <p className="md-supplier-section-hint">Cargando…</p>
      ) : sortedRows.length === 0 ? (
        <p className="md-supplier-section-hint">
          Sin retenciones predeterminadas configuradas para esta empresa.
        </p>
      ) : (
        <ul className="md-supplier-retention-list">
          {sortedRows.map((row, index) => (
            <li
              key={row.id}
              className={`md-supplier-retention-row${row.isActive ? "" : " md-supplier-retention-row--inactive"}`}
            >
              <span className="md-supplier-retention-row__code">
                {row.taxType} — {row.code} — {row.codeName} (
                {row.percentage}%)
              </span>
              {!row.isCatalogCodeActive && (
                <span className="md-supplier-retention-row__warning">
                  Código inactivo en catálogo
                </span>
              )}
              <div className="md-supplier-retention-row__actions">
                <ZHIconButton
                  icon="arrow_upward"
                  title="Subir"
                  ariaLabel={`Subir prioridad de ${row.code}`}
                  variant="ghost"
                  disabled={disabled || busyId === row.id || index === 0}
                  onClick={() => void handleMove(row, -1)}
                />
                <ZHIconButton
                  icon="arrow_downward"
                  title="Bajar"
                  ariaLabel={`Bajar prioridad de ${row.code}`}
                  variant="ghost"
                  disabled={
                    disabled ||
                    busyId === row.id ||
                    index === sortedRows.length - 1
                  }
                  onClick={() => void handleMove(row, 1)}
                />
                <ZHToggle
                  label={row.isActive ? "Activa" : "Inactiva"}
                  description="Se propone automáticamente al calcular retenciones si está activa"
                  value={row.isActive}
                  onChange={(next) => void handleToggleActive(row, next)}
                  disabled={disabled || busyId === row.id}
                />
              </div>
            </li>
          ))}
        </ul>
      )}

      <ZHGrid cols={3}>
        <ZHField label="Tipo de impuesto">
          <select
            value={newTaxType}
            onChange={(e) => {
              setNewTaxType(e.target.value as "IVA" | "RENTA");
              setNewCodeId("");
            }}
            disabled={disabled || adding}
          >
            <option value="IVA">IVA</option>
            <option value="RENTA">Renta</option>
          </select>
        </ZHField>
        <ZHField label="Código de retención SRI">
          <select
            value={newCodeId}
            onChange={(e) => setNewCodeId(e.target.value)}
            disabled={disabled || adding || loadingCatalog}
          >
            <option value="">— Seleccione —</option>
            {loadingCatalog ? (
              <option value="">Cargando…</option>
            ) : (
              availableCodes.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.code} — {o.name} ({o.percentage}%)
                </option>
              ))
            )}
          </select>
        </ZHField>
        <div className="md-supplier-retention-add-btn">
          <ZHBtn
            type="button"
            variant="secondary"
            size="sm"
            disabled={disabled || adding || !newCodeId}
            onClick={() => void handleAdd()}
          >
            {adding ? "Agregando..." : "Agregar retención"}
          </ZHBtn>
        </div>
      </ZHGrid>
    </div>
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
          initialConfig={page.supplierConfigBp.config}
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
