import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
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
import { useSalesPage, type SalesPageContext } from "../hooks/useSalesPage";
import { useRideActions } from "../hooks/useRideActions";
import { PAYMENT_EXCEEDS_TOLERANCE } from "../constants/tolerances";
import "../styles/sales-invoice.css";
import "../../../styles/shared/erp-form-core.css";
import "../../electronicDocuments/monitor/components/electronic-documents-monitor.css";

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
  const statusBadgeClass = (s: string) =>
    s === "Draft"
      ? "pf-badge--warning"
      : s === "Authorized"
        ? "pf-badge--success"
        : "pf-badge--danger";

  return (
    <div style={{ padding: "8px 16px 0" }}>
      {/* Solo aplica a Puntos de Emisión Electrónicos — fuente única ctx.isElectronic
          (CashRegister → EmissionPoint → EmissionType). Un Punto Físico nunca debe consultar
          ni mostrar estado de configuración SRI. */}
      {ctx.isElectronic && <ZHElectronicEnvironmentBanner />}

      {/* ── Aviso caja no abierta ────────────────────────────────── */}
      {ctx.hasCashSession === false && (
        <ZHPageNotice
          variant="warning"
          message="No tiene una caja abierta. Debe abrir una caja antes de autorizar facturas."
        />
      )}

      {/* ═══════════════════════════ LISTADO ═══════════════════════════ */}
      {ctx.tab === "listado" && (
        <div className="prd-section">
          <div
            style={{
              display: "flex",
              gap: 12,
              marginBottom: 16,
              alignItems: "center",
            }}
          >
            <button
              className="pf-btn pf-btn--primary"
              onClick={() => {
                ctx.resetForm();
                ctx.setTab("nuevo");
              }}
            >
              <span className="material-symbols-outlined pf-btn__icon">
                add
              </span>
              Nueva Factura
            </button>
            <div style={{ flex: 1 }} />
            <input
              type="text"
              placeholder="Buscar por número o cliente..."
              value={ctx.listSearch}
              onChange={(e) => ctx.setListSearch(e.target.value)}
              style={{ maxWidth: 300 }}
            />
            <ZHBtn
              variant="secondary"
              onClick={ctx.fetchList}
              disabled={ctx.listLoading}
            >
              <span
                className="material-symbols-outlined"
                style={{ fontSize: 18 }}
              >
                refresh
              </span>
            </ZHBtn>
          </div>
          {ctx.listLoading ? (
            <p>Cargando...</p>
          ) : (
            <table className="pf-table">
              <thead>
                <tr>
                  <th>Nro. Factura</th>
                  <th>Fecha</th>
                  <th>Cliente</th>
                  <th className="pf-th--right">Total</th>
                  <th className="pf-th--center">Líneas</th>
                  <th>Estado</th>
                  <th className="pf-th--center">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {ctx.listItems.map((inv) => (
                  <tr key={inv.id}>
                    <td style={{ fontFamily: "monospace", fontWeight: 600 }}>
                      {inv.invoiceNumber}
                    </td>
                    <td>{inv.issueDate}</td>
                    <td>{inv.customerName}</td>
                    <td className="pf-td--num">
                      {formatMoneyWithSymbol(
                        inv.grandTotal,
                        getDecimalConfig().totalAmount,
                      )}
                    </td>
                    <td className="pf-td--center">{inv.lineCount}</td>
                    <td>
                      <span
                        className={`pf-badge ${statusBadgeClass(inv.status)}`}
                      >
                        {statusLabel(inv.status)}
                      </span>
                    </td>
                    <td className="pf-td--center">
                      <button
                        className="pf-row-action"
                        onClick={() => void ctx.loadForEdit(inv.id)}
                        title={
                          inv.status === "Draft"
                            ? "Reintentar emisión"
                            : "Ver / Editar"
                        }
                      >
                        <span
                          className="material-symbols-outlined"
                          style={{ fontSize: 20 }}
                        >
                          {inv.status === "Draft" ? "replay" : "edit"}
                        </span>
                      </button>
                      {inv.status === "Authorized" && (
                        <button
                          className="pf-row-action"
                          title="Ver Movimiento de Inventario"
                          onClick={() =>
                            navigate(
                              `/inventory/kardex?docId=${inv.id}&docType=SalesInvoice`,
                            )
                          }
                        >
                          <span
                            className="material-symbols-outlined"
                            style={{ fontSize: 20 }}
                          >
                            history
                          </span>
                        </button>
                      )}
                      {inv.status === "Authorized" && (
                        <button
                          className="pf-row-action"
                          title="Ver RIDE"
                          disabled={ride.ridePending}
                          onClick={() => void ride.handleViewRide(inv.id)}
                        >
                          <span
                            className="material-symbols-outlined"
                            style={{ fontSize: 20 }}
                          >
                            picture_as_pdf
                          </span>
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
                {ctx.listItems.length === 0 && (
                  <tr>
                    <td colSpan={7} className="pf-table-empty">
                      Sin facturas registradas.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
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
                  ctx.resetForm();
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
                    style={{
                      width: "100%",
                      fontSize: 12,
                      padding: "5px 8px",
                      border: "1.5px solid var(--color-border)",
                      borderRadius: 6,
                      marginBottom: 4,
                    }}
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
                    style={{
                      width: "100%",
                      fontSize: 12,
                      padding: "5px 8px",
                      border: "1.5px solid var(--color-border)",
                      borderRadius: 6,
                      marginBottom: 4,
                    }}
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
                  <div style={{ marginTop: 4 }}>
                    <span className="sf-emission__label">Nro: </span>
                    <span
                      className="sf-emission__value"
                      style={{ fontFamily: "monospace" }}
                    >
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
                <div
                  style={{
                    marginTop: 6,
                    fontSize: 11,
                    color: "var(--color-text-secondary)",
                    lineHeight: 1.6,
                  }}
                >
                  {ctx.customerProfile.address && (
                    <div
                      style={{ display: "flex", alignItems: "center", gap: 4 }}
                    >
                      <span
                        className="material-symbols-outlined"
                        style={{ fontSize: 14 }}
                      >
                        location_on
                      </span>
                      {ctx.customerProfile.address}
                    </div>
                  )}
                  {ctx.customerProfile.email && (
                    <div
                      style={{ display: "flex", alignItems: "center", gap: 4 }}
                    >
                      <span
                        className="material-symbols-outlined"
                        style={{ fontSize: 14 }}
                      >
                        mail
                      </span>
                      {ctx.customerProfile.email}
                    </div>
                  )}
                  {ctx.customerProfile.phone && (
                    <div
                      style={{ display: "flex", alignItems: "center", gap: 4 }}
                    >
                      <span
                        className="material-symbols-outlined"
                        style={{ fontSize: 14 }}
                      >
                        phone
                      </span>
                      {ctx.customerProfile.phone}
                    </div>
                  )}
                  {!ctx.fieldDisabled && (
                    <button
                      type="button"
                      onClick={ctx.openEditCustomerModal}
                      style={{
                        marginTop: 4,
                        background: "none",
                        border: "none",
                        cursor: "pointer",
                        color: "var(--color-primary)",
                        fontSize: 11,
                        fontWeight: 600,
                        padding: 0,
                        display: "flex",
                        alignItems: "center",
                        gap: 3,
                      }}
                    >
                      <span
                        className="material-symbols-outlined"
                        style={{ fontSize: 14 }}
                      >
                        edit
                      </span>
                      Editar datos
                    </button>
                  )}
                </div>
              )}
            </div>

            {/* Resumen Impuestos + Total */}
            <div
              className="sf-sidebar__section"
              style={{ padding: "12px 16px" }}
            >
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
                  <div
                    className="sf-summary__discount-total"
                    style={{ marginTop: 8 }}
                  >
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
                <div className="sf-total-box__header" style={{ marginTop: 10 }}>
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
                  <span
                    className="material-symbols-outlined"
                    style={{
                      fontSize: 14,
                      verticalAlign: "middle",
                      marginRight: 4,
                    }}
                  >
                    qr_code_2
                  </span>
                  Clave de Acceso SRI
                </span>
                <span className="sf-bottombar__sri-key">
                  {ctx.editing?.accessKey ?? "— se genera al emitir —"}
                </span>
                {ctx.editing && (
                  <button
                    type="button"
                    className="edm-icon-btn"
                    onClick={() => setSriDiagnosticOpen(true)}
                    aria-label="Ver diagnóstico SRI"
                    title="Ver diagnóstico SRI"
                  >
                    <span className="material-symbols-outlined zh-icon-sm">
                      troubleshoot
                    </span>
                  </button>
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
                className="sf-bottombar__btn"
                onClick={() => ctx.setModalCancelReason(true)}
                style={{
                  background: "var(--color-error)",
                  color: "#fff",
                  borderColor: "var(--color-error)",
                }}
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
  const canEmit = canSaveDraft && hasEmissionPoint && paymentOk;

  const nextStep = !hasCustomer
    ? "Seleccione un cliente para comenzar."
    : !hasLines
      ? "Agregue productos a la factura."
      : !hasEmissionPoint
        ? "Debe abrir una caja antes de emitir."
        : paymentExceeds
          ? "El cobro excede el total — ajuste las formas de pago."
          : total > 0 && !paymentOk
            ? "Configure las formas de cobro para poder emitir."
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
          "Caja abierta",
          ctx.hasCashSession === true
            ? "ok"
            : ctx.hasCashSession === false
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

  return (
    <div style={{ position: "relative" }}>
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
            className="material-symbols-outlined sf-sidebar__header-right"
            title="Limpiar cobros"
            style={{ cursor: "pointer", fontSize: 16 }}
            onClick={() => ctx.setInvoicePayments([])}
          >
            delete_sweep
          </span>
        )}
      </div>
      {ctx.readOnly ? (
        <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
          {(ctx.editing?.payments ?? [])
            .filter((p) => p.amount > 0)
            .map((p) => (
              <div
                key={p.id}
                style={{
                  padding: "6px 10px",
                  borderRadius: 6,
                  border: "1.5px solid var(--color-primary)",
                  background: "var(--color-primary)",
                  color: "#fff",
                  fontSize: 11,
                  fontWeight: 700,
                }}
              >
                {p.paymentMethodName}{" "}
                <span style={{ fontFamily: "monospace" }}>
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
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fill, minmax(130px, 1fr))",
              gap: 6,
            }}
          >
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
                  style={{
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    gap: 2,
                    padding: "8px 6px",
                    borderRadius: 8,
                    fontSize: 10,
                    fontWeight: 700,
                    textTransform: "uppercase",
                    transition: "all 0.15s",
                    border: hasValue
                      ? "2px solid var(--color-primary)"
                      : "1.5px solid var(--color-border)",
                    background: hasValue ? "var(--color-primary)" : "#fff",
                    color: hasValue ? "#fff" : "var(--color-text-primary)",
                  }}
                >
                  <button
                    type="button"
                    disabled={ctx.fieldDisabled}
                    style={{
                      fontSize: 11,
                      cursor: "pointer",
                      width: "100%",
                      textAlign: "center",
                      background: "none",
                      border: "none",
                      padding: 0,
                      margin: 0,
                      font: "inherit",
                      fontWeight: "inherit",
                      textTransform: "inherit",
                      color: "inherit",
                    }}
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
                    <div
                      style={{ display: "flex", alignItems: "center", gap: 2 }}
                    >
                      <span
                        style={{ color: "rgba(255,255,255,0.7)", fontSize: 11 }}
                      >
                        $
                      </span>
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
                        style={{
                          width: 70,
                          fontSize: 13,
                          fontFamily: "monospace",
                          fontWeight: 800,
                          textAlign: "center",
                          padding: "2px 4px",
                          border: "1px solid rgba(255,255,255,0.4)",
                          borderRadius: 4,
                          background: "rgba(255,255,255,0.15)",
                          color: "#fff",
                        }}
                      />
                      <button
                        type="button"
                        onClick={() =>
                          ctx.setInvoicePayments((prev) =>
                            prev.filter((p) => p._key !== entry!._key),
                          )
                        }
                        style={{
                          background: "none",
                          border: "none",
                          cursor: "pointer",
                          color: "rgba(255,255,255,0.7)",
                          fontSize: 14,
                          padding: 0,
                          lineHeight: 1,
                        }}
                      >
                        ×
                      </button>
                    </div>
                  )}
                  {hasValue && pm.requiresReference && !isCredit && (
                    <span
                      style={{
                        fontSize: 12,
                        fontFamily: "monospace",
                        fontWeight: 800,
                      }}
                    >
                      {formatMoneyWithSymbol(
                        totalForMethod,
                        getDecimalConfig().totalAmount,
                      )}{" "}
                      <span
                        style={{ fontSize: 9, fontWeight: 400, opacity: 0.8 }}
                      >
                        ({entries.length})
                      </span>
                    </span>
                  )}
                  {hasValue && isCredit && (
                    <span
                      style={{
                        fontSize: 13,
                        fontFamily: "monospace",
                        fontWeight: 800,
                        cursor: "pointer",
                      }}
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
          {(() => {
            const paid = ctx.payments.reduce((s, p) => s + (p.amount || 0), 0);
            const total = ctx.summary.total;
            const factor = 10 ** getDecimalConfig().totalAmount;
            const diff = Math.round((total - paid) * factor) / factor;
            const exceeds = diff < 0;
            return (
              <div
                style={{
                  fontSize: 11,
                  marginTop: 6,
                  padding: "6px 8px",
                  borderRadius: 6,
                  background:
                    diff === 0 ? "#f0fdf4" : exceeds ? "#fef2f2" : "#f8fafc",
                  border: exceeds
                    ? "1.5px solid #dc2626"
                    : "1px solid var(--color-border)",
                }}
              >
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    color: "var(--color-text-secondary)",
                  }}
                >
                  <span>Total factura:</span>
                  <span style={{ fontFamily: "monospace" }}>
                    {formatMoneyWithSymbol(
                      total,
                      getDecimalConfig().totalAmount,
                    )}
                  </span>
                </div>
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    color: "var(--color-text-secondary)",
                  }}
                >
                  <span>Total cobrado:</span>
                  <span style={{ fontFamily: "monospace" }}>
                    {formatMoneyWithSymbol(
                      paid,
                      getDecimalConfig().totalAmount,
                    )}
                  </span>
                </div>
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    fontWeight: 700,
                    marginTop: 2,
                    paddingTop: 2,
                    borderTop: "1px solid var(--color-border)",
                    color:
                      diff === 0
                        ? "#16a34a"
                        : exceeds
                          ? "#dc2626"
                          : "var(--color-text-primary)",
                  }}
                >
                  <span>
                    {diff === 0
                      ? "✓ Cobro completo"
                      : exceeds
                        ? "✗ Excede"
                        : "Pendiente:"}
                  </span>
                  {diff !== 0 && (
                    <span style={{ fontFamily: "monospace" }}>
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
