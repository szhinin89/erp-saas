import { PurchaseFormFeedback } from "../components/PurchaseFormFeedback";
import { buildPurchaseLineIssues } from "../utils/purchaseLineIssues";
import { PurchaseActionReason } from "../components/PurchaseActionReason";
import { PurchaseSearchFeedback } from "../components/PurchaseSearchFeedback";
import { PurchaseFieldError } from "../components/PurchaseFieldError";
import { purchaseFieldErrors } from "../utils/purchaseFieldErrors";
import { PurchaseLineBreakdownModal } from "../components/PurchaseLineBreakdownModal";
import { canBreakDown } from "../utils/purchaseXmlBreakdown";
import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHLineAction } from "../../../components/zh/ZHLineAction";
import { ZHLineActionItem } from "../../../components/zh/ZHLineActionItem";
import { ZHRowDeleteAction } from "../../../components/zh/ZHRowDeleteAction";
import { ZHLineCard } from "../../../components/zh/ZHLineCard";
import { ZHTabBar } from "../../../components/zh/ZHTabBar";
import { ZHPickerResultItem } from "../../../components/zh/ZHPickerResultItem";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHInputGroup } from "../../../components/zh/ZHInputGroup";
import { SupplierSearchSelect } from "../../masterData/facades/supplierPickerFacade";
import { DistributeCostModal } from "../components/DistributeCostModal";
import { PurchaseSummaryPanel } from "../components/PurchaseSummaryPanel";
import { PurchaseRetentionDraft } from "../components/PurchaseRetentionDraft";
import {
  RetentionAnnulmentPanel,
  RetentionElectronicStatusBadge,
} from "../../retentions/facades/purchaseRetentionFacade";
import { ResolvePendingProductsModal } from "../components/ResolvePendingProductsModal";
import { PurchaseLineIssuesTag } from "../components/PurchaseLineIssuesTag";
import { CorrectSupplierMatchModal } from "../components/CorrectSupplierMatchModal";
import { ProductPicker } from "../components/ProductPicker";
import type { ProductProfile } from "../components/ProductPicker";
import { ItemEditorModal } from "../../../components/items/ItemEditorModal/ItemEditorModal";
import type { ItemCreatedResult } from "../../../components/items/ItemEditorModal/types";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhNumberInput } from "../../../components/zh/inputs/ZhNumberInput";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhDateTimeInput } from "../../../components/zh/inputs/ZhDateTimeInput";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { usePrecisionDecimals, usePrecisionPolicy } from "../../../hooks/usePrecisionPolicy";
import { resolvePrecisionDecimals } from "../../../lib/config/precisionPolicy.config";
import { formatMoney, formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import {
  ZHConfirmModal,
  ZHPromptModal,
} from "../../../components/zh/ZHConfirmModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import {
  roundToTotalAmount,
} from "../utils/purchaseCalc";
import { usePurchasesPage, type Tab } from "../hooks/usePurchasesPage";
import type { PurchaseListItemDto } from "../api/purchaseService";
import { useI18n } from "../../../i18n/i18n";
import { ItemMatchStatusBadge } from "../components/ItemMatchStatusBadge";
import { useOpenItemDetail } from "../../items/facades/itemDetailNavigationFacade";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import {
  buildPurchaseLinePresentation,
  applyRegisteredQuantity,
  applyRegisteredUnitCost,
  headerInputKey,
  isUnchangedAtPrecision,
  resetLineToSupplierOrigin,
  resolvePackagingChange,
  type LineMarginState,
} from "../utils/purchaseLinePresentation";
import {
  buildPurchaseItemProfile,
  resolvePurchaseItemSelection,
} from "../utils/purchaseItemProfile";
import type { PurchaseLineReadinessAction } from "../utils/purchaseLineReadiness";
import { canDiscardDraft, purchaseStatusBadge } from "../utils/purchaseDocumentState";
import { buildConfirmChecklist, buildConfirmCommercialRisk, buildConfirmLineWarnings } from "../utils/purchaseConfirmChecklist";
import { buildPurchaseCancelMessage } from "../utils/purchaseCancelMessage";
import { PurchaseConfirmChecklist } from "../components/PurchaseConfirmChecklist";
import "../../../styles/shared/items-catalog.css";
import "../../../styles/shared/erp-form-core.css";
import "../styles/purchases-invoice.css";

export function PurchasesPage() {
  const moneyDecimals = usePrecisionDecimals("money"); // textos compuestos (06)
  const ctx = usePurchasesPage();
  const navigate = useNavigate();
  const { t } = useI18n();
  const [searchParams] = useSearchParams();
  const openedFromParam = useRef(false);
  const xmlConfirmChecklist = buildConfirmChecklist(
    ctx.lines,
    ctx.editing?.lines ?? [],
    ctx.vatRatesMap,
    ctx.iceRatesMap,
    t,
    moneyDecimals,
  );
  const precisionPolicy = usePrecisionPolicy();
  // Solo se calcula con el modal abierto; mismas incidencias que las tarjetas de línea.
  const confirmLineWarnings = ctx.modalConfirm
    ? buildConfirmLineWarnings(ctx.lines, ctx.lineReadinessByKey, precisionPolicy, ctx.vatRatesMap, ctx.iceRatesMap, t)
    : [];
  const confirmCommercialRisk = ctx.modalConfirm
    ? buildConfirmCommercialRisk(ctx.lines, ctx.lineReadinessByKey, t)
    : null;

  // Entrada cruzada desde el Kardex ("Ver documento origen"): abre la factura referida.
  // Entrada cruzada desde Recepción Electrónica ("Crear compra"): precarga un borrador desde el XML.
  useEffect(() => {
    const invoiceId = searchParams.get("invoiceId");
    const fromReceptionId = searchParams.get("fromReceptionId");
    const accessKey = searchParams.get("accessKey");
    if (invoiceId && !openedFromParam.current) {
      openedFromParam.current = true;
      void ctx.loadForEdit(invoiceId);
    } else if (fromReceptionId && !openedFromParam.current) {
      openedFromParam.current = true;
      void ctx.loadFromReception(fromReceptionId, accessKey);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams]);

  const tabs: { id: Tab; label: string; icon: string }[] = [
    { id: "listado", label: t("purchases.tabs.list", "Listado"), icon: "view_list" },
    {
      id: "nuevo",
      label: ctx.editing
        ? ctx.readOnly
          ? t("purchases.tabs.view", "Ver compra")
          : t("purchases.tabs.edit", "Editar compra")
        : t("purchases.tabs.new", "Nueva compra"),
      icon: ctx.editing ? (ctx.readOnly ? "visibility" : "edit") : "add_box",
    },
  ];

  // ZH-LISTING-MAIN-ROW-NUMBER-FIX-07: showRowNumber activo — "Nro. factura" sigue siendo el
  // identificador funcional del documento; "N°" es solo el índice visual de fila (primera
  // columna), ambos coexisten en el listado principal.
  const purchaseListColumns: ZHDataTableColumn<PurchaseListItemDto>[] = [
    {
      key: "invoiceNumber",
      header: t("purchases.list.invoiceNumber", "Nro. factura"),
      render: (inv) => <span className="pf-invoice-number zh-code-value">{inv.invoiceNumber}</span>,
    },
    // FASE 5 PURCHASE-UX — proveedor (snapshot de la compra) y total del documento.
    {
      key: "supplier",
      header: t("purchases.list.supplier", "Proveedor"),
      render: (inv) => (
        <span className="pf-list-supplier">
          <span className="pf-list-supplier__name">{inv.supplierName}</span>
          {inv.supplierTaxId && (
            <ZHDataValue variant="code">{inv.supplierTaxId}</ZHDataValue>
          )}
        </span>
      ),
    },
    { key: "date", header: t("purchases.list.date", "Fecha"), render: (inv) => formatDate(inv.issueDate) },
    { key: "lines", header: t("purchases.list.lines", "Líneas"), align: "center", render: (inv) => inv.lineCount },
    {
      key: "total",
      header: t("purchases.list.total", "Total"),
      align: "right",
      render: (inv) => <ZHMoneyValue value={inv.grandTotal} precision="money" />,
    },
    {
      key: "status",
      header: t("purchases.list.status", "Estado"),
      // El estado persistido distingue Discarded de Cancelled.
      render: (inv) => {
        const badge = purchaseStatusBadge(inv.status);
        return <Badge variant={badge.variant} label={t(badge.key, badge.fallback)} />;
      },
    },
    {
      key: "actions",
      header: t("purchases.list.actions", "Acciones"),
      align: "center",
      render: (inv) => (
        <>
          <ZHIconButton
            icon={inv.status === "Draft" ? "edit" : "visibility"}
            variant="ghost"
            title={`${inv.status === "Draft" ? t("common.edit", "Editar") : t("common.view", "Ver")} factura ${inv.invoiceNumber}`}
            ariaLabel={`${inv.status === "Draft" ? t("common.edit", "Editar") : t("common.view", "Ver")} factura ${inv.invoiceNumber}`}
            onClick={() => void ctx.loadForEdit(inv.id)}
          />
          {inv.status === "Confirmed" && (
            <ZHIconButton
              icon="history"
              variant="ghost"
              title={t("purchases.actions.viewInventoryMovement", "Ver movimiento de inventario")}
              ariaLabel={`${t("purchases.actions.viewInventoryMovement", "Ver movimiento de inventario")} de factura ${inv.invoiceNumber}`}
              onClick={() => navigate(`/inventory/kardex?docId=${inv.id}&docType=PurchaseInvoice`)}
            />
          )}
        </>
      ),
    },
  ];

  return (
    <ErpPageTemplate
      title={t("purchases.page.title", "Facturas de compra")}
      subtitle={t("purchases.page.subtitle", "Registro de facturas de compra de proveedores.")}
    >
      <ZHTabBar
        tabs={tabs}
        activeTab={ctx.tab}
        onChange={(id) => {
          if (id !== "listado") {
            ctx.setTab(id);
            return;
          }
          // PURCHASE-DRAFT-RECOVERY D3 — nunca descartar cambios sin guardar en silencio.
          ctx.requestLeave(() => {
            ctx.resetForm();
            ctx.setTab(id);
          });
        }}
      />

      {/* ══════════════════════════════════════════════════════════ LISTADO */}
      {ctx.tab !== "nuevo" && ctx.saveError && <ZHPageNotice variant="error" message={ctx.saveError} detail={ctx.saveErrorDetails.join("\n")} />}
      {ctx.tab === "listado" && (
        <div className="prd-section">
          <div className="pg-table-controls zh-mb-16">
            <ZhTextInput
              placeholder={t(
                "purchases.list.searchPlaceholder",
                "Buscar por número, proveedor o RUC...",
              )}
              value={ctx.listSearchInput}
              onChange={(e) => ctx.setListSearchInput(e.target.value)}
            />
            <ZhSelect
              value={ctx.listStatus}
              onChange={(e) => ctx.setListStatus(e.target.value)}
            >
              <option value="">{t("purchases.list.allStatuses", "Todos los estados")}</option>
              <option value="Draft">{t("purchases.status.draft", "Borrador")}</option>
              <option value="Confirmed">{t("purchases.status.confirmed", "Confirmada")}</option>
              <option value="Cancelled">
                {t("purchases.status.cancelled", "Anulada")}
              </option>
              <option value="Discarded">{t("purchases.status.discarded", "Descartado")}</option>
            </ZhSelect>
            <ZHBtn
              variant="secondary"
              onClick={ctx.fetchList}
              disabled={ctx.listLoading}
            >
              <span
                className="material-symbols-outlined zh-icon-lg"
              >
                refresh
              </span>
            </ZHBtn>
          </div>
          <ZHDataTable
            columns={purchaseListColumns}
            rows={ctx.listItems}
            rowKey={(inv) => inv.id}
            loading={ctx.listLoading}
            showRowNumber
            rowNumberOffset={(ctx.listPage - 1) * ctx.listPageSize}
            tableClassName="table--compact table--neutral"
            emptyMessage={t("purchases.list.empty", "Sin compras registradas.")}
          />
          {!ctx.listLoading && ctx.listTotal > ctx.listPageSize && (
            <div className="pf-pagination">
              <span className="pf-pagination__summary">
                {t("purchases.pagination.page", "Página")} {ctx.listPage} {t("purchases.pagination.of", "de")}{" "}
                {Math.max(1, Math.ceil(ctx.listTotal / ctx.listPageSize))} —{" "}
                {t("purchases.pagination.purchaseCount", {
                  count: ctx.listTotal,
                })}
              </span>
              <div className="pf-pagination__actions">
                <ZHBtn
                  type="button"
                  variant="secondary"
                  disabled={ctx.listPage <= 1}
                  onClick={() => ctx.setListPage((p) => p - 1)}
                >
                  {t("common.previous", "Anterior")}
                </ZHBtn>
                <ZHBtn
                  type="button"
                  variant="secondary"
                  disabled={
                    ctx.listPage >= Math.ceil(ctx.listTotal / ctx.listPageSize)
                  }
                  onClick={() => ctx.setListPage((p) => p + 1)}
                >
                  {t("common.next", "Siguiente")}
                </ZHBtn>
              </div>
            </div>
          )}
        </div>
      )}

      {/* ══════════════════════════════════════════════════════════ NUEVO / EDITAR */}
      {ctx.tab === "nuevo" && (
        <div>
          <PurchaseFormFeedback ctx={ctx} />

          {/* ── SECTION 1: Header Cards ── */}
          <div className="pf-header-cards-row">
            {/* Datos del Proveedor */}
            <div className="pf-mini-card" id="purchase-supplier" tabIndex={-1}>
              <h4 className="pf-mini-card__title">
                <span className="material-symbols-outlined pf-mini-card__icon">
                  storefront
                </span>
                {t("purchases.supplier.title", "Datos del proveedor")}
                <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_SUPPLIER} />
              </h4>
              <div className="pf-mini-card__body">
                <ZHField
                  density="compact"
                  label={t("purchases.supplier.searchLabel", "RUC o nombre")}
                  fieldError={ctx.errors.supplierId?.message || (ctx.isSupplierInactiveBlocking ? `${ctx.supplierInactiveMessage} ${ctx.supplierInactiveDetail}` : undefined)}
                  required
                >
                  <SupplierSearchSelect
                    value={ctx.formWatch.supplierId || null}
                    onChange={ctx.handleSupplierChange}
                    disabled={ctx.fieldDisabled}
                  />
                </ZHField>
                {ctx.supplierProfile && (
                  <ZHField
                    density="compact"
                    label={t("purchases.supplier.legalName", "Razón social")}
                  >
                    <div className="pf-mini-card__readonly">
                      {ctx.supplierProfile.name}
                    </div>
                  </ZHField>
                )}
                {ctx.supplierProfile && (
                  <div className="pf-mini-card__badges">
                    {ctx.supplierProfile.isRequiredToKeepAccounting ? (
                      <Badge
                        variant="success"
                        label={
                          <>
                            <span className="material-symbols-outlined pf-badge__icon">
                              check_circle
                            </span>
                            {t(
                              "purchases.supplier.accountingRequired",
                              "Obligado contabilidad",
                            )}
                          </>
                        }
                      />
                    ) : (
                      <Badge
                        variant="error"
                        label={
                          <>
                            <span className="material-symbols-outlined pf-badge__icon">
                              cancel
                            </span>
                            {t(
                              "purchases.supplier.accountingNotRequired",
                              "No obligado",
                            )}
                          </>
                        }
                      />
                    )}
                    {ctx.supplierProfile.config?.isRetentionExempt && (
                      <Badge
                        variant="warning"
                        label={
                          <>
                            <span className="material-symbols-outlined pf-badge__icon">
                              shield
                            </span>
                            {t(
                              "purchases.supplier.retentionExempt",
                              "Exento retención",
                            )}
                          </>
                        }
                      />
                    )}

                  </div>
                )}
              </div>
            </div>

            {/* Datos del Documento */}
            <div className="pf-mini-card" id="purchase-document" tabIndex={-1}>
              <h4 className="pf-mini-card__title">
                <span className="material-symbols-outlined pf-mini-card__icon">
                  description
                </span>
                {t("purchases.document.title", "Datos del documento")}
              </h4>
              <div className="pf-mini-card__body">
                <ZHField
                  density="compact"
                  label={t("purchases.document.docType", "Tipo de documento")}
                  required
                  fieldError={ctx.sriDocTypesUnavailable ? t("purchases.validation.sriDocTypesUnavailableDetail") : ctx.errors.docTypeCode?.message}
                >
                  <ZhSelect
                    value={ctx.formWatch.docTypeCode}
                    onChange={(e) =>
                      ctx.setValue("docTypeCode", e.target.value)
                    }
                    disabled={ctx.fieldDisabled || !ctx.canUseSriDocTypes}
                  >
                    <option value="">
                      {!ctx.sriDocTypesLoaded
                        ? t(
                            "purchases.document.docTypesLoading",
                            "Cargando tipos de documento...",
                          )
                        : t(
                            "purchases.document.docTypePlaceholder",
                            "Seleccione tipo de documento",
                          )}
                    </option>
                    {ctx.sriDocTypes.map((d) => (
                      <option key={d.code} value={d.code}>
                        {d.code} — {d.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
                <div className="pf-mini-card__row">
                  <ZHField
                    density="compact"
                    label={t("purchases.document.number", "Nro. documento")}
                    required
                    fieldError={ctx.errors.invoiceNumber?.message}
                  >
                    <ZhTextInput
                      {...ctx.register("invoiceNumber")}
                      placeholder={t(
                        "purchases.document.numberPlaceholder",
                        "001-001...",
                      )}
                      maxLength={30}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                  <ZHField
                    density="compact"
                    label={t("purchases.document.issueDate", "Fecha emisión")}
                    required
                    fieldError={ctx.errors.issueDate?.message}
                  >
                    <ZhDateInput
                      {...ctx.register("issueDate")}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                </div>
              </div>
            </div>

            {/* Condiciones */}
            <div className="pf-mini-card">
              <h4 className="pf-mini-card__title">
                <span className="material-symbols-outlined pf-mini-card__icon">
                  handshake
                </span>
                {t("purchases.conditions.title", "Condiciones")}
              </h4>
              <div className="pf-mini-card__body">
                <ZHField
                  density="compact"
                  label={t("purchases.conditions.sriPaymentMethod", "Forma de pago SRI")}
                >
                  <ZhSelect
                    value={ctx.formWatch.sriPaymentMethodCode}
                    onChange={(e) =>
                      ctx.setValue("sriPaymentMethodCode", e.target.value)
                    }
                    disabled={ctx.fieldDisabled}
                  >
                    <option value="">
                      {t("common.selectPlaceholder", "— Seleccionar —")}
                    </option>
                    {ctx.sriPaymentMethods.map((m) => (
                      <option key={m.code} value={m.code}>
                        {m.code} — {m.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
                {/* taxSupportCode: código de metadato documental SRI (Sustento
                    Tributario) — no participa en cálculo de impuestos/retenciones,
                    solo se persiste como dato del comprobante. Migración visual
                    únicamente, ver reporte 14B-2. */}
                <ZHField
                  density="compact"
                  label={t("purchases.conditions.taxSupport", "Sustento tributario")}
                >
                  <ZhSelect
                    value={ctx.formWatch.taxSupportCode}
                    onChange={(e) =>
                      ctx.setValue("taxSupportCode", e.target.value)
                    }
                    disabled={ctx.fieldDisabled}
                  >
                    <option value="">
                      {t("common.selectPlaceholder", "— Seleccionar —")}
                    </option>
                    {ctx.sriTaxSupports.map((s) => (
                      <option key={s.code} value={s.code}>
                        {s.code} — {s.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
                <ZHField
                  density="compact"
                  label={t("purchases.conditions.paymentTerm", "Condición de pago")}
                >
                  <ZhSelect
                    value={ctx.formWatch.paymentTermId}
                    onChange={(e) =>
                      ctx.handlePaymentTermChange(e.target.value)
                    }
                    disabled={ctx.fieldDisabled}
                  >
                    <option value="">
                      {t("purchases.conditions.supplierDefault", "— Del proveedor —")}
                    </option>
                    {ctx.paymentTermsList
                      .filter((p) => p.isActive)
                      .map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name} ({p.summary})
                        </option>
                      ))}
                  </ZhSelect>
                </ZHField>
              </div>
            </div>
          </div>

          {/* ── Información Electrónica (colapsable) ── */}
          <div className="pf-collapsible">
            <button
              type="button"
              className="pf-collapsible__toggle"
              onClick={() => ctx.setShowElectronic((v) => !v)}
            >
              <span
                className="material-symbols-outlined zh-icon-lg"
              >
                qr_code_2
              </span>
              <span>{t("purchases.electronic.title", "Información electrónica")}</span>
              <span
                className={`material-symbols-outlined pf-collapsible__chevron ${ctx.showElectronic ? "pf-collapsible__chevron--open" : ""}`}
              >
                expand_more
              </span>
              {!ctx.showElectronic &&
                (ctx.formWatch.accessKey ||
                  ctx.formWatch.authorizationNumber) && (
                  <Badge
                    variant="info"
                    label={t("purchases.electronic.hasData", "Con datos")}
                    className="pf-collapsible__status"
                  />
                )}
            </button>
            {ctx.showElectronic && (
              <div className="pf-collapsible__body">
                <div className="pf-mini-card__row pf-mini-card__row--3col">
                  <ZHField
                    density="compact"
                    label={t("purchases.electronic.accessKey", "Clave de acceso")}
                  >
                    <ZhTextInput
                      {...ctx.register("accessKey")}
                      placeholder={t("purchases.electronic.accessKeyPlaceholder", "49 dígitos")}
                      maxLength={49}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                  <ZHField
                    density="compact"
                    label={
                      <>
                        {t("purchases.electronic.authorizationNumber", "Nro. autorización")}
                        <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_SRI_AUTHORIZATION} />
                      </>
                    }
                  >
                    <ZhTextInput
                      {...ctx.register("authorizationNumber")}
                      maxLength={49}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                  {/* authorizationDate: ZhDateTimeInput (type="datetime-local", fecha + hora)
                      — ZhDateInput fuerza type="date" (sin hora); no es equivalente.
                      El valor debe llegar ya normalizado a yyyy-MM-ddTHH:mm vía
                      toDateTimeLocalInputValue() al poblar el formulario (reset()). */}
                  <ZHField
                    density="compact"
                    label={
                      <>
                        {t("purchases.electronic.authorizationDate", "Fecha y hora autorización")}
                        <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_SRI_AUTHORIZATION_DATE} />
                      </>
                    }
                  >
                    <ZhDateTimeInput
                      density="compact"
                      {...ctx.register("authorizationDate")}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                </div>
              </div>
            )}
          </div>

          <div className="pf-header-cards-row pf-header-cards-row--mid">
            <div className="pf-mini-card">
              <h4 className="pf-mini-card__title">
                <span className="material-symbols-outlined pf-mini-card__icon">
                  inventory
                </span>
                {t("purchases.logistics.title", "Logística y totales")}
              </h4>
              <div className="pf-mini-card__body">
                <ZHField
                  density="compact"
                  label={
                    <>
                      {t("purchases.logistics.destinationWarehouse", "Bodega destino")}
                      <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_WAREHOUSE} />
                    </>
                  }
                >
                  <ZhSelect
                    value={ctx.formWatch.globalWarehouseId}
                    onChange={(e) => ctx.applyGlobalWarehouse(e.target.value)}
                    disabled={ctx.fieldDisabled}
                  >
                    <option value="">
                      {t("purchases.logistics.selectWarehouse", "— Seleccionar bodega —")}
                    </option>
                    {ctx.warehouses.map((w) => (
                      <option key={w.id} value={w.id}>
                        {w.code ? `${w.code} — ${w.name}` : w.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
              </div>
            </div>
          </div>

          {/* ── Observaciones ── */}
          <div className="pf-collapsible">
            <button
              type="button"
              className="pf-collapsible__toggle"
              onClick={() => ctx.setShowNotes((v) => !v)}
            >
              <span
                className="material-symbols-outlined zh-icon-lg"
              >
                notes
              </span>
              <span>{t("purchases.notes.title", "Observaciones")}</span>
              <span
                className={`material-symbols-outlined pf-collapsible__chevron ${ctx.showNotes ? "pf-collapsible__chevron--open" : ""}`}
              >
                expand_more
              </span>
              {!ctx.showNotes && ctx.formWatch.notes && (
                <Badge
                  variant="info"
                  label={t("purchases.notes.hasNotes", "Con notas")}
                  className="pf-collapsible__status"
                />
              )}
            </button>
            {ctx.showNotes && (
              <div className="pf-collapsible__body">
                <ZHField density="compact">
                  <ZhTextarea
                    {...ctx.register("notes")}
                    rows={3}
                    placeholder={t(
                      "purchases.notes.placeholder",
                      "Ingrese notas adicionales o descripciones del servicio/producto...",
                    )}
                    disabled={ctx.fieldDisabled}
                  />
                </ZHField>
              </div>
            )}
          </div>

          {/* ── MAIN LAYOUT ── */}
          <div className="pf-main-layout">
            <div>
              {/* ── Product Detail ── */}
              <div className="pdl-container">
                <div className="pdl-topbar">
                  <h4 className="pdl-topbar__title">
                    <span
                      className="material-symbols-outlined pf-icon--22"
                    >
                      inventory_2
                    </span>
                    {t("purchases.lines.title", "Detalle de productos")}
                    {ctx.xmlLinesSummary.total > 0 && <ZHDataValue variant="muted">{t("purchases.pendingProducts.summary.total", { count: ctx.xmlLinesSummary.total })}</ZHDataValue>}
                    {ctx.xmlLinesSummary.warnings > 0 && <Badge variant="warning" label={t("purchases.pendingProducts.summary.warnings", { count: ctx.xmlLinesSummary.warnings })} />}
                  </h4>
                  <div className="pdl-search__wrap" ref={ctx.globalSearchRef}>
                    <div className="pdl-search__input-wrap">
                      <span className="material-symbols-outlined pdl-search__icon">
                        search
                      </span>
                      <ZhTextInput
                        className="pdl-search__input"
                        placeholder={t(
                          "purchases.lines.globalSearchPlaceholder",
                          "Buscar producto por nombre o SKU...",
                        )}
                        value={ctx.globalQuery}
                        onChange={(e) => {
                          ctx.setGlobalQuery(e.target.value);
                          ctx.setGlobalOpen(true);
                        }}
                        onFocus={() => {
                          if (ctx.globalQuery.length >= 2)
                            ctx.setGlobalOpen(true);
                        }}
                        onKeyDown={ctx.handleGlobalKeyDown}
                        disabled={ctx.fieldDisabled}
                      />
                    </div>
                    {ctx.globalOpen && ctx.globalQuery.length >= 2 && (
                      <div className="pdl-search__dropdown">
                        <PurchaseSearchFeedback status={ctx.globalSearchStatus} empty={ctx.globalResults.length === 0}
                          query={ctx.globalQuery} onRetry={ctx.retryGlobalSearch} />
                        {ctx.globalResults.map((item, i) => (
                            <ZHPickerResultItem
                              key={item.id}
                              selected={i === ctx.globalFocusIdx}
                              onClick={() => void ctx.addLineWithItem(item)}
                              onMouseEnter={() => ctx.setGlobalFocusIdx(i)}
                              title={
                                <div className="pdl-search__result-row">
                                  <span className="pdl-search__result-name">
                                    {item.shortName}
                                  </span>
                                  {item.baseSalePrice != null && (
                                    <span className="pdl-search__result-price">
                                      <ZHMoneyValue
                                        value={item.baseSalePrice}
                                        precision="salesUnitPrice"
                                      />
                                    </span>
                                  )}
                                </div>
                              }
                              subtitle={
                                <div className="pdl-search__result-row pdl-search__result-row--meta">
                                  <span className="pdl-search__result-sku">
                                    {item.sku}
                                  </span>
                                  <Badge
                                    variant="neutral"
                                    size="md"
                                    upper
                                    label={item.itemTypeName}
                                    className="pdl-search__result-type"
                                  />
                                </div>
                              }
                            />
                          ))}
                      </div>
                    )}
                  </div>
                  <ZhSelect
                    className="pdl-type-filter-select"
                    value={ctx.globalFilter}
                    onChange={(e) => {
                      ctx.setGlobalFilter(e.target.value);
                      if (ctx.globalQuery.length >= 2) ctx.setGlobalOpen(true);
                    }}
                  >
                    <option value="all">{t("common.all", "Todo")}</option>
                    {ctx.itemTypeOptions.map((it) => (
                      <option key={it.id} value={it.id}>
                        {it.name}
                      </option>
                    ))}
                  </ZhSelect>
                  <ZHBtn
                    type="button"
                    variant="secondary"
                    onClick={() => ctx.setModalDistributeCost(true)}
                    disabled={!!ctx.distributeCostDisabledReason}
                  >
                    <span className="material-symbols-outlined zh-icon-md">
                      local_shipping
                    </span>
                    {t("purchases.distributeCost.trigger", "Distribuir flete/gasto")}
                  </ZHBtn>
                  <PurchaseActionReason reason={ctx.distributeCostDisabledReason}
                    action={t("purchases.distributeCost.trigger", "Distribuir flete/gasto")} />
                  {ctx.pendingProductSources.length > 0 && !ctx.fieldDisabled && (
                    <ZHBtn
                      type="button"
                      variant="primary"
                      onClick={() => ctx.openPendingProducts()}
                    >
                      <span className="material-symbols-outlined zh-icon-md">
                        playlist_add_check
                      </span>
                      {t("purchases.pendingProducts.trigger", {
                        count: ctx.pendingProductSources.length,
                      })}
                    </ZHBtn>
                  )}
                </div>

                {/* Line Cards */}
                <div className="pdl-lines" id="purchase-lines" tabIndex={-1}>
                  {(ctx.errors.lines?.message || ctx.errors.lines?.root?.message) && <ZHPageNotice variant="error" message={ctx.errors.lines.message ?? ctx.errors.lines.root?.message ?? ""} />}
                  {ctx.lines.map((l, idx) => (
                    <PurchaseLineCard
                      key={l._key}
                      line={l}
                      idx={idx}
                      ctx={ctx}
                    />
                  ))}
                  {ctx.lines.length === 0 && (
                    <div className="pdl-empty">
                      <span className="pdl-empty__icon-wrap">
                        <span className="material-symbols-outlined pdl-empty__icon">
                          add_shopping_cart
                        </span>
                      </span>
                      <div className="pdl-empty__body">
                        <p className="pdl-empty__title">
                          {t("purchases.lines.empty", "Sin productos agregados")}
                        </p>
                        <p className="pdl-empty__desc">
                          {t(
                            "purchases.lines.emptyDetail",
                            "Comience añadiendo ítems a esta transacción.",
                          )}
                        </p>
                      </div>
                      <ZHBtn
                        type="button"
                        variant="primary"
                        className="pdl-empty__cta"
                        onClick={ctx.addLine}
                        disabled={ctx.fieldDisabled}
                      >
                        <span className="material-symbols-outlined zh-icon-md">
                          add_circle
                        </span>
                        {t(
                          "purchases.lines.addTitle",
                          "Agregar nuevo producto o servicio",
                        )}
                      </ZHBtn>
                    </div>
                  )}
                  {ctx.lines.length > 0 && (
                    <div className="pdl-add-inline">
                      <ZHBtn
                        type="button"
                        variant="primary"
                        className="pdl-add-inline__cta"
                        onClick={ctx.addLine}
                        disabled={ctx.fieldDisabled}
                      >
                        <span className="material-symbols-outlined zh-icon-md">
                          add_circle
                        </span>
                        {t(
                          "purchases.lines.addTitle",
                          "Agregar nuevo producto o servicio",
                        )}
                      </ZHBtn>
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* ── Schedule + Summary ── */}
            <div className="pf-plazos-resumen-grid">
              <div>
                <PaymentScheduleSection ctx={ctx} />
                {/* ZH-PURCHASE-RETENTION-CONFIRM-01 — en borrador se define la retención (se emite
                    al confirmar); confirmada, se consulta/gestiona la ya emitida. */}
                {ctx.editing &&
                  (ctx.editing.status === "Draft" || ctx.editing.status === "Confirmed") && (
                    <RetentionSection ctx={ctx} />
                  )}
              </div>
              <PurchaseSummaryPanel lines={ctx.lines} vatRatesMap={ctx.vatRatesMap}
                iceRatesMap={ctx.iceRatesMap} totals={ctx.summaryTotals} />
            </div>
          </div>

          {/* Footer Actions */}
          <div className="pf-footer">
            <ZHBtn
              type="button"
              variant="secondary"
              onClick={() =>
                ctx.requestLeave(() => {
                  ctx.resetForm();
                  ctx.setTab("listado");
                })
              }
            >
              <span className="material-symbols-outlined zh-icon-md">
                arrow_back
              </span>
              {ctx.readOnly ? t("common.back", "Volver") : t("common.cancel", "Cancelar")}
            </ZHBtn>
            {/* FASE 5 PURCHASE-UX — una compra confirmada/anulada es solo lectura: sin Guardar. */}
            {!ctx.readOnly && (
            <ZHBtn
              id="purchase-save"
              type="button"
              variant="primary"
              onClick={ctx.handleSave}
              disabled={
                ctx.saving ||
                !ctx.formWatch.invoiceNumber.trim() ||
                !ctx.formWatch.supplierId.trim() ||
                !ctx.formWatch.docTypeCode.trim() ||
                !ctx.formWatch.issueDate ||
                !ctx.canUseSriDocTypes ||
                ctx.isDuplicateAccessKeyBlocking ||
                ctx.isSupplierInactiveBlocking ||
                ctx.hasLineSaveBlockers ||
                ctx.lines.length === 0
              }
            >
              <span className="material-symbols-outlined zh-icon-md">
                save
              </span>
              {ctx.saving
                ? t("common.saving", "Guardando...")
                : ctx.editing
                  ? t("purchases.actions.updateDraft", "Actualizar borrador")
                  : t("purchases.actions.saveDraft", "Guardar borrador")}
            </ZHBtn>
            )}
            {/* FASE 5 PURCHASE-UX — un borrador guardado se descarta (no se anula). "Cancelar" de
                arriba sigue siendo solo navegación. */}
            {canDiscardDraft(ctx.editing) && (
              <ZHBtn
                type="button"
                variant="destructive"
                onClick={() => ctx.setModalDiscardDraft(true)}
                disabled={ctx.saving}
              >
                <span className="material-symbols-outlined zh-icon-md">delete</span>
                {t("purchases.actions.discardDraft", "Descartar borrador")}
              </ZHBtn>
            )}
            {ctx.editing && ctx.editing.status === "Draft" && (<>
              <ZHBtn
                type="button"
                variant="primary"
                onClick={() => ctx.setModalConfirm(true)}
                disabled={
                  ctx.fieldDisabled ||
                  ctx.pendingReceptionItems.length > 0 ||
                  ctx.hasLineReadinessBlockers ||
                  ctx.scheduleBlocksConfirm ||
                  ctx.hasUnsavedFormChanges
                }

              >
                <span className="material-symbols-outlined zh-icon-md">
                  check_circle
                </span>
                {t("purchases.actions.confirm", "Confirmar compra")}
              </ZHBtn>
              <PurchaseActionReason reason={ctx.pendingReceptionItems.length > 0
                    ? t(
                        "purchases.validation.resolvePendingItemsBeforeConfirm",
                        "Resuelva los productos pendientes de vinculación antes de confirmar.",
                      )
                    : ctx.hasLineReadinessBlockers
                      ? ctx.confirmBlockedTitle
                      : ctx.scheduleBlocksConfirm
                        ? ctx.scheduleMismatchConfirmMessage
                        : ctx.hasUnsavedFormChanges
                        ? ctx.unsavedChangesMessage
                        : undefined} action={t("purchases.actions.confirm", "Confirmar compra")} />
            </>)}
            {ctx.editing && ctx.editing.status === "Confirmed" && (
              <ZHBtn
                type="button"
                variant="secondary"
                title={t("purchases.creditNote.actions.openCreditNote", "Abrir nota de crédito")}
                onClick={() =>
                  navigate(`/purchases/credit-notes/new?invoiceId=${ctx.editing!.id}`)
                }
              >
                <span className="material-symbols-outlined zh-icon-md">
                  request_quote
                </span>
                {t("purchases.creditNote.actions.creditNoteButton", "Nota de crédito")}
              </ZHBtn>
            )}
            {ctx.editing && ctx.editing.status === "Confirmed" && (
              <ZHBtn
                type="button"
                variant="destructive"
                onClick={() => ctx.setModalCancelReason(true)}
                disabled={ctx.saving}
              >
                <span className="material-symbols-outlined zh-icon-md">
                  block
                </span>
                {t("purchases.actions.cancelPurchase", "Anular compra")}
              </ZHBtn>
            )}
          </div>
        </div>
      )}

      {/* ══════════════════════════════════════════════════════════ MODALS */}
      {/* Modal de descuento global (Desc. global del antiguo dropdown "Gastos y acciones",
          PURCHASE-LANDED-COST-PANEL-REMOVE-01) sigue sin disparador propio en esta pantalla;
          handleApplyDiscount/modalDiscount se conservan en el hook por si se integra al flujo
          de "Distribuir flete/gasto" en una iteración futura. */}

      {/* PURCHASE-DISTRIBUTE-COST-BEFORE-SAVE-01 — dos orígenes de datos según si la compra ya
          existe persistida (ctx.editing) o es nueva sin guardar (ctx.lines del formulario):
          mismo modal, mismo cálculo, distinto destino al aplicar (backend vs. formulario). */}
      <ResolvePendingProductsModal
        open={ctx.modalPendingProducts}
        sources={ctx.modalPendingProductSources}
        supplierName={ctx.supplierProfile?.name ?? ""}
        supplierId={ctx.formWatch.supplierId || undefined}
        onClose={ctx.closePendingProducts}
        onResolved={(result) => void ctx.applyResolvedLines(result)}
      />
      {ctx.breakdownSourceId && ctx.lines.find(l => l.purchaseReceptionLineId === ctx.breakdownSourceId)?.xmlSource && (
        <PurchaseLineBreakdownModal
          key={ctx.breakdownSourceId}
          source={ctx.lines.find(l => l.purchaseReceptionLineId === ctx.breakdownSourceId)!}
          lines={ctx.lines}
          supplierId={ctx.formWatch.supplierId}
          warehouseId={ctx.formWatch.globalWarehouseId}
          onCancel={() => ctx.setBreakdownSourceId(null)}
          onApply={rows => ctx.applyBreakdown(ctx.breakdownSourceId!, rows)}
        />
      )}
      <DistributeCostModal
        error={ctx.distributionError}
        open={ctx.modalDistributeCost}
        busy={ctx.distributionBusy}
        invoice={ctx.editing}
        formLines={ctx.lines}
        onCancel={() => ctx.setModalDistributeCost(false)}
        onSave={ctx.saveCostDistribution}
        onRemove={ctx.removeCostDistribution}
      />

      <ZHConfirmModal
        open={ctx.modalConfirm}
        variant="warning"
        title={t("purchases.confirm.title", "Confirmar compra")}
        message={
          <>
            {ctx.editing && (
              <PurchaseConfirmSummary
                supplierName={ctx.supplierProfile?.name ?? ctx.editing.supplierName}
                invoiceNumber={ctx.editing.invoiceNumber}
                issueDate={ctx.editing.issueDate}
                lineCount={ctx.editing.lines.length}
                total={ctx.editing.grandTotal}
              />
            )}
            <PurchaseConfirmChecklist summary={xmlConfirmChecklist} lineWarnings={confirmLineWarnings}
              commercialRisk={confirmCommercialRisk} />
            {ctx.retentionIntent.appliesRetention && ctx.whPreview && (
              <ZHPageNotice
                variant="info"
                icon="receipt_long"
                message={t(
                  "purchases.confirm.withRetention",
                  "Se emitirá la retención junto con la compra, en una sola operación.",
                )}
                detail={`${t("purchases.retention.totalToWithhold", "Total a retener")}: ${formatMoneyWithSymbol(ctx.whPreview.totalRetained, moneyDecimals)}`}
              />
            )}
          </>
        }
        confirmLabel={t("common.confirm", "Confirmar")}
        onCancel={() => ctx.setModalConfirm(false)}
        onConfirm={ctx.handleConfirm}
      />

      <ZHPromptModal
        open={ctx.modalCancelReason}
        variant="danger"
        title={t("purchases.cancel.title", "Anular compra")}
        message={
          ctx.editing
            ? buildPurchaseCancelMessage(t, ctx.editing.invoiceNumber, ctx.retention)
            : ""
        }
        label={t("purchases.cancel.reasonLabel", "Motivo de anulación")}
        placeholder={t("purchases.cancel.reasonPlaceholder", "Ingrese el motivo...")}
        confirmLabel={t("purchases.cancel.confirm", "Anular")}
        onCancel={() => ctx.setModalCancelReason(false)}
        onConfirm={ctx.handleCancel}
      />

      {/* FASE 5 PURCHASE-UX — descartar borrador: soft, sin reversos, motivo opcional. */}
      <ZHPromptModal
        open={ctx.modalDiscardDraft}
        variant="danger"
        required={false}
        title={t("purchases.discardDraft.title", "Descartar borrador")}
        message={
          ctx.editing
            ? t("purchases.discardDraft.message", { invoiceNumber: ctx.editing.invoiceNumber })
            : ""
        }
        label={t("purchases.discardDraft.reasonLabel", "Motivo (opcional)")}
        placeholder={t("purchases.discardDraft.reasonPlaceholder", "Ej.: cargado por error, duplicado...")}
        confirmLabel={t("purchases.discardDraft.confirm", "Descartar borrador")}
        onCancel={() => ctx.setModalDiscardDraft(false)}
        onConfirm={(reason) => void ctx.handleDiscardDraft(reason)}
      />

      {/* PURCHASE-DRAFT-RECOVERY D3 — salir con cambios sin guardar exige confirmación explícita. */}
      <ZHConfirmModal
        open={ctx.pendingLeave}
        variant="warning"
        title={t("purchases.draftRecovery.leaveTitle", "Hay cambios sin guardar")}
        message={t(
          "purchases.draftRecovery.leaveMessage",
          "Si sale ahora se perderán los cambios que no guardó en el borrador. Use \"Guardar borrador\" para conservarlos.",
        )}
        confirmLabel={t("purchases.draftRecovery.leaveConfirm", "Descartar cambios")}
        cancelLabel={t("purchases.draftRecovery.leaveCancel", "Seguir editando")}
        onCancel={ctx.cancelLeave}
        onConfirm={ctx.confirmLeave}
      />

      {/* ZH-RETENTION-SRI-ANNULMENT-01 — la retención ya está autorizada por el SRI. */}
      <ZHConfirmModal
        open={ctx.sriAnnulmentReason !== null}
        variant="warning"
        title={t("purchases.sriAnnulment.title", "La retención ya fue autorizada por el SRI")}
        message={t(
          "purchases.sriAnnulment.message",
          "La retención ya fue autorizada por el SRI y debe anularse primero ante el SRI. Se registrará la solicitud con el motivo indicado; la compra seguirá vigente (no se anula, no se revierte la cuenta por pagar ni la contabilidad) hasta que el SRI informe ANULADO (el ERP lo verifica automáticamente en el SRI).",
        )}
        confirmLabel={t("purchases.sriAnnulment.start", "Iniciar proceso de anulación")}
        cancelLabel={t("common.cancel", "Cancelar")}
        onCancel={() => ctx.setSriAnnulmentReason(null)}
        onConfirm={() => void ctx.handleStartSriAnnulment()}
      />

    </ErpPageTemplate>
  );
}

// ── Internal Components ────────────────────────────────────────────────

/** FASE 5 PURCHASE-UX — identifica la compra antes de confirmarla (evita confirmar la equivocada). */
function PurchaseConfirmSummary({
  supplierName,
  invoiceNumber,
  issueDate,
  lineCount,
  total,
}: {
  supplierName: string;
  invoiceNumber: string;
  issueDate: string;
  lineCount: number;
  total: number;
}) {
  const { t } = useI18n();
  return (
    <div className="pf-confirm-summary">
      <ZHInfoRow
        wide
        label={<ZHFieldLabel size="sm">{t("purchases.confirm.summary.supplier", "Proveedor")}</ZHFieldLabel>}
        value={<ZHDataValue variant="strong">{supplierName}</ZHDataValue>}
      />
      <ZHInfoRow
        wide
        label={<ZHFieldLabel size="sm">{t("purchases.confirm.summary.number", "Número")}</ZHFieldLabel>}
        value={<ZHDataValue variant="code">{invoiceNumber}</ZHDataValue>}
      />
      <ZHInfoRow
        wide
        label={<ZHFieldLabel size="sm">{t("purchases.confirm.summary.date", "Fecha")}</ZHFieldLabel>}
        value={<ZHDataValue variant="numeric">{formatDate(issueDate)}</ZHDataValue>}
      />
      <ZHInfoRow
        wide
        label={<ZHFieldLabel size="sm">{t("purchases.confirm.summary.lines", "Líneas")}</ZHFieldLabel>}
        value={<ZHDataValue variant="numeric">{lineCount}</ZHDataValue>}
      />
      <ZHInfoRow
        wide
        label={<ZHFieldLabel size="sm">{t("purchases.confirm.summary.total", "Total")}</ZHFieldLabel>}
        value={<ZHMoneyValue value={total} precision="money" emphasis="strong" />}
      />
    </div>
  );
}

/** Margin indicator: tone by state; without a sale price there is no margin to show. */
const MARGIN_BADGE_VARIANT = {
  positive: "success",
  zero: "warning",
  negative: "error",
  noSalePrice: "neutral",
  unavailable: "neutral",
} as const satisfies Record<LineMarginState, BadgeVariant>;

function marginBadgeLabel(
  profitability: { marginState: LineMarginState; marginPct: string },
  t: ReturnType<typeof useI18n>["t"],
) {
  if (profitability.marginState === "noSalePrice") return t("purchases.lines.marginNoSalePrice", "Sin PVP");
  if (profitability.marginState === "unavailable") return profitability.marginPct;
  return `${profitability.marginPct}%`;
}

type PurchaseLineDraft = PurchaseLineFormValues;
type TFunction = ReturnType<typeof useI18n>["t"];

function readinessActionLabel(
  action: PurchaseLineReadinessAction | undefined,
  t: TFunction,
) {
  switch (action) {
    case "SELECT_ITEM":
      return t("purchases.lineReadiness.action.selectItem", "Seleccionar producto");
    case "CREATE_ITEM":
      return t("purchases.lineReadiness.action.createItem", "Crear producto");
    case "SELECT_PRESENTATION":
      return t(
        "purchases.lineReadiness.action.selectPresentation",
        "Seleccionar presentación",
      );
    case "SAVE_PRESENTATION":
      return t(
        "purchases.lineReadiness.action.savePresentation",
        "Guardar presentación",
      );
    case "SELECT_WAREHOUSE":
      return t("purchases.lineReadiness.action.selectWarehouse", "Asignar bodega");
    case "REVIEW_TAX":
      return t("purchases.lineReadiness.action.reviewTax", "Revisar impuesto");
    case "NONE":
    case undefined:
      return "";
  }
}

function ProductLineActionMenu({
  line: l,
  ctx,
  disabled,
  onProductSelect,
  onViewProduct,
  onUnlinkProduct,
}: {
  line: PurchaseLineDraft;
  ctx: ReturnType<typeof usePurchasesPage>;
  disabled: boolean;
  onProductSelect: (product: ProductProfile) => void;
  onViewProduct: () => void;
  onUnlinkProduct: () => void;
}) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [selectorOpen, setSelectorOpen] = useState(false);
  const [productModalOpen, setProductModalOpen] = useState(false);
  const hasProduct = !!l.itemId;
  const canEditProduct = !disabled;
  const triggerDisabled = !hasProduct && !canEditProduct;

  // Click-outside/Escape ahora los maneja ZHLineAction kind="menu" internamente; este
  // componente solo decide si el cierre procede (se ignora mientras hay un modal interno
  // abierto — mismo gate que antes tenía el handler de mousedown).
  const handleOpenChange = (next: boolean) => {
    if (!next && productModalOpen) return;
    setOpen(next);
    if (!next) setSelectorOpen(false);
  };

  const closeAfterModal = () => {
    setProductModalOpen(false);
    setOpen(false);
    setSelectorOpen(false);
  };

  return (
    <ZHLineAction
      kind="menu"
      icon="grid_view"
      tone="primary"
      label={t("purchases.lines.productActions", "Acciones del producto")}
      title={t("purchases.lines.productActions", "Acciones del producto")}
      disabled={triggerDisabled}
      open={open}
      onOpenChange={handleOpenChange}
    >
      {!hasProduct ? (
        <>
          <ZHLineActionItem
            icon="search"
            label={t("purchases.lines.selectProduct", "Seleccionar producto")}
            disabled={disabled || ctx.matchingKey === l._key}
            onClick={() => setSelectorOpen((current) => !current)}
          />
          {selectorOpen && (
            <div className="pdl-product-menu__picker">
              <ProductPicker
                disabled={disabled || ctx.matchingKey === l._key}
                vatRates={ctx.vatRatesMap}
                initialQuery={l.description?.trim() || undefined}
                onSelect={(product) => {
                  onProductSelect(product);
                  setOpen(false);
                  setSelectorOpen(false);
                }}
              />
            </div>
          )}
          {l.purchaseReceptionLineId ? (
            <ReceptionCreateItemAction
              line={l}
              ctx={ctx}
              disabled={disabled}
              buttonClassName="zh-line-action-item"
              label={t("purchases.lines.createProduct", "Crear producto")}
              onAfterModalClose={closeAfterModal}
            />
          ) : (
            <CreateItemLineAction
              line={l}
              ctx={ctx}
              disabled={disabled}
              buttonClassName="zh-line-action-item"
              label={t("purchases.lines.createProduct", "Crear producto")}
              onModalOpenChange={setProductModalOpen}
              onAfterModalClose={closeAfterModal}
            />
          )}
        </>
      ) : (
        <>
          <ZHLineActionItem
            icon="visibility"
            label={t("purchases.lines.viewProduct", "Ver producto")}
            onClick={() => {
              setOpen(false);
              onViewProduct();
            }}
          />
          {canEditProduct && (
            <UpdateItemAction
              line={l}
              ctx={ctx}
              buttonClassName="zh-line-action-item"
              label={t("purchases.lines.updateProduct", "Actualizar producto")}
              onModalOpenChange={setProductModalOpen}
              onAfterModalClose={closeAfterModal}
            />
          )}
          {canEditProduct && l.purchaseReceptionLineId && (
            <CorrectMatchAction
              line={l}
              ctx={ctx}
              buttonClassName="zh-line-action-item"
              onModalOpenChange={setProductModalOpen}
              onAfterModalClose={closeAfterModal}
            />
          )}
          {canEditProduct && (
            <ZHLineActionItem
              icon="link_off"
              label={
                ctx.matchingKey === l._key
                  ? t("purchases.lines.unlinkingProduct", "Desvinculando...")
                  : t("purchases.lines.unlinkProduct", "Desvincular producto")
              }
              disabled={ctx.matchingKey === l._key}
              onClick={() => {
                setOpen(false);
                onUnlinkProduct();
              }}
            />
          )}
        </>
      )}
    </ZHLineAction>
  );
}

export function PurchaseLineCard({
  line: l,
  idx,
  ctx,
}: {
  line: PurchaseLineDraft;
  idx: number;
  ctx: ReturnType<typeof usePurchasesPage>;
}) {
  const { t } = useI18n();
  const lineLocked = ctx.fieldDisabled || !!l.xmlAllocation;
  // Origen o destino de una distribución de costos: no se elimina ni se desglosa (todo su grupo XML).
  const inCostDistribution = (line: PurchaseLineDraft) => !!line.lineId && ctx.costDistributionLineIds.has(line.lineId);
  const distributionLocked = inCostDistribution(l);
  const breakdownLocked = ctx.lines.some(x => x.purchaseReceptionLineId === l.purchaseReceptionLineId && inCostDistribution(x));
  const distributionLockReason = t("purchases.distributeCost.lineLocked",
    "La línea participa en una distribución de costos. Edite o retire la distribución antes de eliminarla o desglosarla.");
  const precisionPolicy = usePrecisionPolicy(); // presentación de línea (06)
  const viewMatchedItem = useOpenItemDetail();
  // Estado local de expandir/colapsar SOLO la banda inferior de detalle
  // (Producto recibido XML / Presentación e inventario / Información
  // comercial) — no se persiste (ni backend ni localStorage), vive por
  // instancia de línea (key={l._key} en el .map() que renderiza esta
  // card). Inicia expandido para no ocultar información por defecto.
  const [collapsed, setCollapsed] = useState(false);
  const fieldIssues = purchaseFieldErrors(ctx.errors?.lines?.[idx]);
  const errorId = (field: string) => `purchase-line-${l._key}-${field}-error`;
  const fieldMessage = (fields: string[]) => [...new Set(fieldIssues
    .filter(issue => fields.some(field => issue.path === field || issue.path.startsWith(`${field}.`)))
    .map(issue => issue.message))].join(" ") || undefined;
  const identityFields = ["description", "itemId"];
  const quantityFields = ["quantity", "quantityInBaseUom"];
  const costFields = ["unitPrice", "baseUnitPrice"];
  const taxFields = ["vatCode", "iceCode", "taxes"];
  const headerFields = [...identityFields, ...quantityFields, ...costFields, ...taxFields, "warehouseId"];
  const detailErrors = fieldIssues.filter(issue => !headerFields.some(field => issue.path === field || issue.path.startsWith(`${field}.`)));

  // PURCHASE-LINE-HEADER-EDIT-RECALCULATE-TAX-TOTAL-01 — Base/IVA/ICE/Total de la cabecera
  // editable viven en `vm.editableHeader`, recalculados en cada render desde quantity/unitPrice/
  // discountPct/vatCode/iceCode actuales — nunca desde el snapshot XML (`vm.xml.*`, que se muestra
  // aparte en "Producto recibido (XML)" y no cambia con la edición). Antes de este fix, una línea
  // con origen XML mostraba xmlTaxValue/xmlTotalLine en cabecera sin importar que el usuario ya
  // hubiera cambiado quantity/unitPrice — Base imp. sí se recalculaba pero IVA/Total quedaban
  // pegados al valor documental original.
  const vm = buildPurchaseLinePresentation(l, precisionPolicy, t, ctx.vatRatesMap, ctx.iceRatesMap);
  const quantityDecimals = resolvePrecisionDecimals(precisionPolicy, "quantity");
  const purchasePriceDecimals = resolvePrecisionDecimals(precisionPolicy, "purchaseUnitPrice");
  const sub = vm.editableHeader.taxableBase;
  const vatAmt = vm.editableHeader.vatAmount;
  const iceAmt = vm.editableHeader.iceAmount;
  const irbpnrAmt = vm.editableHeader.irbpnrAmount;
  const total = vm.editableHeader.total;
  const ctxData = l.context;
  const vatPct = ctxData?.vatPercent ?? ctx.vatRatesMap[l.vatCode] ?? 0;
  const readiness = ctx.lineReadinessByKey[l._key];
  const readinessFields: Record<string, string[]> = {
    MISSING_ITEM: ["itemId", "description"], MISSING_PRESENTATION: ["packagingLevelId"],
    MISSING_WAREHOUSE: ["warehouseId"], INVALID_TAX: taxFields,
  };
  const readinessField = readinessFields[readiness?.status ?? ""];
  const readinessHasFieldError = readinessField && fieldMessage(readinessField);
  const primaryReadinessAction = readinessActionLabel(readiness?.primaryAction, t);
  const secondaryReadinessAction = readinessActionLabel(
    readiness?.secondaryAction,
    t,
  );
  const readinessActions = [primaryReadinessAction, secondaryReadinessAction]
    .filter(Boolean)
    .join(" / ");
  const showReadinessActions =
    readiness?.status !== "MISSING_ITEM" &&
    readiness?.status !== "MISSING_PRESENTATION" &&
    readinessActions;
  const packagingLevels = ctxData?.packagingLevels ?? [];
  // FASE 5 PURCHASE-UX — Cantidad/Costo a registrar (unidad base) son autoritativos. Línea XML sin
  // editar: la presentación expresa la cantidad ORIGINAL del proveedor (2 cajas x12 → 24). Línea
  // manual o XML ya editada: se conserva exactamente lo a registrar, la presentación solo se deriva.
  // Ver resolvePackagingChange.
  const handlePackagingChange = (packagingLevelId: string) => {
    const selected = packagingLevels.find((p) => p.id === packagingLevelId);
    const nextFactor = selected?.baseQuantity ?? 1;
    const next = resolvePackagingChange(
      l,
      vm.inventory.conversionFactorValue,
      nextFactor,
      packagingLevelId || undefined,
    );
    ctx.updateLine(l._key, "packagingLevelId", packagingLevelId || undefined);
    ctx.updateLine(l._key, "uomCode", selected?.uomCode ?? ctxData?.baseUomCode);
    ctx.updateLine(l._key, "baseUomCode", ctxData?.baseUomCode);
    ctx.updateLine(l._key, "conversionFactor", nextFactor);
    ctx.updateLine(l._key, "quantity", next.quantity);
    ctx.updateLine(l._key, "unitPrice", next.unitPrice);
    ctx.updateLine(l._key, "quantityInBaseUom", next.quantityInBaseUom);
    ctx.updateLine(l._key, "baseUnitPrice", next.baseUnitPrice);
    ctx.updateLine(l._key, "xmlOriginFactor", next.xmlOriginFactor);
    ctx.updateLine(l._key, "xmlOriginPackagingLevelId", next.xmlOriginPackagingLevelId);
  };
  // FASE 5 PURCHASE-UX — acción EXPLÍCITA (única) para volver a la cantidad/costo del proveedor,
  // en la presentación de origen.
  const handleResetToSupplierOrigin = () => {
    const origin = resetLineToSupplierOrigin(l, vm.inventory.conversionFactorValue);
    if (!origin) return;
    const originLevel = packagingLevels.find((p) => p.id === origin.packagingLevelId);
    ctx.updateLine(l._key, "packagingLevelId", origin.packagingLevelId);
    ctx.updateLine(l._key, "uomCode", originLevel?.uomCode ?? ctxData?.baseUomCode);
    ctx.updateLine(l._key, "conversionFactor", origin.conversionFactor);
    ctx.updateLine(l._key, "xmlOriginFactor", origin.xmlOriginFactor);
    ctx.updateLine(l._key, "xmlOriginPackagingLevelId", origin.xmlOriginPackagingLevelId);
    ctx.updateLine(l._key, "quantity", origin.quantity);
    ctx.updateLine(l._key, "unitPrice", origin.unitPrice);
    ctx.updateLine(l._key, "quantityInBaseUom", origin.quantityInBaseUom);
    ctx.updateLine(l._key, "baseUnitPrice", origin.baseUnitPrice);
  };
  const handleProductSelect = (p: ProductProfile) => {
    const patch = resolvePurchaseItemSelection(p, {
      existingLine: l,
    });
    ctx.updateLine(l._key, "_readinessIssue", undefined);
    if (patch.vatCode !== undefined)
      ctx.updateLine(l._key, "vatCode", patch.vatCode);
    if (patch.iceCode !== undefined)
      ctx.updateLine(l._key, "iceCode", patch.iceCode);
    if (l.purchaseReceptionLineId) {
      void ctx.handleMatchItem(
        l._key,
        p.id,
        patch.description ?? p.label,
      );
      return;
    }
    ctx.updateLine(l._key, "itemId", patch.itemId ?? p.id);
    ctx.updateLine(
      l._key,
      "description",
      patch.description ?? p.label,
    );
    const wh = l.warehouseId || ctx.formWatch.globalWarehouseId;
    void ctx.fetchItemContext(l._key, p.id, wh);
  };
  const handleUnlinkProduct = () => {
    if (l.purchaseReceptionLineId) {
      void ctx.handleUnmatchItem(l._key);
      return;
    }
    ctx.updateLine(l._key, "itemId", undefined);
    ctx.updateLine(l._key, "description", "");
    ctx.updateLine(l._key, "context", undefined);
    ctx.updateLine(l._key, "_readinessIssue", undefined);
  };
  const productName =
    ctxData?.shortName || l.description?.split(" — ")[1] || l.description;
  const detailId = `pdl-detail-${l._key}`;
  // ZH-DS-LINE-ISSUES-01 — incidencias de la línea, cada una una sola vez, para la
  // etiqueta anclada al borde de la tarjeta. Sin incidencias no hay etiqueta (sin badges de éxito).
  const lineIssues = buildPurchaseLineIssues({ vm, readiness, t, hasFieldErrors: fieldIssues.length > 0,
    readinessHasFieldError, showReadinessActions, lineLocked, onReset: handleResetToSupplierOrigin,
    onGoToField: () => {
      setCollapsed(false);
      requestAnimationFrame(() => {
        const line = document.getElementById(`purchase-line-${l._key}-fields`);
        (line?.querySelector<HTMLElement>('[aria-invalid="true"]:not(:disabled)') ?? line)?.focus();
      });
    },
  });
  // Franja operativa (no son estados de error): solo si hay equivalencia o nota de costo.
  const hasOperationalInfo =
    vm.registration.show && (!!vm.registration.presentationDetail || !!vm.registration.costNote);

  return (
    // DS-LINE-CARD-UNIFY-02: wrapper externo unificado (ZHLineCard) en vez de la caja local
    // `.zh-detail-line pdl-line-row` — mismo rail izquierdo aprobado (número, menú de producto,
    // duplicar, eliminar, colapso — plantilla DESIGN-SYSTEM-LINE-RAIL-04), pasado como prop
    // `rail`; el contenido de la línea (`.pdl-line`, sin cambios internos) sigue como children.
    <ZHLineCard
      className={`pdl-line-card${lineIssues.length > 0 ? " pdl-line-card--has-issues" : ""}`}
      rail={
        <>
          <span className="pdl-line-row__num">
            {String(idx + 1).padStart(2, "0")}
          </span>
          {canBreakDown(l) && (
            <ZHLineAction icon="call_split" variant="default" size="md" compact showText={false}
              title={breakdownLocked ? distributionLockReason : t(l.xmlAllocation ? "purchases.breakdown.edit" : "purchases.breakdown.action", l.xmlAllocation ? "Editar desglose" : "Desglosar")}
              label={t(l.xmlAllocation ? "purchases.breakdown.edit" : "purchases.breakdown.action", l.xmlAllocation ? "Editar desglose" : "Desglosar")}
              ariaLabel={t(l.xmlAllocation ? "purchases.breakdown.edit" : "purchases.breakdown.action", l.xmlAllocation ? "Editar desglose" : "Desglosar")}
              disabled={ctx.fieldDisabled || breakdownLocked} onClick={() => ctx.setBreakdownSourceId(l.purchaseReceptionLineId!)} />
          )}
          <ProductLineActionMenu
            line={l}
            ctx={ctx}
            disabled={lineLocked}
            onProductSelect={handleProductSelect}
            onViewProduct={() => viewMatchedItem(l.itemId as string)}
            onUnlinkProduct={handleUnlinkProduct}
          />
          <ZHLineAction
            icon="content_copy"
            variant="default"
            size="md"
            compact
            showText={false}
            title={t("purchases.lines.duplicate", "Duplicar")}
            label={t("purchases.lines.duplicate", "Duplicar")}
            disabled={lineLocked}
            onClick={() => ctx.duplicateLine(l._key)}
          />
          <ZHRowDeleteAction
            compact
            showText={false}
            title={distributionLocked ? distributionLockReason : t("purchases.lines.delete", "Eliminar")}
            label={t("purchases.lines.delete", "Eliminar")}
            ariaLabel={t("purchases.lines.delete", "Eliminar")}
            disabled={lineLocked || distributionLocked}
            onClick={() => ctx.removeLine(l._key)}
          />
        </>
      }
    >
      <div className="pdl-line" id={`purchase-line-${l._key}`} tabIndex={-1}>
      {lineIssues.length > 0 && <PurchaseLineIssuesTag issues={lineIssues} />}
      <div className="pdl-line__main" id={`purchase-line-${l._key}-fields`} tabIndex={-1}>
        <div className="pdl-line__summary">
          <div className="pdl-line__identity" role="group" aria-label={t("purchases.lines.product", "Producto")}
            aria-describedby={fieldMessage(identityFields) ? errorId("identity") : undefined}>
            {!l.itemId ? (
              <div className="pdl-line__product-name pdl-line__product-name--muted">
                {t(
                  "purchases.lines.noLinkedProduct",
                  "Sin producto vinculado",
                )}
              </div>
            ) : (
              <>
                <div className="pdl-line__product-name zh-row-title">
                  {productName}
                </div>
                {/* PURCHASE-LINE-HEADER-UX-01 — mismos datos/orden que antes (SKU · Cód.
                    proveedor), en pares label/valor: ZHFieldLabel size="sm" (global) para el
                    label + ZHDataValue variant="code" (global, mono documentado) para el
                    código — PURCHASES-DS-GLOBAL-COMPONENT-04. */}
                <div className="pdl-line__product-meta">
                  <span className="pdl-line__product-meta-item">
                    <ZHFieldLabel size="sm">SKU</ZHFieldLabel>
                    <ZHDataValue variant="code">{vm.item.sku}</ZHDataValue>
                  </span>
                  <ZHDataValue variant="muted">·</ZHDataValue>
                  <span className="pdl-line__product-meta-item">
                    <ZHFieldLabel size="sm">
                      {t("purchases.lines.supplierCode", "Cód. proveedor")}
                    </ZHFieldLabel>
                    <ZHDataValue variant="code">{vm.xml.supplierCode}</ZHDataValue>
                  </span>
                </div>
              </>
            )}
            <PurchaseFieldError id={errorId("identity")} message={fieldMessage(identityFields)} />
            {/* PURCHASE-LINE-COMPACT-02 — disclosure "Detalle" (ZH-DS-DISCLOSURE-01) dentro de la
                cabecera; el estado de la línea vive en la etiqueta anclada (ISSUES-TAG-01). */}
            <div className="pdl-line__identity-actions">
              <ZHBtn
                type="button"
                variant="secondary"
                size="sm"
                expanded={!collapsed}
                aria-controls={detailId}
                onClick={() => setCollapsed((current) => !current)}
              >
                {t("purchases.lines.detailToggle", "Detalle")}
              </ZHBtn>
            </div>
          </div>
          {/* PURCHASE-LINE-HEADER-UX-02 — bodega como columna operativa propia (criterio
              "Ubicación" de Ventas), en vez de un campo suelto debajo del producto. Mismo
              componente/handler/condición (l.itemId) que antes: solo cambió dónde se renderiza. */}
          <div className="pdl-line__warehouse">
            {l.itemId && (
              <>
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.warehouse", "Bodega")}
                </ZHFieldLabel>
                <ZhSelect
                  className="pdl-line__wh-select-compact"
                  aria-label={t("purchases.lines.warehouse", "Bodega")}
                  aria-invalid={!!fieldMessage(["warehouseId"])} aria-describedby={fieldMessage(["warehouseId"]) ? errorId("warehouse") : undefined}
                  density="compact"
                  value={l.warehouseId ?? ctx.formWatch.globalWarehouseId ?? ""}
                  onChange={(e) =>
                    ctx.updateLineWarehouse(l._key, e.target.value || null)
                  }
                  disabled={ctx.fieldDisabled}
                >
                  <option value="">
                    {t("purchases.logistics.selectWarehouse", "— Seleccionar bodega —")}
                  </option>
                  {ctx.warehouses.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.code ? `${w.code} — ${w.name}` : w.name}
                    </option>
                  ))}
                </ZhSelect>
              </>
            )}
            <PurchaseFieldError id={errorId("warehouse")} message={fieldMessage(["warehouseId"])} />
          </div>
          {/* PURCHASE-LINE-HEADER-UX-03 — cada métrica pasa a ser columna directa de
              .pdl-line__summary (antes agrupadas en .pdl-line__metrics) para que la cabecera sea
              un grid real de celdas (mismo criterio que .sf-product en Ventas: columnas con
              minmax() y separadores, no un bloque flex anidado) — mismos datos/orden/handlers. */}
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.quantityToRegister", "Cantidad a registrar")}
              </ZHFieldLabel>
              <ZhDecimalInput
                key={headerInputKey("qty", vm, l.packagingLevelId)}
                className="pdl-input pdl-input--qty"
                aria-label={t("purchases.lines.quantityToRegister", "Cantidad a registrar")}
                aria-invalid={!!fieldMessage(quantityFields)} aria-describedby={fieldMessage(quantityFields) ? errorId("quantity") : undefined}
                density="compact"
                precision="quantity"
                positiveOnly
                defaultValue={vm.inventory.baseQuantityValue}
                onBlur={(e) => {
                  // FASE 5 PURCHASE-UX — cantidad a registrar (unidad base), autoritativa: se
                  // guarda tal cual en quantityInBaseUom; la cantidad del documento se deriva.
                  // Blur sin edición real no toca la línea (evita round-trip por la escala visible).
                  const entered = Number(e.target.value) || 0;
                  if (isUnchangedAtPrecision(entered, vm.inventory.baseQuantityValue, quantityDecimals))
                    return;
                  const patch = applyRegisteredQuantity(l, vm.inventory.conversionFactorValue, entered);
                  if (!patch) return;
                  ctx.updateLine(l._key, "quantity", patch.quantity);
                  ctx.updateLine(l._key, "quantityInBaseUom", patch.quantityInBaseUom);
                  ctx.updateLine(l._key, "baseUnitPrice", patch.baseUnitPrice);
                }}
                disabled={lineLocked}
              />
              <PurchaseFieldError id={errorId("quantity")} message={fieldMessage(quantityFields)} />
            </div>
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.unitCostToRegister", "Costo unitario a registrar")}
              </ZHFieldLabel>
              <ZHInputGroup className="pdl-line__metric-value" prefix="$">
                <ZhDecimalInput
                  key={headerInputKey("cost", vm, l.packagingLevelId)}
                  className="pdl-input pdl-input--cost"
                  aria-label={t("purchases.lines.unitCostToRegister", "Costo unitario a registrar")}
                  aria-invalid={!!fieldMessage(costFields)} aria-describedby={fieldMessage(costFields) ? errorId("cost") : undefined}
                  density="compact"
                  // Escala del precio de compra: es la que el backend persiste para el precio base.
                  precision="purchaseUnitPrice"
                  positiveOnly
                  defaultValue={vm.inventory.baseUnitCostValue}
                  onBlur={(e) => {
                    // FASE 5 PURCHASE-UX — costo unitario a registrar (neto de descuento, con
                    // flete/gastos asignados), autoritativo: se despeja el precio base y el precio
                    // por presentación se deriva. Blur sin edición real no toca la línea.
                    const entered = Number(e.target.value) || 0;
                    if (isUnchangedAtPrecision(entered, vm.inventory.baseUnitCostValue, purchasePriceDecimals))
                      return;
                    const patch = applyRegisteredUnitCost(l, vm.inventory.conversionFactorValue, entered);
                    if (!patch) return;
                    ctx.updateLine(l._key, "unitPrice", patch.unitPrice);
                    ctx.updateLine(l._key, "baseUnitPrice", patch.baseUnitPrice);
                  }}
                  disabled={lineLocked}
                />
              </ZHInputGroup>
              <PurchaseFieldError id={errorId("cost")} message={fieldMessage(costFields)} />
            </div>
            {/* Subtotal de producto (cantidad × costo − descuento, sin impuestos): no es la base
                imponible tributaria — esa vive en el XML como "Base imp. IVA" (puede incluir ICE). */}
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.productSubtotal", "Subtotal producto")}
              </ZHFieldLabel>
              <div className="pdl-line__metric-value">
                <ZHMoneyValue value={sub} precision="money" />
              </div>
            </div>
            <div className="pdl-line__metric">
              <div className="pdl-line__tax">
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.vatShort", "IVA")} {vatPct}%: $
                </ZHFieldLabel>
                <div className="pdl-line__tax-amount" role="group" aria-label={t("purchases.lines.taxes", "Impuestos")}
                  aria-describedby={fieldMessage(taxFields) ? errorId("tax") : undefined}>
                  <ZHMoneyValue
                    value={vatAmt}
                    precision="tax"
                    currencySymbol=""
                    emphasis="strong"
                  />
                </div>
                <PurchaseFieldError id={errorId("tax")} message={fieldMessage(taxFields)} />
                {/* ICE/IRBPNR solo cuando aplican a la línea: un $0.00 fijo no aporta dato fiscal. */}
                {(iceAmt > 0 || !!l.iceCode) && (
                  <div>
                    <ZHDataValue variant="muted">
                      {t("purchases.lines.iceShort", "ICE")}:{" "}
                    </ZHDataValue>
                    <ZHMoneyValue
                      value={iceAmt}
                      precision="tax"
                      emphasis="muted"
                    />
                  </div>
                )}
                {irbpnrAmt > 0 && (
                  <div>
                    <ZHDataValue variant="muted">
                      IRBPNR:{" "}
                    </ZHDataValue>
                    <ZHMoneyValue
                      value={irbpnrAmt}
                      precision="tax"
                      emphasis="muted"
                    />
                  </div>
                )}
              </div>
            </div>
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.lineTotalWithTaxes", "Total línea (con impuestos)")}
              </ZHFieldLabel>
              <div className="pdl-line__total">
                <ZHMoneyValue
                  value={total}
                  precision="money"
                  emphasis="grand"
                />
              </div>
            </div>
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.marginShort", "Margen")}
              </ZHFieldLabel>
              <Badge
                variant={MARGIN_BADGE_VARIANT[vm.commercial.profitability.marginState]}
                size="md"
                label={marginBadgeLabel(vm.commercial.profitability, t)}
              />
            </div>
        </div>
      </div>

      {/* Franja operativa (no son estados de error), solo si existe: equivalencia de
          presentación y nota de costo. Las incidencias viven en la etiqueta anclada
          (ZH-DS-LINE-ISSUES-01). Cantidad/costo a registrar viven solo en la cabecera. */}
      {hasOperationalInfo && (
        <div className="pdl-line__status">
          {vm.registration.presentationDetail && (
            <ZHDataValue variant="muted">{vm.registration.presentationDetail}</ZHDataValue>
          )}
          {vm.registration.costNote && (
            <ZHDataValue variant="muted">{vm.registration.costNote}</ZHDataValue>
          )}
        </div>
      )}

      {/* Detalle expandible (botón "Detalle", ZH-DS-DISCLOSURE-01): 1. XML recibido — snapshot
          documental, única comparación explícita XML vs registro; en compra manual solo la nota.
          2. Presentación e inventario. 3. Información comercial. Cabecera, línea secundaria y
          avisos quedan siempre visibles, también colapsado. */}
      {!collapsed && (
      <div className="pdl-blocks" id={detailId}>
        <section className="pdl-block">
          <header className="pdl-block__header">
            <span
              className="material-symbols-outlined pdl-block__icon"
            >
              description
            </span>
            <h4 className="pdl-block__title zh-section-title">
              {t("purchases.lines.receivedProduct", "Producto recibido (XML)")}
              <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_XML_RECEIVED} />
            </h4>
          </header>
          {!vm.xml.hasOrigin && (
            <p className="pdl-block__hint">
              <ZHDataValue variant="muted">
                {t(
                  "purchases.lines.manualPurchaseHint",
                  "Compra manual — no proviene de recepción electrónica.",
                )}
              </ZHDataValue>
            </p>
          )}
          {vm.xml.hasOrigin && (<>
          {/* Cód. proveedor ya está en la cabecera cuando hay producto vinculado. */}
          <div className="pdl-ctx-col__badges">
            {!l.itemId && (
              <Badge
                variant="neutral"
                code
                label={`${t("purchases.lines.supplierCode", "Cód. proveedor")}: ${vm.xml.supplierCode}`}
              />
            )}
            <Badge
              variant={vm.xml.hasSupplierAuxCode ? "warning" : "neutral"}
              code
              label={`${t("purchases.lines.auxCode", "Cód. auxiliar")}: ${vm.xml.supplierAuxCode}`}
            />
          </div>
          <ZHDataValue variant="muted" className="pdl-ctx-col__desc">
            {vm.xml.description}
          </ZHDataValue>
          <div className="pdl-ctx-col__costs pdl-ctx-col__costs--grid">
            <ZHInfoRow
              label={<ZHFieldLabel size="sm">{t("purchases.lines.quantity", "Cantidad")}</ZHFieldLabel>}
              value={<ZHDataValue variant="numeric">{vm.xml.quantity}</ZHDataValue>}
            />
            <ZHInfoRow
              label={<ZHFieldLabel size="sm">{t("purchases.lines.price", "Precio")}</ZHFieldLabel>}
              value={<ZHDataValue variant="numeric">{vm.xml.unitPrice}</ZHDataValue>}
            />
            <ZHInfoRow
              label={<ZHFieldLabel size="sm">{t("purchases.lines.discount", "Descuento")}</ZHFieldLabel>}
              value={<ZHDataValue variant="numeric">{vm.xml.discount}</ZHDataValue>}
            />
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.xmlVatTaxableBase", "Base imp. IVA")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="numeric">{vm.xml.taxableBase}</ZHDataValue>}
            />
            <ZHInfoRow
              label={<ZHFieldLabel size="sm">IVA ({vm.xml.vatPercentage}%)</ZHFieldLabel>}
              value={<ZHDataValue variant="numeric">{vm.xml.taxValue}</ZHDataValue>}
            />
            {vm.xml.hasIce && (
              <ZHInfoRow
                label={<ZHFieldLabel size="sm">{t("purchases.lines.iceShort", "ICE")}</ZHFieldLabel>}
                value={<ZHDataValue variant="numeric">{vm.xml.iceValue}</ZHDataValue>}
              />
            )}
            {vm.xml.hasIrbpnr && (
              <ZHInfoRow
                label={<ZHFieldLabel size="sm">IRBPNR</ZHFieldLabel>}
                value={<ZHDataValue variant="numeric">{vm.xml.irbpnrValue}</ZHDataValue>}
              />
            )}
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.lineTotal", "Total línea")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="strong">{vm.xml.totalLine}</ZHDataValue>}
            />
          </div>
          {vm.xml.hasAdditionalFields && (
            <div className="pdl-additional-fields">
              <ZHFieldLabel size="sm">
                {t(
                  "purchases.lines.additionalFieldsTitle",
                  "Datos adicionales XML",
                )}
              </ZHFieldLabel>
              <div className="pdl-ctx-col__costs">
                {vm.xml.additionalFields.map((f, idx) => (
                  <ZHInfoRow
                    key={`${f.name}-${idx}`}
                    wide
                    label={<ZHFieldLabel size="sm">{f.name}</ZHFieldLabel>}
                    value={<ZHDataValue variant="numeric">{f.value}</ZHDataValue>}
                  />
                ))}
              </div>
            </div>
          )}
          </>)}
        </section>

        {/* 2. Con qué producto del sistema está relacionado — siempre visible, incluso sin producto aún. */}
        <section className="pdl-block">
          <header className="pdl-block__header">
            <span
              className="material-symbols-outlined pdl-block__icon"
            >
              badge
            </span>
            <h4 className="pdl-block__title zh-section-title">
              {t("purchases.lines.systemProduct", "Presentación e inventario")}
              <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_ITEM_MATCHING} />
            </h4>
            {vm.item.isLoading && (
              <span
                className="pdl-ctx-col__loading"
                title={t("purchases.lines.loadingContext", "Cargando contexto...")}
              >
                  <span
                  className="material-symbols-outlined pdl-ctx-col__loading-icon"
                >
                  hourglass_empty
                </span>
              </span>
            )}
            {vm.item.hasItem &&
              (vm.item.matchStatus || readiness?.status !== "READY") && (
                <span className="pdl-block__header-actions">
                  {vm.item.matchStatus ? (
                    <ItemMatchStatusBadge status={vm.item.matchStatus} />
                  ) : (
                    <Badge
                      variant="green"
                      label={t("purchases.lines.productLinked", "Producto vinculado")}
                    />
                  )}
                </span>
              )}
          </header>
          <div className="pdl-ctx-col__costs">
            {detailErrors.filter(issue => issue.path !== "packagingLevelId").map(issue => (
              <ZHField key={issue.path} density="compact"
                label={t(`purchases.validation.lineField.${issue.path}`, issue.path === "discountPct" ? "Descuento" : issue.path)}
                fieldError={issue.message}>{null}</ZHField>
            ))}
            {packagingLevels.length > 0 && (
              <div>
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.receivedPresentation", "Presentación recibida")}
                </ZHFieldLabel>
                <ZhSelect
                  density="compact"
                  value={l.packagingLevelId ?? ""}
                  aria-label={t("purchases.lines.receivedPresentation", "Presentación recibida")}
                  aria-invalid={!!fieldMessage(["packagingLevelId"])} aria-describedby={fieldMessage(["packagingLevelId"]) ? errorId("presentation") : undefined}
                  onChange={(e) => handlePackagingChange(e.target.value)}
                  disabled={lineLocked}
                >
                  <option value="">
                    {t("purchases.lines.baseUnit", "Unidad base")}
                  </option>
                  {packagingLevels.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
                </ZhSelect>
              </div>
            )}
            <PurchaseFieldError id={errorId("presentation")} message={fieldMessage(["packagingLevelId"])} />
            {vm.registration.savedInBaseUnitNote && <ZHDataValue variant="muted">{vm.registration.savedInBaseUnitNote}</ZHDataValue>}
            {/* FASE 5 PURCHASE-UX — producto sin presentaciones: flujo normal en unidad base, sin
                controles de presentación innecesarios. */}
            {vm.item.hasItem && packagingLevels.length === 0 && !vm.inventory.hasPresentation && (
              <ZHDataValue variant="muted">
                {t(
                  "purchases.lines.noPresentationsBaseUnit",
                  "Producto sin presentaciones: se registra en unidad base.",
                )}
              </ZHDataValue>
            )}
            {/* La equivalencia ya se lee en la línea secundaria cuando hay presentación elegida. */}
            {vm.inventory.hasPresentation &&
              vm.inventory.conversionFactorValue !== 1 &&
              !(vm.registration.show && vm.registration.presentationDetail) && (
              <ZHInfoRow
                wide
                label={
                  <ZHFieldLabel size="sm">
                    {t("purchases.lines.conversionFactor", "Factor de conversión")}
                  </ZHFieldLabel>
                }
                value={
                  <ZHDataValue variant="numeric">
                    {vm.inventory.equivalenceDetail || vm.inventory.conversionFactorLabel}
                  </ZHDataValue>
                }
              />
            )}
            {vm.item.hasItem && vm.xml.hasOrigin && packagingLevels.length > 0 && (
              <div className="pdl-cost-wide">
                <ZHBtn
                  type="button"
                  variant="primary"
                  size="xs"
                  title={t(
                    "purchases.lines.saveSupplierPresentationHint",
                    "Recordar esta presentación para las próximas facturas de este proveedor.",
                  )}
                  disabled={
                    lineLocked ||
                    ctx.matchingKey === l._key ||
                    !l.packagingLevelId
                  }
                  onClick={() => void ctx.handleSaveSupplierPresentation(l._key)}
                >
                  {ctx.matchingKey === l._key
                    ? t(
                        "purchases.lines.savingSupplierPresentation",
                        "Guardando presentación...",
                      )
                    : t("purchases.lines.saveSupplierPresentation", "Guardar presentación")}
                </ZHBtn>
              </div>
            )}
          </div>
        </section>

        {/* 3. Impacto para el negocio — el margen % vive solo en la cabecera; aquí lo que no está
            arriba (stock, costos de referencia, precio de venta, descuento máximo). */}
        <section className="pdl-block pdl-block--muted">
          <header className="pdl-block__header">
            <span
              className="material-symbols-outlined pdl-block__icon"
            >
              insights
            </span>
            <h4 className="pdl-block__title zh-section-title">
              {t("purchases.lines.commercialInfo", "Información comercial")}
            </h4>
          </header>
          {!vm.commercial.hasContext ? (
            <p className="pdl-block__hint">
              <ZHDataValue variant="muted">
                {t("purchases.lines.noCommercialData", "Sin datos comerciales para esta línea.")}
              </ZHDataValue>
            </p>
          ) : (<>
          <div className="pdl-ctx-col__stock">
            <div className="pdl-stock-item">
              <Badge
                variant={vm.commercial.stock.isCritical ? "error" : "success"}
                size="md"
                label={vm.commercial.stock.statusLabel}
              />
              <ZHDataValue variant="numeric">{vm.commercial.stock.current}</ZHDataValue>
            </div>
            <div className="pdl-stock-item">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.availableShort", "Disp.")}
              </ZHFieldLabel>
              <ZHDataValue variant="numeric">{vm.commercial.stock.available}</ZHDataValue>
            </div>
            <div className="pdl-stock-item">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.reserved", "Reserva")}
              </ZHFieldLabel>
              <ZHDataValue variant="numeric">{vm.commercial.stock.reserved}</ZHDataValue>
            </div>
          </div>
          <div className="pdl-ctx-col__costs pdl-ctx-col__costs--grid">
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.averageCost", "Costo promedio")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="numeric">{vm.commercial.costs.average}</ZHDataValue>}
            />
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.lastCost", "Último costo")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="numeric">{vm.commercial.costs.last}</ZHDataValue>}
            />
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.salePrice", "Precio de venta")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="numeric">{vm.commercial.profitability.pvp}</ZHDataValue>}
            />
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.maxDiscount", "Desc. máx.")}
                </ZHFieldLabel>
              }
              value={
                <ZHDataValue variant="numeric">
                  {vm.commercial.profitability.maxDiscountPercent}%
                </ZHDataValue>
              }
            />
          </div>
          </>)}
        </section>
      </div>
      )}
      </div>
    </ZHLineCard>
  );
}

/**
 * "Crear Item" para una línea de compra manual sin producto asignado — usa el ItemEditorModal
 * genérico directamente (sin wrapper), en modo Crear (sin `itemId`): esta línea no tiene
 * PurchaseReceptionLineId, por lo que no hay nada que vincular en el backend de Compras más allá
 * de seleccionar el ítem en el formulario, igual que hace ProductPicker.onSelect.
 */
function CreateItemLineAction({
  line: l,
  ctx,
  disabled,
  buttonClassName,
  label,
  onModalOpenChange,
  onAfterModalClose,
}: {
  line: PurchaseLineDraft;
  ctx: ReturnType<typeof usePurchasesPage>;
  disabled?: boolean;
  buttonClassName?: string;
  label?: string;
  onModalOpenChange?: (open: boolean) => void;
  onAfterModalClose?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const { t } = useI18n();

  const handleSaved = (item: ItemCreatedResult) => {
    setOpen(false);
    onModalOpenChange?.(false);
    onAfterModalClose?.();
    const patch = resolvePurchaseItemSelection(buildPurchaseItemProfile(item), {
      existingLine: l,
      overwriteVatCode: true,
    });
    ctx.updateLine(l._key, "itemId", patch.itemId ?? item.id);
    ctx.updateLine(
      l._key,
      "description",
      patch.description ?? `${item.sku} — ${item.shortName}`,
    );
    ctx.updateLine(l._key, "_readinessIssue", undefined);
    // El Item recién creado desde Compras trae su propio IVA de compra (ahora obligatorio en
    // ItemEditorModal) — se refleja de inmediato en la línea, sin depender de fetchItemContext
    // (que solo llena `context` para mostrar, no el campo `vatCode` del formulario).
    ctx.updateLine(l._key, "vatCode", patch.vatCode ?? "");
    const wh = l.warehouseId || ctx.formWatch.globalWarehouseId;
    void ctx.fetchItemContext(l._key, item.id, wh);
  };

  return (
    <>
      <ZHBtn
        variant="ghost"
        size="xs"
        type="button"
        className={buttonClassName}
        disabled={disabled}
        onClick={() => {
          onModalOpenChange?.(true);
          setOpen(true);
        }}
      >
        {label ?? t("purchases.lines.createProduct", "Crear producto")}
      </ZHBtn>
      <ItemEditorModal
        open={open}
        initialData={{
          name: l.description || undefined,
          barcode: l.xmlSupplierAuxCode ?? l.xmlSupplierCode ?? undefined,
          supplierCode: l.xmlSupplierCode ?? undefined,
          supplierId: ctx.formWatch.supplierId || undefined,
          source: l.xmlSupplierCode ? "PurchaseReception" : "Manual",
          purchaseContext:
            l.unitPrice > 0
              ? {
                  unitCost: l.unitPrice,
                  quantity: l.quantity,
                  discountPct: l.discountPct ?? undefined,
                  vatCode: l.vatCode || undefined,
                }
              : undefined,
        }}
        onClose={() => {
          setOpen(false);
          onModalOpenChange?.(false);
          onAfterModalClose?.();
        }}
        onSaved={handleSaved}
      />
    </>
  );
}

/**
 * "Actualizar Item" — visible en cualquier línea que ya tenga Item vinculado (manual o de
 * Recepción, da igual el origen). Reutiliza el mismo ItemEditorModal en modo Actualizar (pasando
 * `itemId`) y, al guardar, refresca el contexto de la línea con el mismo flujo ya existente
 * (`fetchItemContext`) — nunca un flujo paralelo de actualización visual.
 */
/**
 * FASE 3 — "Corregir relación" de una línea XML ya vinculada: producto/presentación correctos para
 * esta línea y para las próximas facturas del proveedor con el mismo código (backend atómico).
 */
function CorrectMatchAction({
  line: l,
  ctx,
  buttonClassName,
  onModalOpenChange,
  onAfterModalClose,
}: {
  line: PurchaseLineFormValues;
  ctx: ReturnType<typeof usePurchasesPage>;
  buttonClassName?: string;
  onModalOpenChange?: (open: boolean) => void;
  onAfterModalClose?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const { t } = useI18n();
  const close = () => {
    setOpen(false);
    onModalOpenChange?.(false);
    onAfterModalClose?.();
  };
  return (
    <>
      <ZHBtn
        variant="ghost"
        size="xs"
        type="button"
        className={buttonClassName}
        disabled={ctx.matchingKey === l._key}
        onClick={() => {
          onModalOpenChange?.(true);
          setOpen(true);
        }}
      >
        {t("purchases.lines.correctMatch", "Corregir relación")}
      </ZHBtn>
      <CorrectSupplierMatchModal
        open={open}
        supplierName={ctx.supplierProfile?.name ?? ""}
        supplierCode={l.xmlSupplierCode ?? ""}
        xmlDescription={l.xmlDescription ?? l.description}
        currentLabel={l.description}
        onClose={close}
        onSubmit={(itemId, packagingLevelId) =>
          ctx.handleCorrectMatch(l._key, itemId, packagingLevelId)
        }
      />
    </>
  );
}

function UpdateItemAction({
  line: l,
  ctx,
  buttonClassName,
  label,
  onModalOpenChange,
  onAfterModalClose,
}: {
  line: PurchaseLineFormValues;
  ctx: ReturnType<typeof usePurchasesPage>;
  buttonClassName?: string;
  label?: string;
  onModalOpenChange?: (open: boolean) => void;
  onAfterModalClose?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const { t } = useI18n();
  if (!l.itemId) return null;

  const handleSaved = (item: ItemCreatedResult) => {
    setOpen(false);
    onModalOpenChange?.(false);
    onAfterModalClose?.();
    const patch = resolvePurchaseItemSelection(buildPurchaseItemProfile(item), {
      existingLine: l,
      overwriteVatCode: true,
    });
    ctx.updateLine(
      l._key,
      "description",
      patch.description ?? `${item.sku} — ${item.shortName}`,
    );
    ctx.updateLine(l._key, "_readinessIssue", undefined);
    // Si el usuario cambió el IVA de compra al editar el Item desde esta línea, la línea debe
    // reflejar el valor vigente del Item de inmediato.
    ctx.updateLine(l._key, "vatCode", patch.vatCode ?? "");
    const wh = l.warehouseId || ctx.formWatch.globalWarehouseId;
    void ctx.fetchItemContext(l._key, item.id, wh);
  };

  return (
    <>
      <ZHBtn
        variant="ghost"
        size="xs"
        type="button"
        className={buttonClassName}
        onClick={() => {
          onModalOpenChange?.(true);
          setOpen(true);
        }}
      >
        {label ?? t("purchases.lines.updateProduct", "Actualizar producto")}
      </ZHBtn>
      <ItemEditorModal
        open={open}
        itemId={l.itemId}
        initialData={{
          purchaseContext:
            l.unitPrice > 0
              ? {
                  unitCost: l.unitPrice,
                  quantity: l.quantity,
                  discountPct: l.discountPct ?? undefined,
                  vatCode: l.vatCode || undefined,
                }
              : undefined,
        }}
        onClose={() => {
          setOpen(false);
          onModalOpenChange?.(false);
          onAfterModalClose?.();
        }}
        onSaved={handleSaved}
      />
    </>
  );
}

/**
 * "Crear producto" para una línea que sí tiene PurchaseReceptionLineId — PURCHASES-HARDENING-P0
 * (B9): abre el mismo "Resolver productos pendientes" (ResolvePendingProductsModal) limitado a esta
 * línea, así la creación individual usa exactamente el SSOT del resolver masivo (frontend
 * `buildResolveRequest` + backend ResolvePurchaseReceptionLines): barcode real de unidad → unidad,
 * de caja/pack → su presentación, sin barcode real de unidad → código del proveedor como Interno
 * provisional. Nunca una heurística aparte.
 */
function ReceptionCreateItemAction({
  line: l,
  ctx,
  disabled,
  buttonClassName,
  label,
  onAfterModalClose,
}: {
  line: PurchaseLineDraft;
  ctx: ReturnType<typeof usePurchasesPage>;
  disabled?: boolean;
  buttonClassName?: string;
  label?: string;
  onAfterModalClose?: () => void;
}) {
  const { t } = useI18n();
  return (
    <ZHBtn
      variant="ghost"
      size="xs"
      type="button"
      className={buttonClassName}
      disabled={disabled}
      onClick={() => {
        onAfterModalClose?.();
        ctx.openPendingProducts(l.purchaseReceptionLineId as string);
      }}
    >
      {label ?? t("purchases.lines.createProduct", "Crear producto")}
    </ZHBtn>
  );
}

function PaymentScheduleSection({
  ctx,
}: {
  ctx: ReturnType<typeof usePurchasesPage>;
}) {
  const { t } = useI18n();
  const moneyDecimals = usePrecisionDecimals("money"); // texto compuesto (06)
  return (
    <div className="pf-card" id="purchase-schedule" tabIndex={-1}>
      <div className="pf-card__header">
        <h4 className="pf-card__title">
          <span className="material-symbols-outlined pf-card__title-icon">
            calendar_month
          </span>
          {t("purchases.schedule.title", "Plazos de pago")}
          <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_PAYMENT_SCHEDULE} />
          {ctx.hasPersistedSchedule && (
            <Badge
              variant="success"
              className="pf-badge--offset"
              label={
                <>
                  <span className="material-symbols-outlined pf-badge__icon">
                    lock
                  </span>{" "}
                  {t("purchases.schedule.confirmed", "Confirmado")}
                </>
              }
            />
          )}
          {!ctx.hasPersistedSchedule && ctx.ptLoaded && (
            <Badge
              variant="warning"
              className="pf-badge--offset"
              label={t("purchases.schedule.preview", "Preview")}
            />
          )}
        </h4>
        {ctx.isDraft && !ctx.hasPersistedSchedule && (
          <div className="pf-schedule-actions">
            <ZHBtn
              type="button"
              variant="primary"
              size="xs"
              onClick={ctx.regenerateSchedule}
              disabled={ctx.saving || !ctx.formWatch.issueDate}
            >
              <span
                className="material-symbols-outlined pf-schedule-action-icon"
              >
                autorenew
              </span>
              {t("purchases.schedule.regenerate", "Regenerar")}
            </ZHBtn>
            <ZHBtn
              type="button"
              variant="secondary"
              size="xs"
              onClick={ctx.addInstallment}
              disabled={ctx.saving || !ctx.formWatch.issueDate}
            >
              <span
                className="material-symbols-outlined pf-schedule-action-icon"
              >
                add
              </span>
              {t("purchases.schedule.installment", "Cuota")}
            </ZHBtn>
          </div>
        )}
      </div>
      <div className="pf-card__body">
        {ctx.isDraft && !ctx.hasPersistedSchedule && (
          <div className="pf-schedule-setup">
            <ZHField
              density="compact"
              label={t("purchases.schedule.installmentCount", "Nro. cuotas")}
              className="pf-schedule-field--installments"
            >
              <ZhNumberInput
                positiveOnly
                value={String(ctx.ptInstallments)}
                onChange={(e) =>
                  ctx.setPtInstallments(
                    Math.min(60, Math.max(1, Number(e.target.value) || 1)),
                  )
                }
                disabled={ctx.fieldDisabled}
              />
            </ZHField>
            <ZHField
              density="compact"
              label={t("purchases.schedule.daysBetween", "Días entre cuotas")}
              className="pf-schedule-field--days-between"
            >
              <ZhNumberInput
                positiveOnly
                value={String(ctx.ptDaysBetween)}
                onChange={(e) =>
                  ctx.setPtDaysBetween(Math.max(0, Number(e.target.value) || 0))
                }
                disabled={ctx.fieldDisabled}
              />
            </ZHField>
            <div className="pf-schedule-total">
              {t("purchases.schedule.totalTerm", "Total plazo")}:{" "}
              <strong>
                {t("purchases.schedule.daysValue", {
                  days: ctx.ptInstallments * ctx.ptDaysBetween,
                })}
              </strong>
            </div>
          </div>
        )}

        {ctx.ptMismatch && ctx.ptRows.length > 0 && (
          <ZHPageNotice
            variant="warning"
            message={t("purchases.schedule.mismatch", {
              amount: formatMoney(
                roundToTotalAmount(ctx.localTotal - ctx.ptRowsSum),
                moneyDecimals,
              ),
            })}
          />
        )}

        {ctx.hasPersistedSchedule && (
          <table className="table table--compact table--neutral">
            <thead>
              <tr>
                <th className="pf-schedule-col--number">#</th>
                <th>{t("purchases.schedule.dueDate", "Vencimiento")}</th>
                <th className="zh-text-align-right">{t("purchases.schedule.amount", "Monto")}</th>
                <th>{t("purchases.schedule.notes", "Notas")}</th>
              </tr>
            </thead>
            <tbody>
              {ctx.editing!.paymentSchedules.map((s) => (
                <tr key={s.id}>
                  <td className="zh-text-align-center pf-schedule-installment-number">
                    {s.installmentNumber}
                  </td>
                  <td>{formatDate(s.dueDate)}</td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue value={s.amount} precision="money" />
                  </td>
                  <td className="pf-schedule-notes">
                    {s.notes || "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {!ctx.hasPersistedSchedule && ctx.ptRows.length > 0 && (
          <>
            <table className="pf-table">
              <thead>
                <tr>
                  <th className="pf-schedule-col--number">#</th>
                  <th>{t("purchases.schedule.dueDate", "Vencimiento")}</th>
                  <th className="zh-text-align-right pf-schedule-col--amount">
                    {t("purchases.schedule.amountCurrency", "Monto ($)")}
                  </th>
                  <th>{t("purchases.schedule.notes", "Notas")}</th>
                  <th>{t("purchases.schedule.status", "Estado")}</th>
                  <th className="zh-text-align-center pf-schedule-col--actions"></th>
                </tr>
              </thead>
              <tbody>
                {ctx.ptRows.map((r, idx) => (
                  <tr key={idx}>
                    <td className="zh-text-align-center pf-schedule-installment-number pf-schedule-installment-number--muted">
                      {r.number}
                    </td>
                    <td>
                      <ZhDateInput
                        density="compact"
                        className="pf-schedule-input--date"
                        value={r.dueDate}
                        onChange={(e) =>
                          ctx.updateScheduleRow(idx, "dueDate", e.target.value)
                        }
                        disabled={ctx.saving || !ctx.isDraft}
                      />
                    </td>
                    <td>
                      <ZhDecimalInput
                        precision="money"
                        positiveOnly
                        density="compact"
                        className="pf-schedule-input--amount"
                        defaultValue={r.amount}
                        onBlur={(e) =>
                          ctx.updateScheduleRow(
                            idx,
                            "amount",
                            Number(e.target.value) || 0,
                          )
                        }
                        disabled={ctx.saving || !ctx.isDraft}
                      />
                    </td>
                    <td>
                      <ZhTextInput
                        density="compact"
                        className="pf-schedule-input--notes"
                        placeholder={t("purchases.schedule.notePlaceholder", "Observación...")}
                        value={r.notes}
                        onChange={(e) =>
                          ctx.updateScheduleRow(idx, "notes", e.target.value)
                        }
                        maxLength={500}
                        disabled={ctx.saving || !ctx.isDraft}
                      />
                    </td>
                    <td>
                      <Badge
                        variant="warning"
                        label={t("purchases.schedule.pending", "Pendiente")}
                      />
                    </td>
                    <td className="zh-text-align-center">
                      {ctx.isDraft && ctx.ptRows.length > 1 && (
                        <ZHIconButton
                          icon="close"
                          variant="danger"
                          title={t("purchases.schedule.deleteInstallment", "Eliminar cuota")}
                          onClick={() => ctx.removeInstallment(idx)}
                          disabled={ctx.fieldDisabled}
                        />
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="pf-schedule-footer">
              <span className="pf-schedule-footer__count">
                {t("purchases.schedule.installmentCountValue", {
                  count: ctx.ptRows.length,
                })}
              </span>
              <span>
                {t("purchases.schedule.installmentsTotal", "Total cuotas")}:{" "}
                <strong
                  className={`pf-schedule-footer__amount ${ctx.ptMismatch ? "pf-schedule-footer__amount--error" : "pf-schedule-footer__amount--default"}`}
                >
                  <ZHMoneyValue value={ctx.ptRowsSum} precision="money" />
                </strong>
                {" / "}
                {t("purchases.schedule.purchaseTotal", "Total compra")}:{" "}
                <strong className="pf-schedule-footer__purchase-total">
                  <ZHMoneyValue value={ctx.localTotal} precision="money" />
                </strong>
              </span>
            </div>
          </>
        )}

        {!ctx.hasPersistedSchedule && ctx.ptRows.length === 0 && (
          <div className="pf-schedule-empty">
            <span
              className="material-symbols-outlined pf-schedule-empty__icon"
            >
              event_busy
            </span>
            {!ctx.ptLoaded
              ? t(
                  "purchases.schedule.emptySelectSupplier",
                  "Seleccione un proveedor para cargar la condición de pago",
                )
              : !ctx.formWatch.issueDate
                ? t(
                    "purchases.schedule.emptyIssueDate",
                    "Ingrese la fecha de emisión para calcular vencimientos",
                  )
                : t(
                    "purchases.schedule.emptyConfigure",
                    "Configure cuotas arriba para generar el cronograma",
                  )}
          </div>
        )}
      </div>
    </div>
  );
}

function RetentionSection({
  ctx,
}: {
  ctx: ReturnType<typeof usePurchasesPage>;
}) {
  const { t } = useI18n();
  const isDraft = ctx.editing?.status === "Draft";
  return (
    <div className="pf-card pf-retention" id="purchase-retention" tabIndex={-1}>
      <div className="pf-card__header">
        <h4 className="pf-card__title">
          <span className="material-symbols-outlined pf-card__title-icon">
            receipt
          </span>{" "}
          {t("purchases.retention.title", "Retenciones")}
          <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_RETENTIONS} />
        </h4>
        <div className="pf-retention-actions">
          {ctx.retention && ctx.retention.status === "Issued" && (
            <>
              <ZHBtn
                type="button"
                variant="secondary"
                size="sm"
                onClick={() => void ctx.handleViewRetentionXml()}
                disabled={ctx.electronicPending}
                title={t("purchases.retention.viewXml", "Ver XML")}
              >
                <span className="material-symbols-outlined pf-retention-action-icon">
                  code
                </span>
                {t("purchases.retention.viewXml", "XML")}
              </ZHBtn>
              <ZHBtn
                type="button"
                variant="secondary"
                size="sm"
                onClick={() => void ctx.handleViewRetentionRidePdf()}
                disabled={ctx.electronicPending}
                title={t("purchases.retention.viewRide", "Ver RIDE (PDF)")}
              >
                <span className="material-symbols-outlined pf-retention-action-icon">
                  picture_as_pdf
                </span>
                {t("purchases.retention.viewRide", "RIDE")}
              </ZHBtn>
            </>
          )}
        </div>
      </div>
      <div className="pf-card__body">
        {isDraft && <PurchaseRetentionDraft ctx={ctx} />}
        {!isDraft && !ctx.retention && (
          <ZHDataValue variant="muted">{t("purchases.retention.noneIssued")}</ZHDataValue>
        )}
        {ctx.retention && (
          <>
            <div className="pf-retention__issued-meta">
              <span className="pf-retention__issued-item">
                {t("purchases.retention.number", "Número")}:{" "}
                <strong>{ctx.retention.retentionNumber}</strong>
              </span>
              <span className="pf-retention__issued-item">
                {t("purchases.retention.status", "Estado")}:{" "}
                <Badge
                  variant={
                    ctx.retention.status === "Issued" ? "success" : "error"
                  }
                  label={ctx.retention.status}
                />
              </span>
              <span className="pf-retention__issued-item">
                {t("purchases.retention.date", "Fecha")}:{" "}
                <strong>{formatDate(ctx.retention.issueDate)}</strong>
              </span>
              <span className="pf-retention__issued-item">
                {t("retentions.electronicStatus.label", "Estado electrónico")}:{" "}
                <RetentionElectronicStatusBadge status={ctx.retention.electronicStatus} />
              </span>
            </div>
            {ctx.retention.annulment ? (
              <RetentionAnnulmentPanel
                annulment={ctx.retention.annulment}
                origin="purchase"
                canOperate={ctx.canOperateRetentionAnnulment}
                onChanged={() => void ctx.handleRetentionAnnulmentChanged()}
              />
            ) : null}
            <table className="table table--compact table--neutral">
              <thead>
                <tr>
                  <th>{t("purchases.retention.type", "Tipo")}</th>
                  <th>{t("purchases.retention.code", "Código")}</th>
                  <th>{t("purchases.retention.description", "Descripción")}</th>
                  <th className="zh-text-align-right">{t("purchases.retention.base", "Base")}</th>
                  <th className="zh-text-align-right">%</th>
                  <th className="zh-text-align-right">{t("purchases.retention.withheld", "Retenido")}</th>
                </tr>
              </thead>
              <tbody>
                {ctx.retention.lines.map((l) => (
                  <tr key={l.id}>
                    <td>
                      <Badge
                        variant={l.taxType === "Vat" ? "info" : "warning"}
                        label={l.taxType === "Vat" ? "IVA" : "RENTA"}
                      />
                    </td>
                    <td className="zh-code-value">
                      {l.retentionCode}
                    </td>
                    <td>{l.retentionCodeDescription}</td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue value={l.baseAmount} precision="money" />
                    </td>
                    <td className="zh-table-cell--num">{l.retentionRate}%</td>
                    <td className="zh-table-cell--num pf-retention-amount">
                      <ZHMoneyValue value={l.retainedAmount} precision="money" />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="pf-retention__issued-totals">
              <TotalMiniCard
                label={t("purchases.retention.vat", "Ret. IVA")}
                value={ctx.retention.totalRetainedVat}
                color="var(--color-primary)"
              />
              <TotalMiniCard
                label={t("purchases.retention.income", "Ret. renta")}
                value={ctx.retention.totalRetainedIncome}
                color="var(--color-warning)"
              />
              <TotalMiniCard
                label={t("purchases.retention.totalWithheld", "TOTAL RETENIDO")}
                value={ctx.retention.totalRetained}
                color="var(--color-error)"
                highlight
              />
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function TotalMiniCard({
  label,
  value,
  color,
  highlight,
}: {
  label: string;
  value: number;
  color?: string;
  highlight?: boolean;
}) {
  return (
    <div className={`pf-total-mini-card ${highlight ? "pf-total-mini-card--highlight" : "pf-total-mini-card--default"}`}>
      <div className="pf-total-mini-card__label">
        {label}
      </div>
      <div
        className={`pf-total-mini-card__value ${highlight ? "pf-total-mini-card__value--highlight" : "pf-total-mini-card__value--default"}`}
        data-tone={color === "var(--color-error)" ? "error" : "default"}
      >
        <ZHMoneyValue value={value} precision="money" />
      </div>
    </div>
  );
}
