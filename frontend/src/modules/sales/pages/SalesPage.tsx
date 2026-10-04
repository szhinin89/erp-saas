import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZhTextInput } from "../../../components/zh/inputs";
import { ZHConfirmModal, ZHPromptModal } from "../../../components/zh/ZHConfirmModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHElectronicEnvironmentBanner } from "../../../components/zh/ZHElectronicEnvironmentBanner";
import { ZHSectionHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { formatMoney } from "../../../lib/sanitizers";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { CustomerPicker } from "../components/CustomerPicker";
import { SalesPriceListContext } from "../components/SalesPriceListContext";
import { SalesRepricingTable } from "../components/SalesRepricingTable";
import { SalesInvoiceDetailsSection } from "../components/SalesInvoiceDetailsSection";
import { PaymentDetailModal } from "../components/PaymentDetailModal";
import { CreditSimulatorModal } from "../components/CreditSimulatorModal";
import { QuickCustomerModal } from "../components/QuickCustomerModal";
import { SalesElectronicDiagnosticDrawer } from "../components/SalesElectronicDiagnosticDrawer";
import { SalesIssueModal } from "../components/SalesIssueModal";
import { CashSessionNotice } from "../components/CashSessionNotice";
import { ManualCashMovementModal } from "../../caja/facades/manualCashMovementFacade";
import { SalesFormChecklist } from "../components/SalesFormChecklist";
import { SalesOperationalHeader } from "../components/SalesOperationalHeader";
import { EmitButton } from "../components/EmitButton";
import { PaymentMethodsSection } from "../components/PaymentMethodsSection";
import { remainingToCollect } from "../components/paymentRemaining";
import { deriveDetailReference } from "../utils/paymentDetailReference";
import { useSalesPage } from "../hooks/useSalesPage";
import { issueStepsFor } from "../utils/salesIssueSteps";
import type { SalesListItemDto } from "../api/salesService";
import { useRideActions } from "../hooks/useRideActions";
import { useDocumentTitle } from "../../../hooks/useDocumentTitle";
import "../styles/sales-invoice.css";
import "../../../styles/shared/erp-form-core.css";
import "./SalesPage.css";

/** ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: solo presentación — el valor real
 * (`sriAvailability`) lo calcula el backend, nunca se recalcula acá. "Unknown" cubre tanto "aún
 * no se verificó" como el caso sin datos todavía cargados. */
const SRI_AVAILABILITY_BADGE: Record<
  "Available" | "Unavailable" | "Unknown",
  { label: string; variant: BadgeVariant }
> = {
  Available: { label: "SRI disponible", variant: "success" },
  Unavailable: { label: "SRI no disponible", variant: "warning" },
  Unknown: { label: "SRI no verificado", variant: "neutral" },
};

export function SalesPage() {
  const ctx = useSalesPage();
  const ride = useRideActions();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const openedFromParam = useRef(false);
  const [sriDiagnosticOpen, setSriDiagnosticOpen] = useState(false);
  const moneyDecimals = usePrecisionDecimals("money"); // texto de ayuda (04E)

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

  // ZH-LISTING-MAIN-ROW-NUMBER-FIX-07: showRowNumber activo — "Nro. Factura" sigue siendo el
  // identificador funcional del documento; "N°" es solo el índice visual de fila (primera
  // columna), ambos coexisten en el listado principal.
  const salesListColumns: ZHDataTableColumn<SalesListItemDto>[] = [
    {
      key: "invoiceNumber",
      header: "Nro. Factura",
      render: (inv) => <span className="sales-page-invoice-number zh-font-mono">{inv.invoiceNumber}</span>,
    },
    { key: "date", header: "Fecha", render: (inv) => inv.issueDate },
    { key: "customer", header: "Cliente", render: (inv) => inv.customerName },
    {
      key: "total",
      header: "Total",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (inv) => <ZHMoneyValue value={inv.grandTotal} precision="money" />,
    },
    { key: "lines", header: "Líneas", align: "center", render: (inv) => inv.lineCount },
    {
      key: "status",
      header: "Estado",
      render: (inv) => <Badge variant={statusBadgeVariant(inv.status)} label={statusLabel(inv.status)} />,
    },
    {
      key: "actions",
      header: "Acciones",
      align: "center",
      render: (inv) => (
        <>
          <ZHIconButton
            icon={inv.status === "Draft" ? "replay" : "edit"}
            title={`${inv.status === "Draft" ? "Reintentar emisión de" : "Ver / Editar"} factura ${inv.invoiceNumber}`}
            ariaLabel={`${inv.status === "Draft" ? "Reintentar emisión de" : "Ver / Editar"} factura ${inv.invoiceNumber}`}
            onClick={() => void ctx.loadForEdit(inv.id)}
          />
          {inv.status === "Authorized" && (
            <ZHIconButton
              icon="history"
              title="Ver Movimiento de Inventario"
              ariaLabel={`Ver movimiento de inventario de factura ${inv.invoiceNumber}`}
              onClick={() => navigate(`/inventory/kardex?docId=${inv.id}&docType=SalesInvoice`)}
            />
          )}
          {/* POS-EMISSION-VISIBILITY-01: el RIDE solo existe para facturas electrónicas — se
              decide por el snapshot de la factura, nunca por la caja abierta actual. */}
          {inv.status === "Authorized" && inv.emissionType === "Electronic" && (
            <ZHIconButton
              icon="picture_as_pdf"
              title="Ver RIDE"
              ariaLabel={`Ver RIDE de factura ${inv.invoiceNumber}`}
              disabled={ride.ridePending}
              onClick={() => void ride.handleViewRide(inv.id)}
            />
          )}
        </>
      ),
    },
  ];

  return (
    // POS-VIEWPORT-LAYOUT-01: en "Nueva/Editar factura" la página ocupa exactamente el alto
    // disponible (opt-in del shell `.shell-fill-viewport`): la página no hace scroll, solo el
    // detalle de líneas. El historial (listado) conserva el scroll normal de página.
    <div
      className={
        ctx.tab === "nuevo"
          ? "sales-page-root sales-page-root--pos shell-fill-viewport"
          : "sales-page-root"
      }
    >
      {/* Solo aplica a ventas Electrónicas — fuente única ctx.isElectronic. En el POS el ambiente
          se muestra compacto en el header operativo (POS-OPERATIONAL-HEADER-01).
          (POS-EMISSION-TYPE-SNAPSHOT-01: snapshot de la factura si ya existe; si es venta nueva,
          CashRegister → EmissionPoint → EmissionType). Una venta Física nunca debe consultar ni
          mostrar estado de configuración SRI. */}
      {ctx.isElectronic && ctx.tab !== "nuevo" && <ZHElectronicEnvironmentBanner />}

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
          <ZHDataTable
            columns={salesListColumns}
            rows={ctx.listItems}
            rowKey={(inv) => inv.id}
            loading={ctx.listLoading}
            showRowNumber
            tableClassName="table--compact table--neutral"
            emptyMessage="Sin facturas registradas."
          />
        </div>
      )}

      {/* ═══════════════════════════ NUEVO / EDITAR (POS Layout) ═══════════════════════════ */}
      {ctx.tab === "nuevo" && (
        <div className="sf-layout">
          {/* POS-OPERATIONAL-HEADER-01: header operativo compacto — pestañas Nueva Factura /
              Historial + contexto fiscal (sucursal, establecimiento, punto, tipo de emisión,
              ambiente si es electrónica) + Configuración. Siempre visible, primera fila del
              layout. Reemplaza la tarjeta "Configuración de venta" del panel izquierdo. */}
          <SalesOperationalHeader ctx={ctx} />

          {/* ── SIDEBAR — Cliente + Cobro (POS-OPERATIONAL-HEADER-01) ── */}
          <div className="sf-sidebar">
            {/* Cliente */}
            <div className="sf-sidebar__section sf-sidebar__section--customer">
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
                  disabled={ctx.fieldDisabled || ctx.repricingLoading}
                  onCreateNew={ctx.openNewCustomerModal}
                  onEditSelected={
                    ctx.customerProfile ? ctx.openEditCustomerModal : undefined
                  }
                  editLabel="Editar datos"
                />
              </ZHField>
              <SalesPriceListContext state={ctx.priceListHeader} />
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
              {ctx.isConsumerFinalCustomer &&
                ctx.consumerFinalPolicy &&
                (ctx.consumerFinalAmountExceeded ? (
                  <ZHPageNotice
                    variant="error"
                    message={ctx.consumerFinalPolicy.amountExceededMessage}
                  />
                ) : (
                  <ZHSectionHelp
                    className="sales-consumer-final-help"
                    helpKey={HELP_KEYS.SALES_CUSTOMER_CONSUMER_FINAL}
                    variables={{
                      maxConsumerFinalAmount: formatMoney(
                        ctx.consumerFinalPolicy.consumerFinalMaxAmount,
                        moneyDecimals,
                      ),
                    }}
                  />
                ))}
            </div>

            {/* Total a cobrar (protagonista del flujo B) + desglose de impuestos compacto */}
            <div className="sf-sidebar__section sales-form-tax-section">
              <div className="sf-total-box">
                <div className="sf-total-box__header">
                  <span className="sf-total-box__label zh-section-title">
                    Total a Cobrar
                  </span>
                </div>
                <div className="sf-total-box__amount">
                  <ZHMoneyValue
                    value={ctx.grandTotal}
                    precision="money"
                    emphasis="total"
                  />
                </div>
                <table className="sf-tax-table sf-tax-table--compact">
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
                          <ZHMoneyValue value={e.base} precision="money" />
                        </td>
                        <td>
                          <ZHMoneyValue value={e.tax} precision="tax" />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {ctx.totalDiscount > 0 && (
                  <div className="sf-summary__discount-total">
                    <span>Descuento:</span>
                    <span>
                      -
                      <ZHMoneyValue value={ctx.totalDiscount} precision="money" />
                    </span>
                  </div>
                )}
              </div>
            </div>

            {/* Formas de Cobro → Efectivo recibido → Resultado (flujo B) */}
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
              iceRates={ctx.iceRatesMap}
              focusSignal={ctx.productSearchFocusKey}
              customerId={ctx.formWatch.customerId || undefined}
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
                {/* ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: estado discreto de
                    conectividad SRI, verificado al entrar a esta pantalla (ver
                    refreshSriConnectivity en useSalesPage.ts) — nunca bloquea la captura de
                    productos ni la emisión, solo informa. */}
                <Badge
                  label={SRI_AVAILABILITY_BADGE[ctx.sriAvailability].label}
                  variant={SRI_AVAILABILITY_BADGE[ctx.sriAvailability].variant}
                  size="md"
                />
              </div>
            )}

            {/* POS-OPERATIONAL-HEADER-01: "Siguiente paso" / "Listo para emitir" en una línea,
                junto a Emitir (misma fuente ctx.emitBlockers / ctx.canEmit). */}
            {ctx.isDraft && !ctx.readOnly && <SalesFormChecklist ctx={ctx} compact />}

            {/* POS-VIEWPORT-LAYOUT-01: contador discreto de líneas (no es un KPI). */}
            {ctx.lines.length > 0 && (
              <span className="sf-bottombar__count">
                {ctx.lines.length === 1 ? "1 producto" : `${ctx.lines.length} productos`}
              </span>
            )}

            <div className="sf-bottombar__spacer" />

            <div className="sf-bottombar__primary-actions">
              {/* SALES-MANUAL-CASH-MOVEMENT-INTEGRATION-08 — misma regla de disponibilidad que
                  /treasury/cash (empresa lo permite + usuario tiene caja.record + turno abierto),
                  resuelta por el mismo hook compartido; nunca decidida aquí. `myCashSession` no
                  nulo ya significa "hay un turno abierto para este usuario" (ver useSalesPage.ts,
                  GET /cash-sessions/my). Fail-closed: cualquier condición en falso/cargando/error
                  oculta el botón. */}
              {ctx.manualCashMovement.allowManualMovements &&
                ctx.manualCashMovement.canRecordManualMovements &&
                ctx.myCashSession?.status === "Open" && (
                  <ZHBtn
                    type="button"
                    variant="secondary"
                    size="sm"
                    onClick={ctx.manualCashMovement.openMovementModal}
                  >
                    <span className="material-symbols-outlined zh-icon-lg">
                      add
                    </span>
                    Movimiento de caja
                  </ZHBtn>
                )}

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

            {/* POS-EMISSION-VISIBILITY-01: acciones secundarias = solo electrónicas (diagnóstico SRI,
                RIDE). Una venta física no tiene ninguna. */}
            {ctx.editing && ctx.isElectronic && (
              <div
                className="sf-bottombar__secondary-actions"
                aria-label="Acciones secundarias de factura"
              >
                {ctx.editing && ctx.isElectronic && (
                  <ZHIconButton
                    icon="troubleshoot"
                    title="Ver diagnóstico SRI"
                    onClick={() => setSriDiagnosticOpen(true)}
                  />
                )}

                {ctx.editing && ctx.isElectronic && ctx.editing.status === "Authorized" && (
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

                {ctx.editing && ctx.isElectronic && ctx.editing.status === "Authorized" && (
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

                {ctx.editing && ctx.isElectronic && ctx.editing.status === "Authorized" && (
                  <ZHBtn
                    variant="secondary"
                    size="sm"
                    disabled={ride.ridePending}
                    onClick={() =>
                      void ride.handleRegenerateRide(ctx.editing!.id)
                    }
                    title="Fuerza una nueva generación del RIDE aunque el ya almacenado siga siendo válido."
                  >
                    <span className="material-symbols-outlined zh-icon-lg">
                      refresh
                    </span>
                    Regenerar RIDE
                  </ZHBtn>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {/* ═══════════════════════════ MODALS ═══════════════════════════ */}

      {/* SALES-MANUAL-CASH-MOVEMENT-INTEGRATION-08 — exactamente el mismo componente que usa
          /treasury/cash (ManualCashMovementModal), controlado por el mismo hook compartido
          (useManualCashMovementFlow, vía ctx.manualCashMovement) — cero lógica de negocio nueva
          en Sales, mismo endpoint/permiso/configuración de empresa que Caja. */}
      <ManualCashMovementModal
        open={ctx.manualCashMovement.movementModalOpen}
        saving={ctx.manualCashMovement.saving}
        saveError={ctx.manualCashMovement.saveError}
        register={ctx.manualCashMovement.movementForm.register}
        errors={ctx.manualCashMovement.movementForm.formState.errors}
        selectedMovementType={ctx.manualCashMovement.selectedMovementType}
        movementTypes={ctx.manualCashMovement.movementTypes}
        reasons={ctx.manualCashMovement.reasons}
        reasonsLoading={ctx.manualCashMovement.reasonsLoading}
        onSubmit={ctx.manualCashMovement.handleRecordMovement}
        onClose={ctx.manualCashMovement.closeMovementModal}
      />

      <PaymentDetailModal
        open={ctx.modalDetail}
        methodName={ctx.detailMethodName}
        detailType={ctx.detailMethodType}
        requiresReference={
          ctx.paymentMethods.find((pm) => pm.id === ctx.detailMethodId)
            ?.requiresReference ?? false
        }
        bankAccountOptions={ctx.bankAccountOptions}
        initialRows={ctx.detailRows}
        initialKey={ctx.detailKey}
        available={remainingToCollect(ctx, ctx.detailMethodId)}
        onConfirm={(rows) => {
          // POS-CASH-ONLY-FOLLOWS-TOTAL-01: base = pagos con el Efectivo único ya fijado a lo
          // recibido (si aplica) — misma base con la que se calculó `available`.
          const base = ctx.paymentsForAdditionalMethod(ctx.detailMethodId);
          ctx.setInvoicePayments(() => {
            const without = base.filter(
              (p) => p.paymentMethodId !== ctx.detailMethodId,
            );
            const newPayments = rows.map((r) => ({
              _key: ctx.payKey + r._k,
              paymentMethodId: ctx.detailMethodId,
              amount: r.amount,
              // SALES-TRANSFER-PAYMENT-REFERENCE-PAYLOAD-01: antes se enviaba siempre null — el
              // comprobante/autorización/nro. de cheque capturado en el modal nunca llegaba a
              // payments[].reference, así que el backend rechazaba la emisión para métodos con
              // RequiresReference=true (ej. Transferencia Bancaria) aunque el usuario ya lo
              // hubiera ingresado.
              reference: deriveDetailReference(ctx.detailMethodType, r),
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
        issueDate={ctx.formWatch.issueDate || ""}
        isManual={ctx.scheduleIsManual}
        onRowsChange={ctx.setCreditRows}
        onRecalculate={() =>
          ctx.setCreditRows(ctx.simulateCreditInstallments(ctx.creditAmount))
        }
        onConfirm={(rows, totalAmount) => {
          // ADR-033, Fase 4: el cronograma confirmado (detalle exacto, no solo el total) se
          // envía al backend al guardar el borrador — ver persistDraft/schedule en useSalesPage.
          ctx.setConfirmedScheduleRows(rows);
          ctx.setScheduleIsManual(true);
          const creditPm = ctx.paymentMethods.find((p) => p.isCreditAllowed);
          if (creditPm) {
            const base = ctx.paymentsForAdditionalMethod(creditPm.id);
            ctx.setInvoicePayments(() => {
              const prev = base;
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
        isElectronic={ctx.isElectronic}
        steps={issueStepsFor(ctx.isElectronic)}
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

      <ZHConfirmModal
        open={!!ctx.repricingModal}
        variant="warning"
        title="Cambio de cliente"
        message={
          ctx.repricingModal && (
            <div className="sales-repricing-modal">
              <p className="zh-confirm-message">
                El cambio de cliente modifica los precios de{" "}
                {ctx.repricingModal.rows.length} producto
                {ctx.repricingModal.rows.length === 1 ? "" : "s"}.
              </p>
              <SalesRepricingTable rows={ctx.repricingModal.rows} />
            </div>
          )
        }
        confirmLabel="Cambiar cliente y recalcular"
        cancelLabel="Cancelar"
        onCancel={ctx.cancelRepricing}
        onConfirm={ctx.confirmRepricing}
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
