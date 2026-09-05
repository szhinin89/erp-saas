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
import { SupplierPicker } from "../components/SupplierPicker";
import { DistributeCostModal } from "../components/DistributeCostModal";
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
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatMoney, formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import {
  ZHConfirmModal,
  ZHPromptModal,
} from "../../../components/zh/ZHConfirmModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { ZHCompactNotice } from "../../../components/zh/notices";
import { buildNotice, type NoticeSeverity } from "../../../notices";
import {
  lineNet,
  calcLineTax,
  calcVatBreakdown,
  roundToTotalAmount,
  buildCostDistributionInputFromPersistedLines,
  buildCostDistributionInputFromFormLines,
} from "../utils/purchaseCalc";
import { buildWithholdingIssueMessage } from "../utils/withholdingMessages";
import { usePurchasesPage, type Tab } from "../hooks/usePurchasesPage";
import type { PurchaseListItemDto } from "../api/purchaseService";
import { useI18n } from "../../../i18n/i18n";
import {
  ItemMatchStatusBadge,
  useViewMatchedItem,
} from "../components/ItemMatchStatusBadge";
import { CreateItemFromReceptionLineModal } from "../components/CreateItemFromReceptionLineModal";
import type {
  PurchaseReceptionLineMatch,
  ItemMatchStatus,
} from "../api/purchaseReceptionService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import {
  buildPurchaseLinePresentation,
  computeUnitPriceFromBaseUnitCost,
  headerInputKey,
  type LineStatusTone,
} from "../utils/purchaseLinePresentation";
import {
  buildPurchaseItemProfile,
  resolvePurchaseItemSelection,
} from "../utils/purchaseItemProfile";
import type { PurchaseLineReadinessAction } from "../utils/purchaseLineReadiness";
import "../../../styles/shared/items-catalog.css";
import "../../../styles/shared/erp-form-core.css";
import "../styles/purchases-invoice.css";

export function PurchasesPage() {
  const ctx = usePurchasesPage();
  const navigate = useNavigate();
  const { t } = useI18n();
  const [searchParams] = useSearchParams();
  const openedFromParam = useRef(false);
  const xmlConfirmChecklist = buildXmlConfirmChecklist(
    ctx.lines,
    ctx.editing?.lines ?? [],
    ctx.vatRatesMap,
    ctx.iceRatesMap,
    t,
  );
  const statusLabel = (status: string) =>
    t(`purchases.status.${status.toLowerCase()}`, status);

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
        ? t("purchases.tabs.edit", "Editar compra")
        : t("purchases.tabs.new", "Nueva compra"),
      icon: ctx.editing ? "edit" : "add_box",
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
    { key: "date", header: t("purchases.list.date", "Fecha"), render: (inv) => formatDate(inv.issueDate) },
    { key: "lines", header: t("purchases.list.lines", "Líneas"), align: "center", render: (inv) => inv.lineCount },
    {
      key: "status",
      header: t("purchases.list.status", "Estado"),
      render: (inv) => (
        <Badge
          variant={inv.status === "Draft" ? "warning" : inv.status === "Confirmed" ? "success" : "error"}
          label={statusLabel(inv.status)}
        />
      ),
    },
    {
      key: "actions",
      header: t("purchases.list.actions", "Acciones"),
      align: "center",
      render: (inv) => (
        <>
          <ZHIconButton
            icon="edit"
            variant="ghost"
            title={`${t("common.edit", "Editar")} factura ${inv.invoiceNumber}`}
            ariaLabel={`${t("common.edit", "Editar")} factura ${inv.invoiceNumber}`}
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
      subtitle={t("purchases.page.subtitle", "Gestión de compras en borrador.")}
    >
      <ZHTabBar
        tabs={tabs}
        activeTab={ctx.tab}
        onChange={(id) => {
          if (id === "listado") ctx.resetForm();
          ctx.setTab(id);
        }}
      />

      {/* ══════════════════════════════════════════════════════════ LISTADO */}
      {ctx.tab === "listado" && (
        <div className="prd-section">
          <div className="pg-table-controls zh-mb-16">
            <ZhTextInput
              placeholder={t(
                "purchases.list.searchPlaceholder",
                "Buscar por número de factura...",
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
              <option value="Cancelled">{t("purchases.status.cancelled", "Anulada")}</option>
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
          {/* Error/Validation Banners */}
          {ctx.saveError && (
            <ZHPageNotice
              variant="error"
              message={ctx.saveError}
              detail={
                ctx.saveErrorDetails.length > 0
                  ? ctx.saveErrorDetails.map((err) => `- ${err}`).join("\n")
                  : null
              }
              className={
                ctx.saveErrorDetails.length > 0
                  ? "pf-validation-summary"
                  : undefined
              }
            />
          )}
          {ctx.receptionProcessingNotice && (
            <ZHPageNotice
              variant="warning"
              message={ctx.receptionProcessingNotice}
            />
          )}
          {ctx.isDuplicateAccessKeyBlocking && (
            <div>
              <ZHPageNotice
                variant="error"
                message={ctx.duplicateAccessKeyTitle}
                detail={ctx.duplicateAccessKeyDetail}
              />
              {ctx.duplicateAccessKey?.purchaseId && (
                <ZHBtn
                  type="button"
                  variant="secondary"
                  onClick={() => {
                    const purchaseId = ctx.duplicateAccessKey?.purchaseId;
                    if (!purchaseId) return;
                    navigate(`/purchases?invoiceId=${purchaseId}`);
                    void ctx.loadForEdit(purchaseId);
                  }}
                >
                  <span className="material-symbols-outlined zh-icon-md">
                    open_in_new
                  </span>
                  {t(
                    "purchases.duplicate.viewExisting",
                    "Ver compra existente",
                  )}
                </ZHBtn>
              )}
            </div>
          )}
          {ctx.isSupplierInactiveBlocking && (
            <ZHPageNotice
              variant="error"
              message={ctx.supplierInactiveMessage}
              detail={ctx.supplierInactiveDetail}
            />
          )}
          {ctx.hasLineReadinessBlockers &&
            !ctx.saveError &&
            !ctx.isDuplicateAccessKeyBlocking &&
            !ctx.isSupplierInactiveBlocking && (
              <ZHPageNotice
                variant="warning"
                message={ctx.lineReadinessBlockedTitle}
                detail={ctx.lineReadinessBlockerDetails.join("\n")}
              />
            )}
          {ctx.errors.supplierId && (
            <ZHPageNotice
              variant="error"
              message={ctx.errors.supplierId.message ?? ""}
            />
          )}
          {ctx.errors.lines && (
            <ZHPageNotice
              variant="error"
              message={
                ctx.errors.lines.message ?? ctx.errors.lines.root?.message ?? ""
              }
            />
          )}
          {ctx.sriDocTypesUnavailable && (
            <ZHPageNotice
              variant="error"
              message={t(
                "purchases.validation.sriDocTypesUnavailableTitle",
                "No se puede guardar la compra.",
              )}
              detail={t(
                "purchases.validation.sriDocTypesUnavailableDetail",
                "El catálogo SRI de tipos de documento no está disponible. Recargue la página o inténtelo nuevamente.",
              )}
            />
          )}

          {ctx.readOnly && (
            <ZHPageNotice
              variant={
                ctx.editing?.status === "Cancelled" ? "error" : "success"
              }
              message={
                ctx.editing?.status === "Cancelled"
                  ? t("purchases.notices.cancelledReadOnly", {
                      date: formatDate(ctx.editing.cancelledAt),
                      reason:
                        ctx.editing.cancelReason ??
                        t("purchases.notices.noReason", "Sin motivo"),
                    })
                  : t(
                      "purchases.notices.confirmedReadOnly",
                      "Compra confirmada — solo lectura.",
                    )
              }
            />
          )}

          {/* ── SECTION 1: Header Cards ── */}
          <div className="pf-header-cards-row">
            {/* Datos del Proveedor */}
            <div className="pf-mini-card">
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
                  required
                >
                  <SupplierPicker
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
                    {!ctx.supplierProfile.isActive && (
                      <Badge
                        variant="warning"
                        label={t(
                          "purchases.supplier.inactiveBadge",
                          "Proveedor inactivo",
                        )}
                      />
                    )}
                  </div>
                )}
              </div>
            </div>

            {/* Datos del Documento */}
            <div className="pf-mini-card">
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
                  fieldError={ctx.errors.docTypeCode?.message}
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
                        {ctx.globalResults.length === 0 ? (
                          <div className="pdl-search__empty">
                            {t("purchases.lines.noResults", {
                              query: ctx.globalQuery,
                            })}
                          </div>
                        ) : (
                          ctx.globalResults.map((item, i) => (
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
                                        decimals={getDecimalConfig().salesUnitPrice}
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
                          ))
                        )}
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
                    title={ctx.distributeCostDisabledReason ?? undefined}
                  >
                    <span className="material-symbols-outlined zh-icon-md">
                      local_shipping
                    </span>
                    {t("purchases.distributeCost.trigger", "Distribuir flete/gasto")}
                  </ZHBtn>
                </div>

                {/* Line Cards */}
                <div className="pdl-lines">
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
                {ctx.editing && ctx.editing.status === "Confirmed" && (
                  <RetentionSection ctx={ctx} />
                )}
              </div>
              <SummaryPanel ctx={ctx} />
            </div>
          </div>

          {ctx.editing &&
            ctx.editing.status === "Draft" &&
            ctx.pendingReceptionItems.length > 0 && (
              <ZHPageNotice
                variant="warning"
                message={t(
                  "purchases.validation.pendingReceptionItemsTitle",
                  "No se puede confirmar la compra: productos pendientes de vinculación",
                )}
                detail={ctx.pendingReceptionItems.join(", ")}
              />
            )}

          {/* Footer Actions */}
          <div className="pf-footer">
            <ZHBtn
              type="button"
              variant="secondary"
              onClick={() => {
                ctx.resetForm();
                ctx.setTab("listado");
              }}
            >
              <span className="material-symbols-outlined zh-icon-md">
                arrow_back
              </span>
              {t("common.cancel", "Cancelar")}
            </ZHBtn>
            <ZHBtn
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
                ctx.hasLineReadinessBlockers ||
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
            {ctx.editing && ctx.editing.status === "Draft" && (
              <ZHBtn
                type="button"
                variant="primary"
                onClick={() => ctx.setModalConfirm(true)}
                disabled={
                  ctx.fieldDisabled || ctx.pendingReceptionItems.length > 0
                }
                title={
                  ctx.pendingReceptionItems.length > 0
                    ? t(
                        "purchases.validation.resolvePendingItemsBeforeConfirm",
                        "Resuelva los productos pendientes de vinculación antes de confirmar.",
                      )
                    : undefined
                }
              >
                <span className="material-symbols-outlined zh-icon-md">
                  check_circle
                </span>
                {t("purchases.actions.confirm", "Confirmar compra")}
              </ZHBtn>
            )}
            {ctx.editing && ctx.editing.status === "Confirmed" && (
              <ZHBtn
                type="button"
                variant="secondary"
                onClick={() =>
                  navigate(`/purchases/returns/new?invoiceId=${ctx.editing!.id}`)
                }
              >
                <span className="material-symbols-outlined zh-icon-md">
                  assignment_return
                </span>
                {t("purchases.actions.return", "Devolución")}
              </ZHBtn>
            )}
            {ctx.editing && ctx.editing.status === "Confirmed" && (
              // Acción complementaria (FLOW-READY-02C.4) — nunca reemplaza "Devolución": la
              // pantalla centralizada de Nota de Crédito (FLOW-READY-02C.3) delega ahí mismo a
              // /purchases/returns/new cuando el usuario elige Devolución. Mismo patrón de
              // navegación que el botón "Devolución" de arriba (navigate en la misma pestaña,
              // no window.open): esta pantalla de edición de compra ya navega in-place para sus
              // acciones documentales (Devolución, Anular Compra), a diferencia de la bandeja de
              // Recepción que sí abre pestañas nuevas para mantenerse abierta como lista.
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
                disabled={ctx.fieldDisabled}
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
      <DistributeCostModal
        open={ctx.modalDistributeCost}
        lines={
          ctx.editing
            ? buildCostDistributionInputFromPersistedLines(ctx.editing.lines)
            : buildCostDistributionInputFromFormLines(ctx.lines)
        }
        totalFreight={ctx.editing ? ctx.editing.totalFreight : ctx.formWatch.freightCost}
        totalOtherCosts={
          ctx.editing ? ctx.editing.totalOtherCosts : ctx.formWatch.otherCosts
        }
        grandTotal={ctx.localTotal}
        onCancel={() => ctx.setModalDistributeCost(false)}
        onApply={(costType, amount, includedIds) =>
          ctx.editing
            ? ctx.handleDistributeCost(costType, amount, includedIds)
            : ctx.handleDistributeCostToForm(costType, amount, includedIds)
        }
      />

      <ZHConfirmModal
        open={ctx.modalConfirm}
        variant="warning"
        title={t("purchases.confirm.title", "Confirmar compra")}
        message={<XmlConfirmChecklist summary={xmlConfirmChecklist} />}
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
            ? t("purchases.cancel.message", {
                invoiceNumber: ctx.editing.invoiceNumber,
              })
            : ""
        }
        label={t("purchases.cancel.reasonLabel", "Motivo de anulación")}
        placeholder={t("purchases.cancel.reasonPlaceholder", "Ingrese el motivo...")}
        confirmLabel={t("purchases.cancel.confirm", "Anular")}
        onCancel={() => ctx.setModalCancelReason(false)}
        onConfirm={ctx.handleCancel}
      />

      <ZHPromptModal
        open={ctx.modalWhIssue}
        title={t("purchases.withholding.issueTitle", "Emitir retención")}
        variant="warning"
        message={
          ctx.editing
            ? buildWithholdingIssueMessage(
                ctx.editing.invoiceNumber,
                ctx.editing.supplierName,
                ctx.whPreview?.totalRetained,
              )
            : undefined
        }
        label={t("purchases.withholding.emissionPointId", "ID del punto de emisión")}
        placeholder={t("purchases.withholding.emissionPointPlaceholder", "ID del punto de emisión")}
        confirmLabel={t("purchases.withholding.issue", "Emitir")}
        onCancel={() => ctx.setModalWhIssue(false)}
        onConfirm={ctx.handleIssueRetention}
      />

      {/* PURCHASES-RETENTIONS-CANCEL-05D — modal crítico: motivo obligatorio, mismo patrón que
          "Anular compra" (modalCancelReason) arriba. */}
      <ZHPromptModal
        open={ctx.modalRetentionCancel}
        variant="danger"
        title={t("purchases.retention.cancelTitle", "Anular retención")}
        message={t(
          "purchases.retention.cancelMessage",
          "Esta acción reversará la afectación a la cuenta por pagar del proveedor y el asiento contable de la retención. Esta operación no se puede deshacer.",
        )}
        label={t("purchases.cancel.reasonLabel", "Motivo de anulación")}
        placeholder={t("purchases.cancel.reasonPlaceholder", "Ingrese el motivo...")}
        confirmLabel={t("purchases.cancel.confirm", "Anular")}
        onCancel={() => ctx.setModalRetentionCancel(false)}
        onConfirm={ctx.handleCancelRetention}
      />
    </ErpPageTemplate>
  );
}

// ── Internal Components ────────────────────────────────────────────────

const STATUS_TONE_VARIANT: Record<LineStatusTone, BadgeVariant> = {
  success: "green",
  warning: "orange",
  danger: "red",
};

/** readiness.tone ("success"|"warning"|"danger") ya usa el mismo vocabulario que NoticeSeverity
 * salvo "info" (sin equivalente en readiness) — conversión trivial de tipo, sin lógica. */
function toneToSeverity(tone: "success" | "warning" | "danger"): NoticeSeverity {
  return tone;
}

type PurchaseLineDraft = PurchaseLineFormValues;
type TFunction = ReturnType<typeof useI18n>["t"];
type ChecklistTone = "success" | "warning" | "danger" | "neutral";
type XmlConfirmChecklistSummary = {
  hasXmlPurchase: boolean;
  blockerCount: number;
  items: {
    key: string;
    label: string;
    detail: string;
    tone: ChecklistTone;
    icon: string;
  }[];
};

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

function buildXmlConfirmChecklist(
  lines: PurchaseLineDraft[],
  persistedLines: NonNullable<ReturnType<typeof usePurchasesPage>["editing"]>["lines"],
  vatRatesMap: Record<string, number>,
  iceRatesMap: Record<string, number>,
  t: TFunction,
): XmlConfirmChecklistSummary {
  const xmlLines = lines.filter((line) => line.purchaseReceptionLineId);
  const persistedByReceptionLine = new Map(
    persistedLines
      .filter((line) => line.purchaseReceptionLineId)
      .map((line) => [line.purchaseReceptionLineId, line]),
  );
  const linkedItems = xmlLines.filter((line) => !!line.itemId).length;
  const missingItems = xmlLines.length - linkedItems;
  const linesWithoutPresentation = xmlLines.filter(
    (line) => line.itemId && !line.packagingLevelId,
  );
  const stockLinesWithoutPresentation = linesWithoutPresentation.filter(
    (line) => line.context?.tracksStock === true,
  );
  const unknownPresentationRisk = linesWithoutPresentation.filter(
    (line) => line.context?.tracksStock === undefined,
  );
  const taxesRecognized = xmlLines.filter((line) => {
    const persisted = line.purchaseReceptionLineId
      ? persistedByReceptionLine.get(line.purchaseReceptionLineId)
      : undefined;
    if (persisted?.taxes?.length) {
      return persisted.taxes.every((tax) => tax.taxRateCode && tax.taxName);
    }
    return !!line.vatCode || !!line.xmlTaxCode || line.xmlTaxValue !== undefined;
  }).length;
  const xmlTotalsAvailable =
    xmlLines.length > 0 &&
    xmlLines.every((line) => typeof line.xmlTotalLine === "number");
  const totalDifference = xmlTotalsAvailable
    ? roundToTotalAmount(
        xmlLines.reduce(
          (sum, line) =>
            sum +
            lineNet(line) +
            calcLineTax(line, vatRatesMap, iceRatesMap).vat +
            calcLineTax(line, vatRatesMap, iceRatesMap).ice -
            (line.xmlTotalLine ?? 0),
          0,
        ),
      )
    : null;

  if (xmlLines.length === 0) {
    return {
      hasXmlPurchase: false,
      blockerCount: 0,
      items: [
        {
          key: "manual",
          label: t("purchases.confirm.checklist.manualTitle"),
          detail: t("purchases.confirm.checklist.manualDetail"),
          tone: "neutral",
          icon: "description",
        },
      ],
    };
  }

  const blockerCount = missingItems + stockLinesWithoutPresentation.length;
  return {
    hasXmlPurchase: true,
    blockerCount,
    items: [
      {
        key: "linked-items",
        label: t("purchases.confirm.checklist.linkedItems"),
        detail:
          missingItems === 0
            ? t("purchases.confirm.checklist.linkedItemsOk", {
                count: linkedItems,
              })
            : t("purchases.confirm.checklist.linkedItemsMissing", {
                count: missingItems,
              }),
        tone: missingItems === 0 ? "success" : "danger",
        icon: missingItems === 0 ? "check_circle" : "error",
      },
      {
        key: "linked-presentations",
        label: t("purchases.confirm.checklist.linkedPresentations"),
        detail:
          linesWithoutPresentation.length === 0
            ? t("purchases.confirm.checklist.linkedPresentationsOk")
            : t("purchases.confirm.checklist.linkedPresentationsMissing", {
                count: linesWithoutPresentation.length,
              }),
        tone:
          stockLinesWithoutPresentation.length > 0
            ? "danger"
            : linesWithoutPresentation.length > 0
              ? "warning"
              : "success",
        icon:
          linesWithoutPresentation.length === 0
            ? "check_circle"
            : stockLinesWithoutPresentation.length > 0
              ? "error"
              : "warning",
      },
      {
        key: "missing-presentations",
        label: t("purchases.confirm.checklist.linesWithoutPresentation"),
        detail:
          linesWithoutPresentation.length === 0
            ? t("purchases.confirm.checklist.linesWithoutPresentationOk")
            : unknownPresentationRisk.length > 0
              ? t("purchases.confirm.checklist.linesWithoutPresentationUnknown", {
                  count: linesWithoutPresentation.length,
                })
              : t("purchases.confirm.checklist.linesWithoutPresentationWarn", {
                  count: linesWithoutPresentation.length,
                }),
        tone:
          stockLinesWithoutPresentation.length > 0
            ? "danger"
            : linesWithoutPresentation.length > 0
              ? "warning"
              : "success",
        icon:
          linesWithoutPresentation.length === 0
            ? "check_circle"
            : stockLinesWithoutPresentation.length > 0
              ? "error"
              : "warning",
      },
      {
        key: "taxes",
        label: t("purchases.confirm.checklist.recognizedTaxes"),
        detail:
          taxesRecognized === xmlLines.length
            ? t("purchases.confirm.checklist.recognizedTaxesOk", {
                count: taxesRecognized,
              })
            : t("purchases.confirm.checklist.recognizedTaxesWarn", {
                count: xmlLines.length - taxesRecognized,
              }),
        tone: taxesRecognized === xmlLines.length ? "success" : "warning",
        icon: taxesRecognized === xmlLines.length ? "check_circle" : "warning",
      },
      {
        key: "total-difference",
        label: t("purchases.confirm.checklist.totalDifference"),
        detail:
          totalDifference === null
            ? t("purchases.confirm.checklist.totalDifferenceUnavailable")
            : t("purchases.confirm.checklist.totalDifferenceValue", {
                amount: formatMoneyWithSymbol(
                  Math.abs(totalDifference),
                  getDecimalConfig().totalAmount,
                ),
              }),
        tone:
          totalDifference === null
            ? "warning"
            : Math.abs(totalDifference) <= 0.01
              ? "success"
              : "warning",
        icon:
          totalDifference !== null && Math.abs(totalDifference) <= 0.01
            ? "check_circle"
            : "warning",
      },
    ],
  };
}

function XmlConfirmChecklist({
  summary,
}: {
  summary: XmlConfirmChecklistSummary;
}) {
  const { t } = useI18n();
  return (
    <div className="pf-confirm-checklist">
      <p className="pf-confirm-checklist__intro">
        {t(
          "purchases.confirm.message",
          "Esta acción generará movimientos de inventario y cuentas por pagar. No se puede revertir.",
        )}
      </p>
      {summary.hasXmlPurchase && (
        <p className="pf-confirm-checklist__hint">
          {summary.blockerCount > 0
            ? t(
                "purchases.confirm.checklist.blockingHint",
                "Hay controles pendientes que bloquearán la confirmación.",
              )
            : t(
                "purchases.confirm.checklist.readyHint",
                "Revise el checklist antes de confirmar la compra XML.",
              )}
        </p>
      )}
      <ul className="pf-confirm-checklist__list">
        {summary.items.map((item) => (
          <li
            key={item.key}
            className={`pf-confirm-checklist__item pf-confirm-checklist__item--${item.tone}`}
          >
            <span className="material-symbols-outlined pf-confirm-checklist__icon">
              {item.icon}
            </span>
            <span>
              <strong>{item.label}</strong>
              <span className="pf-confirm-checklist__detail">
                {item.detail}
              </span>
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
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
              onModalOpenChange={setProductModalOpen}
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

function PurchaseLineCard({
  line: l,
  idx,
  ctx,
}: {
  line: PurchaseLineDraft;
  idx: number;
  ctx: ReturnType<typeof usePurchasesPage>;
}) {
  const { t } = useI18n();
  const viewMatchedItem = useViewMatchedItem();
  // Estado local de expandir/colapsar SOLO la banda inferior de detalle
  // (Producto recibido XML / Presentación e inventario / Información
  // comercial) — no se persiste (ni backend ni localStorage), vive por
  // instancia de línea (key={l._key} en el .map() que renderiza esta
  // card). Inicia expandido para no ocultar información por defecto.
  const [collapsed, setCollapsed] = useState(false);
  // PURCHASE-LINE-HEADER-EDIT-RECALCULATE-TAX-TOTAL-01 — Base/IVA/ICE/Total de la cabecera
  // editable viven en `vm.editableHeader`, recalculados en cada render desde quantity/unitPrice/
  // discountPct/vatCode/iceCode actuales — nunca desde el snapshot XML (`vm.xml.*`, que se muestra
  // aparte en "Producto recibido (XML)" y no cambia con la edición). Antes de este fix, una línea
  // con origen XML mostraba xmlTaxValue/xmlTotalLine en cabecera sin importar que el usuario ya
  // hubiera cambiado quantity/unitPrice — Base imp. sí se recalculaba pero IVA/Total quedaban
  // pegados al valor documental original.
  const vm = buildPurchaseLinePresentation(l, t, ctx.vatRatesMap, ctx.iceRatesMap);
  const sub = vm.editableHeader.taxableBase;
  const vatAmt = vm.editableHeader.vatAmount;
  const iceAmt = vm.editableHeader.iceAmount;
  const irbpnrAmt = vm.editableHeader.irbpnrAmount;
  const total = vm.editableHeader.total;
  const ctxData = l.context;
  const vatPct = ctxData?.vatPercent ?? ctx.vatRatesMap[l.vatCode] ?? 0;
  const readiness = ctx.lineReadinessByKey[l._key];
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
  const handlePackagingChange = (packagingLevelId: string) => {
    const selected = packagingLevels.find((p) => p.id === packagingLevelId);
    ctx.updateLine(l._key, "packagingLevelId", packagingLevelId || undefined);
    ctx.updateLine(l._key, "uomCode", selected?.uomCode ?? ctxData?.baseUomCode);
    ctx.updateLine(l._key, "baseUomCode", ctxData?.baseUomCode);
    ctx.updateLine(l._key, "conversionFactor", selected?.baseQuantity ?? 1);
    ctx.updateLine(
      l._key,
      "quantityInBaseUom",
      l.quantity * (selected?.baseQuantity ?? 1),
    );
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
    if (wh) void ctx.fetchItemContext(l._key, p.id, wh);
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

  return (
    // DS-LINE-CARD-UNIFY-02: wrapper externo unificado (ZHLineCard) en vez de la caja local
    // `.zh-detail-line pdl-line-row` — mismo rail izquierdo aprobado (número, menú de producto,
    // duplicar, eliminar, colapso — plantilla DESIGN-SYSTEM-LINE-RAIL-04), pasado como prop
    // `rail`; el contenido de la línea (`.pdl-line`, sin cambios internos) sigue como children.
    <ZHLineCard
      className="pdl-line-card"
      rail={
        <>
          <span className="pdl-line-row__num">
            {String(idx + 1).padStart(2, "0")}
          </span>
          <ProductLineActionMenu
            line={l}
            ctx={ctx}
            disabled={ctx.fieldDisabled}
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
            onClick={() => ctx.duplicateLine(l._key)}
          />
          <ZHRowDeleteAction
            compact
            showText={false}
            title={t("purchases.lines.delete", "Eliminar")}
            label={t("purchases.lines.delete", "Eliminar")}
            onClick={() => ctx.removeLine(l._key)}
          />
          <button
            type="button"
            className="pdl-line-row__collapse-toggle"
            aria-expanded={!collapsed}
            aria-label={
              collapsed
                ? t("purchases.lines.expandDetail", "Mostrar detalle de la línea")
                : t("purchases.lines.collapseDetail", "Ocultar detalle de la línea")
            }
            title={
              collapsed
                ? t("purchases.lines.expandDetail", "Mostrar detalle de la línea")
                : t("purchases.lines.collapseDetail", "Ocultar detalle de la línea")
            }
            onClick={() => setCollapsed((current) => !current)}
          >
            <span
              className={`material-symbols-outlined pdl-line-row__collapse-icon${collapsed ? "" : " pdl-line-row__collapse-icon--open"}`}
            >
              expand_more
            </span>
          </button>
        </>
      }
    >
      <div className="pdl-line">
      <div className="pdl-line__main">
        <div className="pdl-line__summary">
          <div className="pdl-line__identity">
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
          </div>
          {/* PURCHASE-LINE-HEADER-UX-03 — cada métrica pasa a ser columna directa de
              .pdl-line__summary (antes agrupadas en .pdl-line__metrics) para que la cabecera sea
              un grid real de celdas (mismo criterio que .sf-product en Ventas: columnas con
              minmax() y separadores, no un bloque flex anidado) — mismos datos/orden/handlers. */}
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.quantity", "Cantidad")}
              </ZHFieldLabel>
              <ZhDecimalInput
                key={headerInputKey("qty", vm, l.packagingLevelId)}
                className="pdl-input pdl-input--qty"
                density="compact"
                decimals={getDecimalConfig().quantity}
                positiveOnly
                defaultValue={
                  vm.inventory.hasPresentation
                    ? vm.inventory.baseQuantityValue
                    : l.quantity
                }
                onBlur={(e) => {
                  const entered = Number(e.target.value) || 0;
                  if (vm.inventory.hasPresentation) {
                    // PURCHASE-LINE-HEADER-INVENTORY-MODE-01 — con presentación vinculada, este
                    // campo edita la cantidad real de inventario (unidad base), no la cantidad de
                    // presentación — se reversa a `quantity` dividiendo por el factor de conversión
                    // para no corromper el dato que el backend interpreta en unidades de presentación.
                    const factor = vm.inventory.conversionFactorValue || 1;
                    ctx.updateLine(l._key, "quantity", entered / factor || 1);
                    ctx.updateLine(l._key, "quantityInBaseUom", entered);
                  } else {
                    ctx.updateLine(l._key, "quantity", entered || 1);
                  }
                }}
                disabled={ctx.fieldDisabled}
              />
            </div>
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.costShort", "COSTO")}
              </ZHFieldLabel>
              <ZHInputGroup className="pdl-line__metric-value" prefix="$">
                <ZhDecimalInput
                  key={headerInputKey("cost", vm, l.packagingLevelId)}
                  className="pdl-input pdl-input--cost"
                  density="compact"
                  decimals={getDecimalConfig().purchaseUnitPrice}
                  positiveOnly
                  defaultValue={
                    vm.inventory.hasPresentation
                      ? vm.inventory.baseUnitCostValue
                      : l.unitPrice
                  }
                  onBlur={(e) => {
                    const entered = Number(e.target.value) || 0;
                    if (vm.inventory.hasPresentation) {
                      // PURCHASE-LINE-HEADER-BASE-QTY-COST-EDIT-01 — con presentación vinculada este
                      // campo edita el costo real unitario base; se reversa a `unitPrice` (fórmula
                      // inversa de baseUnitCost, considerando descuento y flete/otros gastos ya
                      // asignados) para que la línea siga cuadrando fiscalmente. Si el cálculo no es
                      // seguro (cantidades en 0, descuento >= 100%, resultado negativo), no se aplica
                      // — se conserva el unitPrice actual en vez de inventar un valor.
                      const recalculatedUnitPrice = computeUnitPriceFromBaseUnitCost({
                        newBaseUnitCost: entered,
                        quantity: l.quantity ?? 0,
                        quantityInBaseUom: vm.inventory.baseQuantityValue,
                        discountPct: l.discountPct ?? 0,
                        freightAllocated: l.freightAllocated ?? 0,
                        otherCostsAllocated: l.otherCostsAllocated ?? 0,
                      });
                      if (recalculatedUnitPrice === null) return;
                      ctx.updateLine(l._key, "unitPrice", recalculatedUnitPrice);
                      return;
                    }
                    ctx.updateLine(l._key, "unitPrice", entered);
                  }}
                  disabled={ctx.fieldDisabled}
                />
              </ZHInputGroup>
            </div>
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.taxableBaseShort", "BASE IMP.")}
              </ZHFieldLabel>
              <div className="pdl-line__metric-value">
                <ZHMoneyValue value={sub} decimals={getDecimalConfig().totalAmount} />
              </div>
            </div>
            <div className="pdl-line__metric">
              <div className="pdl-line__tax">
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.vatShort", "IVA")} {vatPct}%: $
                </ZHFieldLabel>
                <div className="pdl-line__tax-amount">
                  <ZHMoneyValue
                    value={vatAmt}
                    decimals={getDecimalConfig().totalAmount}
                    currencySymbol=""
                    emphasis="strong"
                  />
                </div>
                <div>
                  <ZHDataValue variant="muted">
                    {t("purchases.lines.iceShort", "ICE")}:{" "}
                  </ZHDataValue>
                  <ZHMoneyValue
                    value={iceAmt}
                    decimals={getDecimalConfig().totalAmount}
                    emphasis="muted"
                  />
                </div>
                {irbpnrAmt > 0 && (
                  <div>
                    <ZHDataValue variant="muted">
                      IRBPNR:{" "}
                    </ZHDataValue>
                    <ZHMoneyValue
                      value={irbpnrAmt}
                      decimals={getDecimalConfig().totalAmount}
                      emphasis="muted"
                    />
                  </div>
                )}
              </div>
            </div>
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.netTotal", "Total neto")}
              </ZHFieldLabel>
              <div className="pdl-line__total">
                <ZHMoneyValue
                  value={total}
                  decimals={getDecimalConfig().totalAmount}
                  emphasis="grand"
                />
              </div>
            </div>
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.marginShort", "Margen")}
              </ZHFieldLabel>
              <Badge
                variant={vm.commercial.profitability.marginPctValue >= 0 ? "success" : "error"}
                size="md"
                label={`${vm.commercial.profitability.marginPct}%`}
              />
            </div>
        </div>
      </div>

      {/* Resumen de estado de preparación: solo interpreta datos ya presentes en la línea. */}
      <div className="pdl-line__status">
        {readiness ? (
          <>
            {readiness.status !== "MISSING_ITEM" &&
              readiness.status !== "READY" && (
                <Badge
                  variant={STATUS_TONE_VARIANT[readiness.tone]}
                  label={readiness.label}
                />
              )}
            {readiness.blocking && (
              <ZHCompactNotice
                notice={buildNotice({
                  severity: toneToSeverity(readiness.tone),
                  intent: "blocking",
                  source: "domain-status",
                  label: readiness.label,
                  detail: showReadinessActions
                    ? `${readiness.detail} ${t("purchases.lineReadiness.nextAction", "Acción")}: ${showReadinessActions}`
                    : readiness.detail,
                })}
              />
            )}
            {/* PURCHASE-LINE-MISSING-SALE-PRICE-WARNING-01 — no bloqueante, independiente del
                bloqueo de arriba: ambos pueden coexistir (p. ej. falta bodega Y falta precio de
                venta a la vez). */}
            {readiness.warning && (
              <ZHCompactNotice
                notice={buildNotice({
                  severity: toneToSeverity(readiness.warning.tone),
                  intent: "status",
                  source: "domain-status",
                  label: readiness.warning.label,
                  detail: readiness.warning.detail,
                })}
              />
            )}
          </>
        ) : (
          <Badge
            variant={STATUS_TONE_VARIANT[vm.status.tone]}
            label={`${vm.status.icon} ${vm.status.label}`}
          />
        )}
      </div>

      {/* 1. Qué llegó del proveedor — visible mientras la línea no esté colapsada,
          con nota cuando la línea no viene de XML. Solo esta banda se oculta al
          colapsar: el aviso de estado/readiness de arriba (.pdl-line__status)
          queda siempre visible, incluso colapsado. */}
      {!collapsed && (
      <div className="pdl-blocks">
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
          <div className="pdl-ctx-col__badges">
            <Badge
              variant="neutral"
              code
              label={`${t("purchases.lines.supplierCode", "Cód. proveedor")}: ${vm.xml.supplierCode}`}
            />
            <Badge
              variant={vm.xml.hasSupplierAuxCode ? "warning" : "neutral"}
              code
              label={`${t("purchases.lines.auxCode", "Cód. auxiliar")}: ${vm.xml.supplierAuxCode}`}
            />
          </div>
          <ZHDataValue variant="muted" className="pdl-ctx-col__desc">
            {vm.xml.description}
          </ZHDataValue>
          <div className="pdl-ctx-col__costs">
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
                  {t("purchases.lines.taxableBase", "Base imponible")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="numeric">{vm.xml.taxableBase}</ZHDataValue>}
            />
            <ZHInfoRow
              label={<ZHFieldLabel size="sm">IVA ({vm.xml.vatPercentage}%)</ZHFieldLabel>}
              value={<ZHDataValue variant="numeric">{vm.xml.taxValue}</ZHDataValue>}
            />
            <ZHInfoRow
              label={<ZHFieldLabel size="sm">{t("purchases.lines.iceShort", "ICE")}</ZHFieldLabel>}
              value={<ZHDataValue variant="numeric">{vm.xml.iceValue}</ZHDataValue>}
            />
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
            {packagingLevels.length > 0 && (
              <div>
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.presentation", "Presentación")}
                </ZHFieldLabel>
                <ZhSelect
                  density="compact"
                  value={l.packagingLevelId ?? ""}
                  onChange={(e) => handlePackagingChange(e.target.value)}
                  disabled={ctx.fieldDisabled}
                >
                  <option value="">
                    {t("purchases.lines.baseUnit", "Unidad base")}
                  </option>
                  {packagingLevels.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} x {p.baseQuantity} {ctxData?.baseUomCode}
                    </option>
                  ))}
                </ZhSelect>
              </div>
            )}
            {vm.item.hasItem &&
              vm.xml.hasOrigin &&
              packagingLevels.length === 0 &&
              !vm.inventory.hasPresentation && (
                <ZHInfoRow
                  wide
                  label={
                    <ZHFieldLabel size="sm">
                      {t("purchases.lines.presentationUnavailable", "Presentación")}
                    </ZHFieldLabel>
                  }
                  value={
                    <Badge
                      variant="neutral"
                      label={t(
                        "purchases.lines.presentationNotLinked",
                        "Sin presentación vinculada",
                      )}
                    />
                  }
                />
              )}
            {vm.inventory.hasPresentation && (
              <ZHInfoRow
                wide
                label={
                  <ZHFieldLabel size="sm">
                    {t("purchases.lines.linkedPresentation", "Presentación vinculada")}
                  </ZHFieldLabel>
                }
                value={<ZHDataValue variant="numeric">{vm.inventory.presentationLabel}</ZHDataValue>}
              />
            )}
            {vm.item.hasItem && !!vm.inventory.equivalenceDetail && (
              <ZHInfoRow
                wide
                label={
                  <ZHFieldLabel size="sm">
                    {t("purchases.lines.baseConversion", "Equivalencia")}
                  </ZHFieldLabel>
                }
                value={<ZHDataValue variant="numeric">{vm.inventory.equivalenceDetail}</ZHDataValue>}
              />
            )}
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.inventory", "Ingreso a inventario")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="numeric">{vm.inventory.baseQuantity}</ZHDataValue>}
            />
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.baseCost", "Costo unitario base")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="numeric">{vm.inventory.baseUnitCost}</ZHDataValue>}
            />
            {vm.item.hasItem && vm.xml.hasOrigin && (
              <div className="pdl-cost-wide">
                <ZHBtn
                  type="button"
                  variant="primary"
                  size="xs"
                  disabled={
                    ctx.fieldDisabled ||
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

        {/* 3. Impacto para el negocio — información de análisis, menor peso visual que los bloques anteriores. */}
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
          <div className="pdl-ctx-col__stock">
            <div className="pdl-stock-item">
              {vm.commercial.hasContext ? (
                <Badge
                  variant={vm.commercial.stock.isCritical ? "error" : "success"}
                  size="md"
                  label={vm.commercial.stock.statusLabel}
                />
              ) : (
                <ZHFieldLabel size="sm">{vm.commercial.stock.statusLabel}</ZHFieldLabel>
              )}
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
          <div className="pdl-ctx-col__costs">
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
          </div>
          {vm.commercial.costs.showDeviationAlert && (
            <ZHCompactNotice
              notice={buildNotice({
                severity: "warning",
                intent: "status",
                source: "domain-status",
                label: t("purchases.lines.costDeviation", "Costo fuera de rango"),
                detail: vm.commercial.costs.deviationLabel,
              })}
            />
          )}
          <div className="pdl-ctx-col__margin">
            <div className="pdl-margin-row">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.profitability", "Rentabilidad (margen neto)")}
              </ZHFieldLabel>
              <ZHDataValue
                variant="numeric"
                tone={vm.commercial.profitability.marginPctValue >= 0 ? "success" : "danger"}
              >
                {vm.commercial.profitability.marginPct}%
              </ZHDataValue>
            </div>
            <progress
              className="pdl-margin-bar pdl-margin-bar--progress"
              value={Math.min(100, Math.max(0, vm.commercial.profitability.marginPctValue))}
              max={100}
              aria-label={t("purchases.lines.profitabilityAria", "Rentabilidad margen neto")}
            />
            <div className="pdl-margin-meta">
              <ZHDataValue variant="muted">
                {t("purchases.lines.salePrice", "Precio de venta")}:{" "}
                {vm.commercial.profitability.pvp}
              </ZHDataValue>
              <ZHDataValue variant="muted">
                {t("purchases.lines.maxDiscount", "Desc. máx.")}:{" "}
                {vm.commercial.profitability.maxDiscountPercent}%
              </ZHDataValue>
            </div>
          </div>
        </section>
      </div>
      )}
      {/* Barra compacta que reemplaza visualmente a .pdl-blocks cuando la línea
          está colapsada — evita que se sienta como un espacio vacío y deja
          claro que hay detalle oculto (XML / presentación / info comercial).
          Mismo estado/handler que el chevron de la banda izquierda, solo un
          segundo disparador para expandir. */}
      {collapsed && (
        <button
          type="button"
          className="pdl-line__collapsed-bar"
          onClick={() => setCollapsed(false)}
        >
          <span className="material-symbols-outlined pdl-line__collapsed-bar-icon">
            info
          </span>
          <span className="pdl-line__collapsed-bar-text">
            {t(
              "purchases.lines.collapsedBar",
              "Detalle colapsado — Ver XML, presentación e información comercial",
            )}
          </span>
          <span className="material-symbols-outlined pdl-line__collapsed-bar-chevron">
            expand_more
          </span>
        </button>
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
    if (wh) void ctx.fetchItemContext(l._key, item.id, wh);
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
    if (wh) void ctx.fetchItemContext(l._key, item.id, wh);
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
 * "Crear Item" para una línea que sí tiene PurchaseReceptionLineId — reutiliza el mismo wrapper
 * que usa la pantalla de Recepción (`CreateItemFromReceptionLineModal`), que crea el Item y lo
 * vincula server-side con `matchItem` en un solo paso, sin reimplementar esa lógica aquí.
 */
function ReceptionCreateItemAction({
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

  const receptionLine: PurchaseReceptionLineMatch = {
    lineId: l.purchaseReceptionLineId as string,
    supplierId: ctx.formWatch.supplierId || null,
    supplierCode: l.xmlSupplierCode ?? null,
    supplierAuxCode: l.xmlSupplierAuxCode ?? null,
    description: l.description,
    quantity: l.quantity,
    unitPrice: l.unitPrice,
    itemId: l.itemId ?? null,
    matchStatus: l.itemMatchStatus ?? "PENDING",
    matchedAt: null,
    suggestions: [],
  };

  const applyLinked = (
    itemId: string,
    matchStatus: ItemMatchStatus,
    label: string,
    vatCode?: string | null,
  ) => {
    const current = ctx.getValues("lines");
    ctx.setValue(
      "lines",
      current.map((x: PurchaseLineFormValues) =>
        x._key === l._key
          ? {
              ...x,
              itemId,
              itemMatchStatus: matchStatus,
              description: label,
              _readinessIssue: undefined,
              // Solo se pisa si viene un IVA de compra real — el matching manual (sin `item`
              // recién creado) no debe borrar un vatCode ya cargado en la línea.
              ...(vatCode !== undefined ? { vatCode: vatCode ?? "" } : {}),
            }
          : x,
      ),
    );
    const wh = l.warehouseId || ctx.formWatch.globalWarehouseId;
    if (wh) void ctx.fetchItemContext(l._key, itemId, wh);
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
      <CreateItemFromReceptionLineModal
        open={open}
        line={receptionLine}
        supplierName={ctx.supplierProfile?.name ?? ""}
        purchaseContext={
          l.unitPrice > 0
            ? {
                unitCost: l.unitPrice,
                quantity: l.quantity,
                discountPct: l.discountPct ?? undefined,
                vatCode: l.vatCode || undefined,
              }
            : undefined
        }
        onClose={() => {
          setOpen(false);
          onModalOpenChange?.(false);
          onAfterModalClose?.();
        }}
        onCreated={(updatedLine, item) => {
          setOpen(false);
          onModalOpenChange?.(false);
          onAfterModalClose?.();
          const profile = buildPurchaseItemProfile(item);
          if (updatedLine.itemId)
            applyLinked(
              updatedLine.itemId,
              updatedLine.matchStatus,
              profile.label,
              profile.purchaseVatCode,
            );
        }}
        onCreatedButNotLinked={() => {
          setOpen(false);
          onModalOpenChange?.(false);
          onAfterModalClose?.();
        }}
      />
    </>
  );
}

function PaymentScheduleSection({
  ctx,
}: {
  ctx: ReturnType<typeof usePurchasesPage>;
}) {
  const { t } = useI18n();
  return (
    <div className="pf-card">
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
                getDecimalConfig().totalAmount,
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
                    <ZHMoneyValue value={s.amount} decimals={getDecimalConfig().totalAmount} />
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
                        decimals={getDecimalConfig().totalAmount}
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
                  <ZHMoneyValue value={ctx.ptRowsSum} decimals={getDecimalConfig().totalAmount} />
                </strong>
                {" / "}
                {t("purchases.schedule.purchaseTotal", "Total compra")}:{" "}
                <strong className="pf-schedule-footer__purchase-total">
                  <ZHMoneyValue value={ctx.localTotal} decimals={getDecimalConfig().totalAmount} />
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
  return (
    <div className="pf-card pf-retention">
      <div className="pf-card__header">
        <h4 className="pf-card__title">
          <span className="material-symbols-outlined pf-card__title-icon">
            receipt
          </span>{" "}
          {t("purchases.retention.title", "Retenciones")}
          <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_RETENTIONS} />
        </h4>
        <div className="pf-retention-actions">
          {!ctx.retention && (
            <>
              <ZHBtn
                type="button"
                variant="primary"
                size="sm"
                onClick={ctx.handleCalcRetention}
                disabled={ctx.whLoading}
              >
                <span
                  className="material-symbols-outlined pf-retention-action-icon"
                >
                  calculate
                </span>
                {t("purchases.retention.calculate", "Calcular")}
              </ZHBtn>
              {ctx.whPreview && ctx.whPreview.lines.length > 0 && (
                <ZHBtn
                  type="button"
                  variant="primary"
                  size="sm"
                  onClick={() => ctx.setModalWhIssue(true)}
                  disabled={ctx.whLoading}
                >
                  <span
                    className="material-symbols-outlined pf-retention-action-icon"
                  >
                    receipt_long
                  </span>
                  {t("purchases.retention.issue", "Emitir")}
                </ZHBtn>
              )}
            </>
          )}
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
              {ctx.canRegisterElectronic && (
                <ZHBtn
                  type="button"
                  variant="primary"
                  size="sm"
                  onClick={() => void ctx.handleRegisterRetentionElectronic()}
                  disabled={ctx.electronicPending}
                  title={t(
                    "purchases.retention.registerElectronic",
                    "Registrar electrónicamente",
                  )}
                >
                  <span className="material-symbols-outlined pf-retention-action-icon">
                    verified
                  </span>
                  {t("purchases.retention.registerElectronic", "Registrar electrónicamente")}
                </ZHBtn>
              )}
              {/* PURCHASES-RETENTIONS-CANCEL-05D — RetentionCanceller ya generaliza la reversa de
                  CxP por origen; visible solo con permiso de Compras (purchases.update, sin
                  permiso nuevo de Retenciones) y retención Issued. */}
              {ctx.canUpdatePurchase && (
                <ZHBtn
                  type="button"
                  variant="destructive"
                  size="sm"
                  onClick={() => ctx.setModalRetentionCancel(true)}
                  disabled={ctx.whLoading}
                  title={t("purchases.retention.cancelTitle", "Anular retención")}
                >
                  <span className="material-symbols-outlined pf-retention-action-icon">
                    cancel
                  </span>
                  {t("purchases.retention.cancel", "Anular")}
                </ZHBtn>
              )}
            </>
          )}
        </div>
      </div>
      <div className="pf-card__body">
        {ctx.whPreview && !ctx.retention && (
          <>
            {ctx.whPreview.skipReason && (
              <div className="pf-retention__skip">
                {ctx.whPreview.skipReason}
              </div>
            )}
            {ctx.whPreview.lines.length > 0 && (
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
                  {ctx.whPreview.lines.map((l, i) => (
                    <tr key={i}>
                      <td>
                        <Badge
                          variant={l.taxType === "IVA" ? "info" : "warning"}
                          label={l.taxType}
                        />
                      </td>
                      <td className="zh-code-value">
                        {l.retentionCode}
                      </td>
                      <td>{l.retentionCodeName}</td>
                      <td className="zh-table-cell--num">
                        <ZHMoneyValue value={l.taxableBase} decimals={getDecimalConfig().totalAmount} />
                      </td>
                      <td className="zh-table-cell--num">{l.retentionPct}%</td>
                      <td className="zh-table-cell--num pf-retention-amount">
                        <ZHMoneyValue value={l.amountRetained} decimals={getDecimalConfig().totalAmount} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            {ctx.whPreview.totalRetained > 0 && (
              <div className="pf-retention__total">
                {t("purchases.retention.totalToWithhold", "Total a retener")}:{" "}
                <ZHMoneyValue value={ctx.whPreview.totalRetained} decimals={getDecimalConfig().totalAmount} />
              </div>
            )}
          </>
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
            </div>
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
                      <ZHMoneyValue value={l.baseAmount} decimals={getDecimalConfig().totalAmount} />
                    </td>
                    <td className="zh-table-cell--num">{l.retentionRate}%</td>
                    <td className="zh-table-cell--num pf-retention-amount">
                      <ZHMoneyValue value={l.retainedAmount} decimals={getDecimalConfig().totalAmount} />
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

function SummaryPanel({ ctx }: { ctx: ReturnType<typeof usePurchasesPage> }) {
  const { t } = useI18n();
  // PURCHASE-INVOICE-TOTALS-SUMMARY-PANEL-01 — desglose por tarifa IVA, calculado siempre desde
  // el draft editable (ctx.lines: quantity/unitPrice/discountPct/vatCode/iceCode), nunca desde el
  // snapshot XML (xmlQuantity/xmlUnitPrice/xmlTaxableBase/...), que solo se muestra por línea en
  // "Producto recibido (XML)". Solo lectura — sin inputs, sin ajustes manuales (fase futura).
  const vatBreakdown = calcVatBreakdown(
    ctx.lines,
    ctx.vatRatesMap,
    ctx.iceRatesMap,
  );
  const hasIce = vatBreakdown.some((row) => row.ice !== 0);
  return (
    <div className="pf-totals">
      <div className="pf-totals__header">
        <h4>
          <span
            className="material-symbols-outlined pf-totals__title-icon--primary"
          >
            summarize
          </span>{" "}
          {t("purchases.summary.title", "Resumen")}
        </h4>
      </div>
      {vatBreakdown.length > 0 && (
        <div className="pf-totals__vat-breakdown">
          <table className="table table--compact table--neutral">
            <thead>
              <tr>
                <th>{t("purchases.summary.vatRate", "% IVA")}</th>
                <th className="zh-table-cell--num">
                  {t("purchases.summary.taxableBase", "Base imponible")}
                </th>
                <th className="zh-table-cell--num">
                  {t("purchases.lines.vatShort", "IVA")}
                </th>
                {hasIce && (
                  <th className="zh-table-cell--num">
                    {t("purchases.lines.iceShort", "ICE")}
                  </th>
                )}
                <th className="zh-table-cell--num">
                  {t("purchases.summary.rowTotal", "Total")}
                </th>
              </tr>
            </thead>
            <tbody>
              {vatBreakdown.map((row) => (
                <tr key={row.vatCode}>
                  <td>{row.vatPercent}%</td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue value={row.taxableBase} decimals={getDecimalConfig().totalAmount} />
                  </td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue value={row.vat} decimals={getDecimalConfig().totalAmount} />
                  </td>
                  {hasIce && (
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue value={row.ice} decimals={getDecimalConfig().totalAmount} />
                    </td>
                  )}
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue value={row.total} decimals={getDecimalConfig().totalAmount} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="pf-totals__body">
        <div className="pf-totals__row">
          <span className="pf-totals__label">{t("purchases.summary.subtotal", "Subtotal")}</span>
          <span className="pf-totals__value">
            <ZHMoneyValue
              value={ctx.editing ? ctx.editing.subtotal : ctx.localSummary.subtotal}
              decimals={getDecimalConfig().totalAmount}
            />
          </span>
        </div>
        <div className="pf-totals__row">
          <span className="pf-totals__label">{t("purchases.summary.discount", "Descuento")}</span>
          <span
            className="pf-totals__value pf-totals__value--warning"
          >
            -
            <ZHMoneyValue
              value={
                ctx.editing ? ctx.editing.totalDiscount : ctx.localSummary.discount
              }
              decimals={getDecimalConfig().totalAmount}
            />
          </span>
        </div>
        <div className="pf-totals__row">
          <span className="pf-totals__label">{t("purchases.lines.iceShort", "ICE")}</span>
          <span className="pf-totals__value">
            <ZHMoneyValue
              value={ctx.editing ? ctx.editing.totalIce : ctx.localSummary.ice}
              decimals={getDecimalConfig().totalAmount}
            />
          </span>
        </div>
        <div className="pf-totals__row">
          <span className="pf-totals__label">
            {t("purchases.lines.vatShort", "IVA")}
            <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_VAT} />
          </span>
          <span className="pf-totals__value">
            <ZHMoneyValue
              value={ctx.editing ? ctx.editing.totalVat : ctx.localSummary.vat}
              decimals={getDecimalConfig().totalAmount}
            />
          </span>
        </div>
        {!!ctx.editing?.totalIrbpnr && (
          <div className="pf-totals__row">
            <span className="pf-totals__label">
              IRBPNR{" "}
              <span
                className="pf-totals__value--warning"
                title={t(
                  "purchases.summary.irbpnrHint",
                  "Informativo — no incluido en el Gran Total mientras no exista soporte contable.",
                )}
              >
                (i)
              </span>
            </span>
            <span className="pf-totals__value">
              <ZHMoneyValue value={ctx.editing.totalIrbpnr} decimals={getDecimalConfig().totalAmount} />
            </span>
          </div>
        )}
        <div className="pf-totals__row">
          <span className="pf-totals__label">{t("purchases.summary.freight", "Flete")}</span>
          <span className="pf-totals__value">
            <ZHMoneyValue
              value={ctx.editing ? ctx.editing.totalFreight : ctx.formWatch.freightCost}
              decimals={getDecimalConfig().totalAmount}
            />
          </span>
        </div>
        <div className="pf-totals__row">
          <span className="pf-totals__label">{t("purchases.summary.otherCosts", "Otros costos")}</span>
          <span className="pf-totals__value">
            <ZHMoneyValue
              value={ctx.editing ? ctx.editing.totalOtherCosts : ctx.formWatch.otherCosts}
              decimals={getDecimalConfig().totalAmount}
            />
          </span>
        </div>
        <div className="pf-totals__divider" />
        <div className="pf-totals__row">
          <span className="pf-totals__label pf-totals__label--strong">
            {t("purchases.summary.lines", "Líneas")}
          </span>
          <span className="pf-totals__value">{ctx.lines.length}</span>
        </div>
      </div>
      <div className="pf-totals__grand">
        <span className="pf-totals__grand-label">{t("purchases.summary.total", "TOTAL")}</span>
        <span className="pf-totals__grand-value">
          <ZHMoneyValue
            value={ctx.editing ? ctx.editing.grandTotal : ctx.localTotal}
            decimals={getDecimalConfig().totalAmount}
          />
        </span>
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
        <ZHMoneyValue value={value} decimals={getDecimalConfig().totalAmount} />
      </div>
    </div>
  );
}
