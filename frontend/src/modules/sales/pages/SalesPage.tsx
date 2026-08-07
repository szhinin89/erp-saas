import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZHPromptModal } from "../../../components/zh/ZHConfirmModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHElectronicEnvironmentBanner } from "../../../components/zh/ZHElectronicEnvironmentBanner";
import { formatMoney, formatMoneyWithSymbol } from "../../../lib/sanitizers";
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
            <input
              type="text"
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
                      {formatMoneyWithSymbol(
                        inv.grandTotal,
                        getDecimalConfig().totalAmount,
                      )}
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
            <div className="sf-tabs">
              <button className="sf-tab sf-tab--active">
                <span className="material-symbols-outlined sf-tab__icon">
                  receipt_long
                </span>
                {ctx.editing ? "Editar Factura" : "Nueva Factura"}
              </button>
              <button
                className="sf-tab"
                onClick={() => {
                  void ctx.resetForm();
                  ctx.setTab("listado");
                }}
              >
                <span className="material-symbols-outlined sf-tab__icon">
                  history
                </span>
                Historial
              </button>
            </div>

            {/* Checklist + Next Step (only in draft mode) */}
            {ctx.isDraft && !ctx.readOnly && <SalesFormChecklist ctx={ctx} />}

            {/* Datos de Emisión */}
            <div className="sf-sidebar__section">
              <div className="sf-sidebar__header">
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
                    <span className="sf-emission__label">Sucursal: </span>
                    <span className="sf-emission__value">{ctx.branchName}</span>
                  </div>
                )}
                {ctx.myCashSession && (
                  <div>
                    <span className="sf-emission__label">Caja: </span>
                    <span className="sf-emission__value">
                      {ctx.myCashSession.cashRegisterCodeSnapshot} —{" "}
                      {ctx.myCashSession.cashRegisterNameSnapshot}
                    </span>
                  </div>
                )}
                {ctx.myCashSession && (
                  <div>
                    <span className="sf-emission__label">Punto: </span>
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
                    <span className="sf-emission__label">Tipo Emisión: </span>
                    <span className="sf-emission__value">
                      {(ctx.myCashSession?.emissionType ??
                        ctx.editing?.emissionType) === "Electronic"
                        ? "🟢 Electrónica"
                        : "🔵 Física"}
                    </span>
                  </div>
                )}
                <div>
                  <div className="sf-emission__label">Tipo Documento</div>
                  <select
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
                  </select>
                </div>
                <div>
                  <div className="sf-emission__label">Forma Pago SRI</div>
                  <select
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
                  </select>
                </div>
                {ctx.editing && (
                  <div className="zh-mt-4">
                    <span className="sf-emission__label">Nro: </span>
                    <span className="sf-emission__value zh-font-mono">
                      {ctx.editing.invoiceNumber}
                    </span>
                  </div>
                )}
              </div>
            </div>

            {/* Cliente */}
            <div className="sf-sidebar__section">
              <div className="sf-sidebar__header">
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
                  {!ctx.fieldDisabled && (
                    <button
                      type="button"
                      onClick={ctx.openEditCustomerModal}
                      className="zh-inline-action zh-mt-4"
                    >
                      <span className="material-symbols-outlined zh-icon-sm">
                        edit
                      </span>
                      Editar datos
                    </button>
                  )}
                </div>
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
                          {formatMoneyWithSymbol(
                            e.base,
                            getDecimalConfig().totalAmount,
                          )}
                        </td>
                        <td>
                          {formatMoneyWithSymbol(
                            e.tax,
                            getDecimalConfig().totalAmount,
                          )}
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
                      {formatMoneyWithSymbol(
                        ctx.totalDiscount,
                        getDecimalConfig().totalAmount,
                      )}
                    </span>
                  </div>
                )}
                <div className="sf-total-box__header zh-mt-10">
                  <span className="sf-total-box__label">Total a Cobrar</span>
                </div>
                <div className="sf-total-box__amount">
                  {formatMoneyWithSymbol(
                    ctx.grandTotal,
                    getDecimalConfig().totalAmount,
                  )}
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
            {ctx.saveError && (
              <div className="sf-sidebar__section">
                <ZHPageNotice variant="error" message={ctx.saveError} />
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
                <span className="sf-bottombar__sri-key">
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

            <button
              type="button"
              className="sf-bottombar__btn"
              onClick={() => void ctx.clearForm()}
            >
              <span className="material-symbols-outlined sf-bottombar__btn-icon">
                delete_sweep
              </span>
              Limpiar Todo
            </button>

            {ctx.isDraft && <EmitButton ctx={ctx} />}

            {ctx.editing &&
              ctx.isElectronic &&
              ctx.editing.status === "Authorized" &&
              ctx.editing.electronicStatus === "None" && (
                <button
                  className="sf-bottombar__btn"
                  onClick={() => void ctx.handleGenerateElectronicDocument()}
                  disabled={ctx.saving}
                  title="Esta factura fue autorizada pero nunca generó su documento electrónico — regenera el registro en el Monitor."
                >
                  <span className="material-symbols-outlined sf-bottombar__btn-icon">
                    bolt
                  </span>
                  Generar documento electrónico
                </button>
              )}

            {ctx.editing && ctx.editing.status === "Authorized" && (
              <button
                className="sf-bottombar__btn"
                disabled={ride.ridePending}
                onClick={() => void ride.handleViewRide(ctx.editing!.id)}
                title="Abre el PDF del RIDE en una pestaña nueva. Reutiliza el ya generado si no cambió nada."
              >
                <span className="material-symbols-outlined sf-bottombar__btn-icon">
                  picture_as_pdf
                </span>
                Ver RIDE
              </button>
            )}

            {ctx.editing && ctx.editing.status === "Authorized" && (
              <button
                className="sf-bottombar__btn"
                disabled={ride.ridePending}
                onClick={() =>
                  void ride.handleDownloadRide(
                    ctx.editing!.id,
                    ctx.editing!.invoiceNumber,
                  )
                }
              >
                <span className="material-symbols-outlined sf-bottombar__btn-icon">
                  download
                </span>
                Descargar RIDE
              </button>
            )}

            {ctx.editing && ctx.editing.status === "Authorized" && (
              <button
                className="sf-bottombar__btn"
                disabled={ride.ridePending}
                onClick={() => void ride.handleRegenerateRide(ctx.editing!.id)}
                title="Fuerza una nueva generación del RIDE aunque el ya almacenado siga siendo válido."
              >
                <span className="material-symbols-outlined sf-bottombar__btn-icon">
                  refresh
                </span>
                Regenerar RIDE
              </button>
            )}

            {ctx.editing && ctx.editing.status === "Authorized" && (
              <button
                className="sf-bottombar__btn sales-bottombar-btn--danger"
                onClick={() => ctx.setModalCancelReason(true)}
              >
                <span className="material-symbols-outlined sf-bottombar__btn-icon">
                  block
                </span>
                Anular
              </button>
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
        available={
          Math.round(
            (ctx.summary.total -
              ctx.payments
                .filter((p) => p.paymentMethodId !== ctx.detailMethodId)
                .reduce((s, p) => s + (p.amount || 0), 0)) *
              10 ** getDecimalConfig().totalAmount,
          ) /
          10 ** getDecimalConfig().totalAmount
        }
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
  const paid = ctx.payments.reduce((s, p) => s + (p.amount || 0), 0);
  const total = ctx.summary.total;
  const paymentOk = ctx.paymentOk;
  const paymentExceeds = paid > total + PAYMENT_EXCEEDS_TOLERANCE;

  const canSaveDraft = hasCustomer && hasLines;
  const canEmit =
    canSaveDraft && hasEmissionPoint && paymentOk && !ctx.cashInsufficient;

  const nextStep = !hasCustomer
    ? "Seleccione un cliente para comenzar."
    : !hasLines
      ? "Agregue productos a la factura."
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
        <div className="sf-checklist__title">Estado del formulario</div>
        {item("Cliente seleccionado", hasCustomer ? "ok" : "missing")}
        {item("Productos agregados", hasLines ? "ok" : "missing")}
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

  return (
    <div className="sales-emit-wrap">
      <button
        className="sf-bottombar__emit"
        onClick={ctx.openIssueFlow}
        disabled={!ctx.canEmit}
        title={
          reasons.length > 0
            ? `No se puede emitir: ${reasons.join(", ")}`
            : undefined
        }
      >
        <span className="material-symbols-outlined sf-bottombar__emit-icon">
          play_arrow
        </span>
        {ctx.isElectronic
          ? "Emitir Factura Electrónica (F8)"
          : "Emitir Factura (F8)"}
      </button>
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
      <div className="sf-sidebar__header">
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
                  {formatMoneyWithSymbol(
                    p.amount,
                    getDecimalConfig().totalAmount,
                  )}
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
              const calcRemaining = () => {
                const factor = 10 ** getDecimalConfig().totalAmount;
                return Math.max(
                  0,
                  Math.round(
                    (ctx.summary.total -
                      ctx.payments
                        .filter((p) => p.paymentMethodId !== pm.id)
                        .reduce((s, p) => s + (p.amount || 0), 0)) *
                      factor,
                  ) / factor,
                );
              };

              return (
                <div
                  key={pm.id}
                  className={`sales-payment-method${hasValue ? " sales-payment-method--active" : ""}`}
                >
                  <button
                    type="button"
                    disabled={ctx.fieldDisabled}
                    className="sales-payment-method__btn"
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
                  >
                    {pm.name}
                  </button>
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
                      {formatMoneyWithSymbol(
                        totalForMethod,
                        getDecimalConfig().totalAmount,
                      )}{" "}
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
                      {formatMoneyWithSymbol(
                        entry!.amount,
                        getDecimalConfig().totalAmount,
                      )}
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
                  {formatMoneyWithSymbol(
                    ctx.cashInsufficient
                      ? ctx.cashDue - ctx.cashReceived
                      : ctx.cashChange,
                    getDecimalConfig().totalAmount,
                  )}
                </span>
              </div>
            </div>
          )}
          {(() => {
            const paid = ctx.payments.reduce((s, p) => s + (p.amount || 0), 0);
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
                    {formatMoneyWithSymbol(
                      total,
                      getDecimalConfig().totalAmount,
                    )}
                  </span>
                </div>
                <div className="sales-summary-row">
                  <span>Total cobrado:</span>
                  <span className="sales-summary-row__amount">
                    {formatMoneyWithSymbol(
                      paid,
                      getDecimalConfig().totalAmount,
                    )}
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
                      {formatMoneyWithSymbol(
                        Math.abs(diff),
                        getDecimalConfig().totalAmount,
                      )}
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
