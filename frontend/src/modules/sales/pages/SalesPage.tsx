import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHToggleTile } from "../../../components/zh/ZHToggleTile";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHTabBar, type ZHTab } from "../../../components/zh/ZHTabBar";
import { ZhDecimalInput, ZhTextInput, ZhSelect } from "../../../components/zh/inputs";
import { ZHPromptModal } from "../../../components/zh/ZHConfirmModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHElectronicEnvironmentBanner } from "../../../components/zh/ZHElectronicEnvironmentBanner";
import { formatMoney } from "../../../lib/sanitizers";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { CustomerPicker } from "../components/CustomerPicker";
import { SalesInvoiceDetailsSection } from "../components/SalesInvoiceDetailsSection";
import { PaymentDetailModal } from "../components/PaymentDetailModal";
import { CreditSimulatorModal } from "../components/CreditSimulatorModal";
import { QuickCustomerModal } from "../components/QuickCustomerModal";
import { SalesElectronicDiagnosticDrawer } from "../components/SalesElectronicDiagnosticDrawer";
import { SalesIssueModal } from "../components/SalesIssueModal";
import {
  useSalesPage,
  type SalesPageContext,
  type CashSessionCheckErrorReason,
} from "../hooks/useSalesPage";
import { useRideActions } from "../hooks/useRideActions";
import { PAYMENT_EXCEEDS_TOLERANCE } from "../constants/tolerances";
import "../styles/sales-invoice.css";
import "../../../styles/shared/erp-form-core.css";
import "../../electronicDocuments/monitor/components/electronic-documents-monitor.css";
import "./SalesPage.css";

// Mensaje por motivo real de falla al consultar GET /cash-sessions/my — nunca el mismo texto que
// "no hay caja abierta" (esa es la única respuesta 200 OK con `null`, ver useSalesPage.ts).
const CASH_SESSION_ERROR_MESSAGE: Record<CashSessionCheckErrorReason, string> =
  {
    permission:
      "No se pudo verificar la caja abierta por falta de permiso para consultar caja.",
    context:
      "No se pudo verificar la caja abierta por contexto incompleto de empresa/sucursal.",
    server:
      "No se pudo verificar la caja abierta. Reintente o revise conexión/servidor.",
  };

/** Aviso de estado de caja — distingue "no hay caja" (confirmado) de "no se pudo verificar"
 * (permiso/contexto/servidor), con acción de reintento para el segundo caso. */
function CashSessionNotice({ ctx }: { ctx: SalesPageContext }) {
  if (ctx.cashSessionCheckError) {
    return (
      <div className="sf-cash-session-notice">
        <ZHPageNotice
          variant="error"
          message={CASH_SESSION_ERROR_MESSAGE[ctx.cashSessionCheckError]}
        />
        <ZHBtn
          variant="ghost"
          size="xs"
          type="button"
          onClick={ctx.refreshCashSession}
        >
          Reintentar
        </ZHBtn>
      </div>
    );
  }
  if (ctx.hasCashSession === false) {
    return (
      <ZHPageNotice
        variant="warning"
        message="No tiene una caja abierta. Debe abrir una caja antes de autorizar facturas."
      />
    );
  }
  return null;
}

/** Saldo pendiente de cobro excluyendo los pagos ya asignados a una forma de pago específica —
 * único punto de este cálculo (redondeo a la precisión configurada), usado tanto para el
 * disponible mostrado en PaymentDetailModal como para precargar el monto de un nuevo pago en
 * la grilla de formas de cobro. Puede devolver negativo (ya se cobró de más con otras formas);
 * cada llamador decide si clamplear a 0 según su propio uso. */
function remainingToCollect(
  ctx: SalesPageContext,
  excludePaymentMethodId: string,
): number {
  const factor = 10 ** getDecimalConfig().totalAmount;
  const othersTotal = ctx.payments
    .filter((p) => p.paymentMethodId !== excludePaymentMethodId)
    .reduce((s, p) => s + (p.amount || 0), 0);
  return Math.round((ctx.summary.total - othersTotal) * factor) / factor;
}

export function SalesPage() {
  const ctx = useSalesPage();
  const ride = useRideActions();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const openedFromParam = useRef(false);
  const [sriDiagnosticOpen, setSriDiagnosticOpen] = useState(false);

  // Entrada cruzada desde el Kardex ("Ver documento origen"): abre la factura referida.
  useEffect(() => {
    const invoiceId = searchParams.get("invoiceId");
    if (invoiceId && !openedFromParam.current) {
      openedFromParam.current = true;
      void ctx.loadForEdit(invoiceId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams]);

  const statusLabel = (s: string) =>
    s === "Draft" ? "Borrador" : s === "Authorized" ? "Autorizada" : "Anulada";
  const statusBadgeVariant = (s: string): BadgeVariant =>
    s === "Draft"
      ? "warning"
      : s === "Authorized"
        ? "success"
        : "error";

  return (
    <div className="sales-page-root">
      {/* Solo aplica a Puntos de Emisión Electrónicos — fuente única ctx.isElectronic
          (CashRegister → EmissionPoint → EmissionType). Un Punto Físico nunca debe consultar
          ni mostrar estado de configuración SRI. */}
      {ctx.isElectronic && <ZHElectronicEnvironmentBanner />}

      {/* ── Aviso caja no abierta / no se pudo verificar ────────────── */}
      <CashSessionNotice ctx={ctx} />

      {/* ── Aviso de error de guardado/emisión — área superior, siempre visible sin
          depender del scroll del sidebar (ver SalesErrorNotice en useSalesPage.ts). */}
      {ctx.tab === "nuevo" && ctx.saveError && (
        <div className="sales-page-save-error">
          <ZHPageNotice
            variant="error"
            message={ctx.saveError.title}
            detail={ctx.saveError.detail}
          />
        </div>
      )}

      {/* ═══════════════════════════ LISTADO ═══════════════════════════ */}
      {ctx.tab === "listado" && (
        <div className="prd-section">
          <div className="pg-table-controls sales-page-listbar">
            <ZHBtn
              type="button"
              variant="primary"
              onClick={() => {
                void ctx.resetForm();
                ctx.setTab("nuevo");
              }}
            >
              <span className="material-symbols-outlined zh-icon-md">
                add
              </span>
              Nueva Factura
            </ZHBtn>
            <div className="sales-page-spacer" />
            <ZhTextInput
              placeholder="Buscar por número o cliente..."
              value={ctx.listSearch}
              onChange={(e) => ctx.setListSearch(e.target.value)}
              className="sales-page-search"
            />
            <ZHBtn
              variant="secondary"
              onClick={ctx.fetchList}
              disabled={ctx.listLoading}
            >
              <span className="material-symbols-outlined zh-icon-lg">
                refresh
              </span>
            </ZHBtn>
          </div>
          {ctx.listLoading ? (
            <p>Cargando...</p>
          ) : (
            <div className="table-scroll">
              <table className="table table--compact table--neutral">
              <thead>
                <tr>
                  <th>Nro. Factura</th>
                  <th>Fecha</th>
                  <th>Cliente</th>
                  <th className="zh-text-align-right">Total</th>
                  <th className="zh-text-align-center">Líneas</th>
                  <th>Estado</th>
                  <th className="zh-text-align-center">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {ctx.listItems.map((inv) => (
                  <tr key={inv.id}>
                    <td className="sales-page-invoice-number zh-font-mono">
                      {inv.invoiceNumber}
                    </td>
                    <td>{inv.issueDate}</td>
                    <td>{inv.customerName}</td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue
                        value={inv.grandTotal}
                        decimals={getDecimalConfig().totalAmount}
                      />
                    </td>
                    <td className="zh-text-align-center">{inv.lineCount}</td>
                    <td>
                      <Badge
                        variant={statusBadgeVariant(inv.status)}
                        label={statusLabel(inv.status)}
                      />
                    </td>
                    <td className="zh-text-align-center">
                      <ZHIconButton
                        icon={inv.status === "Draft" ? "replay" : "edit"}
                        title={
                          inv.status === "Draft"
                            ? "Reintentar emisión"
                            : "Ver / Editar"
                        }
                        onClick={() => void ctx.loadForEdit(inv.id)}
                      />
                      {inv.status === "Authorized" && (
                        <ZHIconButton
                          icon="history"
                          title="Ver Movimiento de Inventario"
                          onClick={() =>
                            navigate(
                              `/inventory/kardex?docId=${inv.id}&docType=SalesInvoice`,
                            )
                          }
                        />
                      )}
                      {inv.status === "Authorized" && (
                        <ZHIconButton
                          icon="picture_as_pdf"
                          title="Ver RIDE"
                          disabled={ride.ridePending}
                          onClick={() => void ride.handleViewRide(inv.id)}
                        />
                      )}
                    </td>
                  </tr>
                ))}
                {ctx.listItems.length === 0 && (
                  <tr>
                    <td colSpan={7} className="zh-table-empty">
                      Sin facturas registradas.
                    </td>
                  </tr>
                )}
              </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* ═══════════════════════════ NUEVO / EDITAR (POS Layout) ═══════════════════════════ */}
      {ctx.tab === "nuevo" && (
        <div className="sf-layout">
          {/* ── SIDEBAR ── */}
          <div className="sf-sidebar">
            {/* Tabs */}
            <ZHTabBar
              tabs={
                [
                  {
                    id: "form",
                    label: ctx.editing ? "Editar Factura" : "Nueva Factura",
                    icon: "receipt_long",
                    inert: true,
                  },
                  { id: "history", label: "Historial", icon: "history" },
                ] as ZHTab<"form" | "history">[]
              }
              activeTab="form"
              fill
              onChange={(id) => {
                if (id === "history") {
                  void ctx.resetForm();
                  ctx.setTab("listado");
                }
              }}
            />

            {/* Checklist + Next Step (only in draft mode) */}
            {ctx.isDraft && !ctx.readOnly && <SalesFormChecklist ctx={ctx} />}

            {/* Datos de Emisión */}
            <div className="sf-sidebar__section">
              <div className="sf-sidebar__header zh-section-title">
                <span className="material-symbols-outlined sf-sidebar__header-icon">
                  apartment
                </span>
                Datos de Emisión
              </div>
              <div className="sf-emission">
                {/* Caja / Punto de emisión / Sucursal: solo informativos — el servidor los
                    resuelve desde ICurrentCashSession (la caja abierta del usuario), nunca
                    seleccionables manualmente. */}
                {ctx.branchName && (
                  <div>
                    <ZHFieldLabel size="sm" className="sf-emission__label">
                      {"Sucursal:"}
                    </ZHFieldLabel>
                    <span className="sf-emission__value">{ctx.branchName}</span>
                  </div>
                )}
                {ctx.myCashSession && (
                  <div>
                    <ZHFieldLabel size="sm" className="sf-emission__label">
                      {"Caja:"}
                    </ZHFieldLabel>
                    <span className="sf-emission__value">
                      {ctx.myCashSession.cashRegisterCodeSnapshot} —{" "}
                      {ctx.myCashSession.cashRegisterNameSnapshot}
                    </span>
                  </div>
                )}
                {ctx.myCashSession && (
                  <div>
                    <ZHFieldLabel size="sm" className="sf-emission__label">
                      {"Punto:"}
                    </ZHFieldLabel>
                    <span className="sf-emission__value">
                      {ctx.myCashSession.emissionPointCodeSnapshot}
                    </span>
                  </div>
                )}
                {/* Tipo de Emisión: fuente única EmissionPoint.EmissionType, resuelta en vivo por
                    el backend a través de CashSessionDto.emissionType (myCashSession) — se
                    prefiere sobre el snapshot de la factura (ctx.editing) para que se vea de
                    inmediato al abrir la pantalla, antes de crear ningún borrador. */}
                {(ctx.myCashSession?.emissionType ??
                  ctx.editing?.emissionType) && (
                  <div>
                    <ZHFieldLabel size="sm" className="sf-emission__label">
                      {"Tipo Emisión:"}
                    </ZHFieldLabel>
                    <span className="sf-emission__value">
                      {(ctx.myCashSession?.emissionType ??
                        ctx.editing?.emissionType) === "Electronic"
                        ? "🟢 Electrónica"
                        : "🔵 Física"}
                    </span>
                  </div>
                )}
                <div>
                  <ZHFieldLabel size="sm" className="sf-emission__label">
                    Tipo Documento
                  </ZHFieldLabel>
                  <ZhSelect
                    className="zh-select--compact zh-mb-4"
                    value={
                      ctx.readOnly
                        ? (ctx.editing?.docTypeCode ?? "")
                        : ctx.formWatch.docTypeCode
                    }
                    onChange={(e) =>
                      ctx.setValue("docTypeCode", e.target.value)
                    }
                    disabled={ctx.fieldDisabled}
                  >
                    {ctx.sriDocTypes.map((dt) => (
                      <option key={dt.code} value={dt.code}>
                        {dt.code} — {dt.name}
                      </option>
                    ))}
                  </ZhSelect>
                </div>
                <div>
                  <ZHFieldLabel size="sm" className="sf-emission__label">
                    Forma Pago SRI
                  </ZHFieldLabel>
                  <ZhSelect
                    className="zh-select--compact zh-mb-4"
                    value={
                      ctx.readOnly
                        ? (ctx.editing?.sriPaymentMethodCode ?? "")
                        : ctx.formWatch.sriPaymentMethodCode
                    }
                    onChange={(e) =>
                      ctx.setValue("sriPaymentMethodCode", e.target.value)
                    }
                    disabled={ctx.fieldDisabled}
                  >
                    {ctx.sriPaymentMethods.map((pm) => (
                      <option key={pm.code} value={pm.code}>
                        {pm.code} — {pm.name}
                      </option>
                    ))}
                  </ZhSelect>
                </div>
                {ctx.editing && (
                  <div className="zh-mt-4">
                    <ZHFieldLabel size="sm" className="sf-emission__label">
                      {"Nro:"}
                    </ZHFieldLabel>
                    <span className="sf-emission__value zh-font-mono">
                      {ctx.editing.invoiceNumber}
                    </span>
                  </div>
                )}
              </div>
            </div>

            {/* Cliente */}
            <div className="sf-sidebar__section">
              <div className="sf-sidebar__header zh-section-title">
                <span className="material-symbols-outlined sf-sidebar__header-icon">
                  person
                </span>
                Cliente
                <span
                  className="material-symbols-outlined sf-sidebar__header-right"
                  title="Nuevo cliente"
                >
                  person_add
                </span>
              </div>
              <ZHField
                density="compact"
                fieldError={ctx.errors.customerId?.message}
              >
                <CustomerPicker
                  value={ctx.formWatch.customerId || null}
                  onChange={ctx.handleCustomerChange}
                  disabled={ctx.fieldDisabled}
                  onCreateNew={ctx.openNewCustomerModal}
                  onEditSelected={
                    ctx.customerProfile ? ctx.openEditCustomerModal : undefined
                  }
                  editLabel="Editar datos"
                />
              </ZHField>
              {ctx.customerProfile && (
                <div className="sales-form-customer-profile">
                  {ctx.customerProfile.address && (
                    <div className="sales-form-profile-row">
                      <span className="material-symbols-outlined zh-icon-sm">
                        location_on
                      </span>
                      {ctx.customerProfile.address}
                    </div>
                  )}
                  {ctx.customerProfile.email && (
                    <div className="sales-form-profile-row">
                      <span className="material-symbols-outlined zh-icon-sm">
                        mail
                      </span>
                      {ctx.customerProfile.email}
                    </div>
                  )}
                  {ctx.customerProfile.phone && (
                    <div className="sales-form-profile-row">
                      <span className="material-symbols-outlined zh-icon-sm">
                        phone
                      </span>
                      {ctx.customerProfile.phone}
                    </div>
                  )}
                </div>
              )}
              {ctx.isConsumerFinalCustomer && ctx.consumerFinalPolicy && (
                <ZHPageNotice
                  variant={ctx.consumerFinalAmountExceeded ? "error" : "info"}
                  message={
                    ctx.consumerFinalAmountExceeded
                      ? ctx.consumerFinalPolicy.amountExceededMessage
                      : "Consumidor Final: solo ventas a contado."
                  }
                  detail={
                    ctx.consumerFinalAmountExceeded
                      ? undefined
                      : `Monto máximo permitido: ${formatMoney(
                          ctx.consumerFinalPolicy.consumerFinalMaxAmount,
                          getDecimalConfig().totalAmount,
                        )}. Para superarlo, seleccione un cliente identificado.`
                  }
                />
              )}
            </div>

            {/* Resumen Impuestos + Total */}
            <div className="sf-sidebar__section sales-form-tax-section">
              <div className="sf-total-box">
                <table className="sf-tax-table">
                  <thead>
                    <tr>
                      <th>Impuesto</th>
                      <th>Base</th>
                      <th>Valor</th>
                    </tr>
                  </thead>
                  <tbody>
                    {ctx.taxBreakdown.map((e) => (
                      <tr key={e.rate}>
                        <td>{e.label}</td>
                        <td>
                          <ZHMoneyValue
                            value={e.base}
                            decimals={getDecimalConfig().totalAmount}
                          />
                        </td>
                        <td>
                          <ZHMoneyValue
                            value={e.tax}
                            decimals={getDecimalConfig().totalAmount}
                          />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {ctx.totalDiscount > 0 && (
                  <div className="sf-summary__discount-total zh-mt-8">
                    <span>Descuento:</span>
                    <span>
                      -
                      <ZHMoneyValue
                        value={ctx.totalDiscount}
                        decimals={getDecimalConfig().totalAmount}
                      />
                    </span>
                  </div>
                )}
                <div className="sf-total-box__header zh-mt-10">
                  <span className="sf-total-box__label zh-section-title">
                    Total a Cobrar
                  </span>
                </div>
                <div className="sf-total-box__amount">
                  <ZHMoneyValue
                    value={ctx.grandTotal}
                    decimals={getDecimalConfig().totalAmount}
                    emphasis="total"
                  />
                </div>
              </div>
            </div>

            {/* Formas de Cobro */}
            <PaymentMethodsSection ctx={ctx} />

            {/* Errors */}
            {ctx.errors.lines && (
              <div className="sf-sidebar__section">
                <ZHPageNotice
                  variant="error"
                  message={
                    ctx.errors.lines.message ??
                    ctx.errors.lines.root?.message ??
                    ""
                  }
                />
              </div>
            )}
          </div>

          {/* ── MAIN AREA ── */}
          <div className="sf-main">
            <SalesInvoiceDetailsSection
              lines={ctx.lines}
              backendLines={ctx.editing?.lines}
              readOnly={ctx.readOnly}
              disabled={ctx.fieldDisabled}
              onRemoveLine={ctx.removeLine}
              onUpdateLine={ctx.updateLine}
              onAddItemLine={ctx.addLineWithItem}
              onUpdateLineWarehouse={ctx.onUpdateLineWarehouse}
              warehouses={ctx.warehouses}
              selectedWarehouseId={ctx.selectedWarehouseId}
              onWarehouseChange={ctx.handleWarehouseChange}
              vatRates={ctx.vatRatesMap}
              focusSignal={ctx.productSearchFocusKey}
            />
          </div>

          {/* ── BOTTOM BAR ── */}
          <div className="sf-bottombar">
            {ctx.isElectronic && (
              <div className="sf-bottombar__sri">
                <span className="sf-bottombar__sri-label">
                  <span className="material-symbols-outlined zh-icon-sm sales-page-sri-icon">
                    qr_code_2
                  </span>
                  Clave de Acceso SRI
                </span>
                <span className="sf-bottombar__sri-key zh-code-value">
                  {ctx.editing?.accessKey ?? "— se genera al emitir —"}
                </span>
                {ctx.editing && (
                  <ZHIconButton
                    icon="troubleshoot"
                    title="Ver diagnóstico SRI"
                    onClick={() => setSriDiagnosticOpen(true)}
                  />
                )}
              </div>
            )}

            <div className="sf-bottombar__spacer" />

            <ZHBtn
              type="button"
              variant="secondary"
              size="sm"
              onClick={() => void ctx.clearForm()}
            >
              <span className="material-symbols-outlined zh-icon-lg">
                delete_sweep
              </span>
              Limpiar Todo
            </ZHBtn>

            {ctx.isDraft && <EmitButton ctx={ctx} />}

            {ctx.editing &&
              ctx.isElectronic &&
              ctx.editing.status === "Authorized" &&
              ctx.editing.electronicStatus === "None" && (
                <ZHBtn
                  variant="secondary"
                  size="sm"
                  onClick={() => void ctx.handleGenerateElectronicDocument()}
                  disabled={ctx.saving}
                  title="Esta factura fue autorizada pero nunca generó su documento electrónico — regenera el registro en el Monitor."
                >
                  <span className="material-symbols-outlined zh-icon-lg">
                    bolt
                  </span>
                  Generar documento electrónico
                </ZHBtn>
              )}

            {ctx.editing && ctx.editing.status === "Authorized" && (
              <ZHBtn
                variant="secondary"
                size="sm"
                disabled={ride.ridePending}
                onClick={() => void ride.handleViewRide(ctx.editing!.id)}
                title="Abre el PDF del RIDE en una pestaña nueva. Reutiliza el ya generado si no cambió nada."
              >
                <span className="material-symbols-outlined zh-icon-lg">
                  picture_as_pdf
                </span>
                Ver RIDE
              </ZHBtn>
            )}

            {ctx.editing && ctx.editing.status === "Authorized" && (
              <ZHBtn
                variant="secondary"
                size="sm"
                disabled={ride.ridePending}
                onClick={() =>
                  void ride.handleDownloadRide(
                    ctx.editing!.id,
                    ctx.editing!.invoiceNumber,
                  )
                }
              >
                <span className="material-symbols-outlined zh-icon-lg">
                  download
                </span>
                Descargar RIDE
              </ZHBtn>
            )}

            {ctx.editing && ctx.editing.status === "Authorized" && (
              <ZHBtn
                variant="secondary"
                size="sm"
                disabled={ride.ridePending}
                onClick={() => void ride.handleRegenerateRide(ctx.editing!.id)}
                title="Fuerza una nueva generación del RIDE aunque el ya almacenado siga siendo válido."
              >
                <span className="material-symbols-outlined zh-icon-lg">
                  refresh
                </span>
                Regenerar RIDE
              </ZHBtn>
            )}

            {ctx.editing && ctx.editing.status === "Authorized" && (
              <ZHBtn
                variant="secondary"
                size="sm"
                className="sales-bottombar-btn--danger"
                onClick={() => ctx.setModalCancelReason(true)}
              >
                <span className="material-symbols-outlined zh-icon-lg">
                  block
                </span>
                Anular
              </ZHBtn>
            )}
          </div>
        </div>
      )}

      {/* ═══════════════════════════ MODALS ═══════════════════════════ */}

      <PaymentDetailModal
        open={ctx.modalDetail}
        methodName={ctx.detailMethodName}
        detailType={ctx.detailMethodType}
        initialRows={ctx.detailRows}
        initialKey={ctx.detailKey}
        available={remainingToCollect(ctx, ctx.detailMethodId)}
        onConfirm={(rows) => {
          ctx.setInvoicePayments((prev) => {
            const without = prev.filter(
              (p) => p.paymentMethodId !== ctx.detailMethodId,
            );
            const newPayments = rows.map((r) => ({
              _key: ctx.payKey + r._k,
              paymentMethodId: ctx.detailMethodId,
              amount: r.amount,
              reference: null,
              cardDetail: r.card ?? null,
              transferDetail: r.transfer ?? null,
              chequeDetail: r.cheque ?? null,
            }));
            return [...without, ...newPayments];
          });
          ctx.setPayKey((k) => k + rows.length + 1);
          ctx.setModalDetail(false);
        }}
        onCancel={() => ctx.setModalDetail(false)}
      />

      <CreditSimulatorModal
        open={ctx.modalCredit}
        amount={ctx.creditAmount}
        rows={ctx.creditRows}
        paymentTermName={ctx.selectedPt?.name}
        installments={ctx.selectedPt?.installments}
        daysBetween={ctx.selectedPt?.daysBetweenInstallments}
        onRowsChange={ctx.setCreditRows}
        onRecalculate={() =>
          ctx.setCreditRows(ctx.simulateCreditInstallments(ctx.creditAmount))
        }
        onConfirm={(totalAmount) => {
          const creditPm = ctx.paymentMethods.find((p) => p.isCreditAllowed);
          if (creditPm) {
            ctx.setInvoicePayments((prev) => {
              const exists = prev.find(
                (p) => p.paymentMethodId === creditPm.id,
              );
              if (exists)
                return prev.map((p) =>
                  p.paymentMethodId === creditPm.id
                    ? { ...p, amount: totalAmount }
                    : p,
                );
              return [
                ...prev,
                {
                  _key: ctx.payKey,
                  paymentMethodId: creditPm.id,
                  amount: totalAmount,
                  reference: null,
                },
              ];
            });
            const currentPayments = ctx.payments;
            if (!currentPayments.find((p) => p.paymentMethodId === creditPm.id))
              ctx.setPayKey((k) => k + 1);
          }
          ctx.setModalCredit(false);
        }}
        onCancel={() => ctx.setModalCredit(false)}
      />

      <SalesIssueModal
        phase={ctx.issuePhase}
        isElectronic={!!ctx.isElectronic}
        customerName={ctx.customerProfile?.name ?? ""}
        lineCount={ctx.lines.length}
        subtotal={ctx.summary.subtotal}
        discount={ctx.summary.discount}
        vat={ctx.summary.vat}
        total={ctx.summary.total}
        stepIndex={ctx.issueStepIndex}
        result={ctx.issueResult}
        ridePending={ride.ridePending}
        xmlDownloading={ctx.xmlDownloading}
        onPrintRide={() =>
          ctx.issueResult && void ride.handleViewRide(ctx.issueResult.id)
        }
        onDownloadPdf={() =>
          ctx.issueResult &&
          void ride.handleDownloadRide(
            ctx.issueResult.id,
            ctx.issueResult.invoiceNumber,
          )
        }
        onDownloadXml={() => void ctx.handleDownloadXml()}
        error={ctx.issueError}
        onRetry={ctx.retryIssue}
        onCancel={ctx.closeIssueFlow}
        onConfirm={() => void ctx.confirmIssue()}
        onNewSale={ctx.startNewSale}
      />

      <ZHPromptModal
        open={ctx.modalCancelReason}
        variant="danger"
        title="Anular Factura"
        message={
          ctx.editing
            ? `¿ANULAR factura ${ctx.editing.invoiceNumber}? Esta acción NO se puede deshacer.`
            : ""
        }
        label="Motivo de anulación"
        placeholder="Ingrese el motivo..."
        confirmLabel="Anular"
        onCancel={() => ctx.setModalCancelReason(false)}
        onConfirm={ctx.handleCancel}
      />

      {ctx.editing && (
        <SalesElectronicDiagnosticDrawer
          open={sriDiagnosticOpen}
          invoiceId={ctx.editing.id}
          invoiceNumber={ctx.editing.invoiceNumber}
          onClose={() => setSriDiagnosticOpen(false)}
        />
      )}

      <QuickCustomerModal
        open={ctx.modalNewCustomer}
        isEdit={ctx.newCustIsEdit}
        saving={ctx.newCustSaving}
        error={ctx.newCustError}
        custId={ctx.newCustId}
        custName={ctx.newCustName}
        custIdType={ctx.newCustIdType}
        custAddress={ctx.newCustAddress}
        custEmail={ctx.newCustEmail}
        custPhone={ctx.newCustPhone}
        sriIdTypes={ctx.sriIdTypes}
        onCustIdChange={ctx.setNewCustId}
        onCustNameChange={ctx.setNewCustName}
        onCustIdTypeChange={ctx.setNewCustIdType}
        onCustAddressChange={ctx.setNewCustAddress}
        onCustEmailChange={ctx.setNewCustEmail}
        onCustPhoneChange={ctx.setNewCustPhone}
        onSave={ctx.handleSaveQuickCustomer}
        onCancel={() => ctx.setModalNewCustomer(false)}
      />
    </div>
  );
}

// ── Form Readiness Checklist ─────────────────────────────────────────────
function SalesFormChecklist({ ctx }: { ctx: SalesPageContext }) {
  const hasCustomer = !!ctx.formWatch.customerId.trim();
  const hasLines = ctx.lines.length > 0;
  const hasEmissionPoint = ctx.hasCashSession === true;
  const paid = ctx.paidTotal;
  const total = ctx.summary.total;
  const paymentOk = ctx.paymentOk;
  const paymentExceeds = paid > total + PAYMENT_EXCEEDS_TOLERANCE;

  const canSaveDraft = hasCustomer && hasLines;
  const canEmit =
    canSaveDraft &&
    hasEmissionPoint &&
    paymentOk &&
    !ctx.cashInsufficient &&
    !ctx.hasInsufficientStock;

  const nextStep = !hasCustomer
    ? "Seleccione un cliente para comenzar."
    : !hasLines
      ? "Agregue productos a la factura."
      : ctx.hasInsufficientStock
        ? "Hay líneas con cantidad mayor al stock disponible — ajústelas antes de emitir."
        : !hasEmissionPoint
          ? ctx.cashSessionCheckError
            ? "No se pudo verificar la caja — reintente arriba antes de emitir."
            : "Debe abrir una caja antes de emitir."
          : paymentExceeds
            ? "El cobro excede el total — ajuste las formas de pago."
            : total > 0 && !paymentOk
              ? "Configure las formas de cobro para poder emitir."
              : ctx.cashInsufficient
                ? "El monto recibido en efectivo es menor al total a cobrar."
                : canSaveDraft && !ctx.editing
                  ? "Guarde el borrador primero. Luego podrá emitir la factura."
                  : canEmit && ctx.editing
                    ? `Listo para emitir ${ctx.isElectronic ? "(electrónica)" : "(física)"}.`
                    : null;

  type ItemStatus = "ok" | "missing" | "error";
  const item = (label: string, status: ItemStatus) => (
    <div className="sf-checklist__item">
      <span
        className={`material-symbols-outlined sf-checklist__icon sf-checklist__icon--${status}`}
      >
        {status === "ok"
          ? "check_circle"
          : status === "error"
            ? "error"
            : "radio_button_unchecked"}
      </span>
      <span className={`sf-checklist__label--${status}`}>{label}</span>
    </div>
  );

  return (
    <>
      <div className="sf-checklist">
        <div className="sf-checklist__title zh-section-title">
          Estado del formulario
        </div>
        {item("Cliente seleccionado", hasCustomer ? "ok" : "missing")}
        {item("Productos agregados", hasLines ? "ok" : "missing")}
        {ctx.hasInsufficientStock &&
          item("Cantidad supera el stock disponible en una línea", "error")}
        {item(
          ctx.cashSessionCheckError ? "Caja abierta (sin verificar)" : "Caja abierta",
          ctx.hasCashSession === true
            ? "ok"
            : ctx.hasCashSession === false || ctx.cashSessionCheckError
              ? "error"
              : "missing",
        )}
        {item(
          paymentExceeds ? "Cobro excede el total" : "Formas de cobro",
          paymentOk
            ? "ok"
            : paymentExceeds
              ? "error"
              : paid > 0
                ? "missing"
                : "missing",
        )}
        {ctx.cashDue > 0 &&
          item(
            "Monto recibido en efectivo",
            ctx.cashInsufficient ? "error" : "ok",
          )}
      </div>
      {nextStep && (
        <div className="sf-next-step">
          <span className="material-symbols-outlined sf-next-step__icon">
            arrow_forward
          </span>
          {nextStep}
        </div>
      )}
    </>
  );
}

// ── Emit Button with tooltip ────────────────────────────────────────────
// Único botón de acción del formulario de venta: "Nueva Venta → Emitir
// Factura → Modal de confirmación → Emisión → Pantalla de éxito" es el
// flujo completo visible al usuario. Este botón solo abre el modal
// (ctx.openIssueFlow) — toda la lógica de negocio vive en el hook.
// El atajo de teclado F8 dispara la misma acción (ver useSalesPage.ts).
function EmitButton({ ctx }: { ctx: SalesPageContext }) {
  const reasons: string[] = [];
  if (!ctx.formWatch.customerId.trim()) reasons.push("Seleccione un cliente");
  if (ctx.lines.length === 0) reasons.push("Agregue al menos un producto");
  if (ctx.hasCashSession === true && ctx.summary.total > 0 && !ctx.paymentOk)
    reasons.push("Registre formas de pago por el total de la factura");
  if (ctx.cashInsufficient)
    reasons.push("El monto recibido en efectivo es menor al total a cobrar");
  if (ctx.hasInsufficientStock)
    reasons.push("Hay una línea con cantidad mayor al stock disponible");

  return (
    <div className="sales-emit-wrap">
      <ZHBtn
        variant="cta"
        onClick={ctx.openIssueFlow}
        disabled={!ctx.canEmit}
        title={
          reasons.length > 0
            ? `No se puede emitir: ${reasons.join(", ")}`
            : undefined
        }
      >
        <span className="material-symbols-outlined zh-icon-lg">
          play_arrow
        </span>
        {ctx.isElectronic
          ? "Emitir Factura Electrónica (F8)"
          : "Emitir Factura (F8)"}
      </ZHBtn>
      {!ctx.canEmit && !ctx.fieldDisabled && reasons.length > 0 && (
        <div className="sf-save-tooltip">{reasons.join(" · ")}</div>
      )}
    </div>
  );
}

// ── Payment Methods Section (internal) ─────────────────────────────────
function PaymentMethodsSection({
  ctx,
}: {
  ctx: ReturnType<typeof useSalesPage>;
}) {
  return (
    <div className="sf-sidebar__section">
      <div className="sf-sidebar__header zh-section-title">
        <span className="material-symbols-outlined sf-sidebar__header-icon">
          payments
        </span>
        Formas de Cobro
        {!ctx.readOnly && ctx.payments.length > 0 && (
          <span
            className="material-symbols-outlined sf-sidebar__header-right zh-icon-md"
            title="Limpiar cobros"
            onClick={() => ctx.setInvoicePayments([])}
          >
            delete_sweep
          </span>
        )}
      </div>
      {ctx.readOnly ? (
        <div className="sales-payment-readonly-list">
          {(ctx.editing?.payments ?? [])
            .filter((p) => p.amount > 0)
            .map((p) => (
              <div key={p.id} className="sales-payment-chip">
                {p.paymentMethodName}{" "}
                <span className="sales-payment-chip__amount">
                  <ZHMoneyValue
                    value={p.amount}
                    decimals={getDecimalConfig().totalAmount}
                  />
                </span>
              </div>
            ))}
        </div>
      ) : (
        <>
          <div className="sales-payment-grid">
            {ctx.paymentMethods.map((pm) => {
              const entries = ctx.payments.filter(
                (ip) => ip.paymentMethodId === pm.id,
              );
              const entry = entries[0];
              const totalForMethod = entries.reduce(
                (s, e) => s + (e.amount || 0),
                0,
              );
              const hasValue = totalForMethod > 0;
              const isCredit = pm.isCreditAllowed;
              const calcRemaining = () =>
                Math.max(0, remainingToCollect(ctx, pm.id));

              return (
                <div key={pm.id} className="sales-payment-method">
                  <ZHToggleTile
                    active={hasValue}
                    disabled={ctx.fieldDisabled}
                    title={pm.name}
                    onClick={() => {
                      if (ctx.fieldDisabled) return;
                      if (isCredit) {
                        const rem = calcRemaining();
                        ctx.setCreditAmount(rem);
                        ctx.setCreditRows(ctx.simulateCreditInstallments(rem));
                        ctx.setModalCredit(true);
                      } else if (pm.requiresReference) {
                        ctx.setDetailMethodId(pm.id);
                        ctx.setDetailMethodType(pm.detailType);
                        ctx.setDetailMethodName(pm.name);
                        const existing = ctx.payments.filter(
                          (p) => p.paymentMethodId === pm.id,
                        );
                        if (existing.length > 0) {
                          ctx.setDetailRows(
                            existing.map((e, i) => ({
                              _k: i + 1,
                              amount: e.amount,
                              card:
                                pm.detailType === "Card"
                                  ? (e.cardDetail ?? {})
                                  : undefined,
                              transfer:
                                pm.detailType === "Transfer"
                                  ? (e.transferDetail ?? {})
                                  : undefined,
                              cheque:
                                pm.detailType === "Check"
                                  ? (e.chequeDetail ?? {})
                                  : undefined,
                            })),
                          );
                          ctx.setDetailKey(existing.length + 1);
                        } else {
                          ctx.setDetailRows([]);
                          ctx.setDetailKey(1);
                        }
                        ctx.setModalDetail(true);
                      } else if (!hasValue) {
                        const rem = calcRemaining();
                        if (rem > 0) {
                          ctx.setInvoicePayments((prev) => [
                            ...prev,
                            {
                              _key: ctx.payKey,
                              paymentMethodId: pm.id,
                              amount: rem,
                              reference: null,
                            },
                          ]);
                          ctx.setPayKey((k) => k + 1);
                        }
                      }
                    }}
                  />
                  {hasValue && !isCredit && !pm.requiresReference && (
                    <div className="sales-payment-amount-row">
                      <span className="sales-payment-dollar">$</span>
                      <ZhDecimalInput
                        decimals={getDecimalConfig().totalAmount}
                        positiveOnly
                        defaultValue={formatMoney(
                          entry!.amount,
                          getDecimalConfig().totalAmount,
                        )}
                        disabled={ctx.fieldDisabled}
                        onBlur={(e) => {
                          const val = Number(e.target.value) || 0;
                          if (val > 0) {
                            ctx.setInvoicePayments((prev) =>
                              prev.map((p) =>
                                p._key === entry!._key
                                  ? { ...p, amount: val }
                                  : p,
                              ),
                            );
                          } else {
                            ctx.setInvoicePayments((prev) =>
                              prev.filter((p) => p._key !== entry!._key),
                            );
                          }
                        }}
                        className="sales-payment-input"
                      />
                      <ZHIconButton
                        icon="close"
                        title="Eliminar pago"
                        variant="danger"
                        onClick={() =>
                          ctx.setInvoicePayments((prev) =>
                            prev.filter((p) => p._key !== entry!._key),
                          )
                        }
                      />
                    </div>
                  )}
                  {hasValue && pm.requiresReference && !isCredit && (
                    <span className="sales-payment-ref-amount">
                      <ZHMoneyValue
                        value={totalForMethod}
                        decimals={getDecimalConfig().totalAmount}
                      />{" "}
                      <span className="sales-payment-ref-count">
                        ({entries.length})
                      </span>
                    </span>
                  )}
                  {hasValue && isCredit && (
                    <span
                      className="sales-payment-credit-amount"
                      onClick={() => {
                        ctx.setCreditAmount(entry!.amount);
                        ctx.setCreditRows(
                          ctx.simulateCreditInstallments(entry!.amount),
                        );
                        ctx.setModalCredit(true);
                      }}
                    >
                      <ZHMoneyValue
                        value={entry!.amount}
                        decimals={getDecimalConfig().totalAmount}
                      />
                    </span>
                  )}
                </div>
              );
            })}
          </div>
          {ctx.cashDue > 0 && (
            <div
              className={`sales-cash-box${ctx.cashInsufficient ? " sales-cash-box--insufficient" : ""}`}
            >
              <div className="sales-cash-box__row">
                <span className="sales-cash-box__label">
                  Monto recibido (Efectivo):
                </span>
                <div className="sales-cash-box__input-wrap">
                  <span className="sales-cash-box__currency">$</span>
                  <ZhDecimalInput
                    decimals={getDecimalConfig().totalAmount}
                    positiveOnly
                    defaultValue={
                      ctx.cashReceived > 0
                        ? formatMoney(
                            ctx.cashReceived,
                            getDecimalConfig().totalAmount,
                          )
                        : ""
                    }
                    disabled={ctx.fieldDisabled}
                    onBlur={(e) =>
                      ctx.setCashReceived(Number(e.target.value) || 0)
                    }
                    className="sales-cash-input"
                  />
                </div>
              </div>
              <div
                className={`sales-cash-box__total-row${ctx.cashInsufficient ? " sales-cash-box__total-row--insufficient" : ""}`}
              >
                <span>{ctx.cashInsufficient ? "✗ Insuficiente" : "Vuelto:"}</span>
                <span className="sales-cash-box__amount">
                  <ZHMoneyValue
                    value={
                      ctx.cashInsufficient
                        ? ctx.cashDue - ctx.cashReceived
                        : ctx.cashChange
                    }
                    decimals={getDecimalConfig().totalAmount}
                  />
                </span>
              </div>
            </div>
          )}
          {(() => {
            const paid = ctx.paidTotal;
            const total = ctx.summary.total;
            const factor = 10 ** getDecimalConfig().totalAmount;
            const diff = Math.round((total - paid) * factor) / factor;
            const exceeds = diff < 0;
            return (
              <div
                className={`sales-summary-box${diff === 0 ? " sales-summary-box--complete" : ""}${exceeds ? " sales-summary-box--exceeds" : ""}`}
              >
                <div className="sales-summary-row">
                  <span>Total factura:</span>
                  <span className="sales-summary-row__amount">
                    <ZHMoneyValue
                      value={total}
                      decimals={getDecimalConfig().totalAmount}
                    />
                  </span>
                </div>
                <div className="sales-summary-row">
                  <span>Total cobrado:</span>
                  <span className="sales-summary-row__amount">
                    <ZHMoneyValue
                      value={paid}
                      decimals={getDecimalConfig().totalAmount}
                    />
                  </span>
                </div>
                <div
                  className={`sales-summary-total-row${diff === 0 ? " sales-summary-total-row--complete" : ""}${exceeds ? " sales-summary-total-row--exceeds" : ""}`}
                >
                  <span>
                    {diff === 0
                      ? "✓ Cobro completo"
                      : exceeds
                        ? "✗ Excede"
                        : "Pendiente:"}
                  </span>
                  {diff !== 0 && (
                    <span className="sales-summary-total-row__amount">
                      <ZHMoneyValue
                        value={Math.abs(diff)}
                        decimals={getDecimalConfig().totalAmount}
                      />
                    </span>
                  )}
                </div>
              </div>
            );
          })()}
        </>
      )}
    </div>
  );
}
