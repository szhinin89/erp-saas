import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHTabBar, type ZHTab } from "../../../components/zh/ZHTabBar";
import { ZhTextInput, ZhSelect } from "../../../components/zh/inputs";
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
import { CashSessionNotice } from "../components/CashSessionNotice";
import { SalesFormChecklist } from "../components/SalesFormChecklist";
import { EmitButton } from "../components/EmitButton";
import { PaymentMethodsSection } from "../components/PaymentMethodsSection";
import { remainingToCollect } from "../components/paymentRemaining";
import { useSalesPage } from "../hooks/useSalesPage";
import { useRideActions } from "../hooks/useRideActions";
import { useDocumentTitle } from "../../../hooks/useDocumentTitle";
import "../styles/sales-invoice.css";
import "../../../styles/shared/erp-form-core.css";
import "../../electronicDocuments/monitor/components/electronic-documents-monitor.css";
import "./SalesPage.css";

export function SalesPage() {
  const ctx = useSalesPage();
  const ride = useRideActions();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const openedFromParam = useRef(false);
  const [sriDiagnosticOpen, setSriDiagnosticOpen] = useState(false);

  // Esta pantalla tiene layout propio (sf-layout) y no pasa por PageShell, así que
  // debe sincronizar el título de la pestaña explícitamente. Los textos de esta
  // página son literales en español (misma convención que "Nueva Factura" abajo).
  useDocumentTitle(
    ctx.editing
      ? `Factura ${ctx.editing.invoiceNumber}`
      : "Punto de venta",
  );

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

            {/* Cliente — prioridad visual sobre Datos de Emisión: es el primer dato
                obligatorio y accionable del cajero (SALES-POS-UI-REFINE-01). */}
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

            {/* Datos de Emisión — informativo/no accionable (el servidor resuelve Caja/Punto/
                Sucursal desde ICurrentCashSession), compactado en grilla de 2 columnas
                (SALES-POS-UI-REFINE-01) para ceder espacio prioritario a Cliente arriba. */}
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
              onUpdateLinePresentation={ctx.onUpdateLinePresentation}
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
        cashDue={ctx.cashDue}
        cashReceived={ctx.cashReceived}
        cashChange={ctx.cashChange}
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
