import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import {
  ZhDecimalInput,
  ZhNumberInput,
  ZhTextarea,
} from "../../../components/zh/inputs";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import {
  Badge,
  PageShell,
  type BadgeVariant,
} from "../../../components/PageShell";
import { formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import { useI18n } from "../../../i18n/i18n";
import { useCajaPage } from "../hooks/useCajaPage";
import { useState } from "react";
import type {
  CashSessionListItemDto,
  CashSessionListCollectionByMethodDto,
  CashMovementDto,
  CashClosingCountDto,
  CashSessionCollectionByMethodDto,
  CashSessionCollectionDetailDto,
} from "../api/cajaService";
import { cashMovementTypeLabel } from "../constants/cashMovementTypes";
import "../../../styles/shared/erp-form-core.css";
import "../../../styles/shared/items-catalog.css";
import "./CajaPage.css";

export function CajaPage() {
  const { t } = useI18n();
  const ctx = useCajaPage();
  const [expandedMethodId, setExpandedMethodId] = useState<string | null>(null);
  const [expandedSessionId, setExpandedSessionId] = useState<string | null>(null);

  const statusLabel = (s: string) => (s === "Open" ? "Abierta" : "Cerrada");
  const statusBadge = (s: string): BadgeVariant =>
    s === "Open" ? "success" : "neutral";

  // ── CASH-SESSION-LIST-SUMMARY-01 — tabla compacta + fila expandible ────────────────
  // Efectivo esperado/Monto contado/Diferencia/Movimientos siguen viniendo de CashSession/
  // CashMovement (efectivo físico, SSOT sin cambios). Facturas del turno/Total facturado y el
  // desglose por forma del expandible vienen de SalesInvoice+SalesInvoicePayment+PaymentMethod
  // (informativo) — ya incluidos en la fila por el backend, sin requests adicionales al expandir.
  const sessionColumns: ZHDataTableColumn<CashSessionListItemDto>[] = [
    { key: "openedAt", header: "Apertura", render: (s) => formatDateTime(s.openedAt) },
    { key: "closedAt", header: "Cierre", render: (s) => (s.closedAt ? formatDateTime(s.closedAt) : "—") },
    { key: "cashRegister", header: "Caja", render: (s) => s.cashRegisterCodeSnapshot },
    { key: "emissionPoint", header: "P. emisión", render: (s) => s.emissionPointCodeSnapshot },
    { key: "user", header: "Cajero", render: (s) => s.userName ?? "—" },
    {
      key: "status",
      header: "Estado",
      render: (s) => <Badge variant={statusBadge(s.status)} label={statusLabel(s.status)} />,
    },
    { key: "invoices", header: "Facturas", align: "center", render: (s) => s.invoiceCount },
    {
      key: "totalInvoiced",
      header: "Total facturado",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (s) => formatMoneyWithSymbol(s.totalInvoiced),
    },
    {
      key: "expectedCash",
      header: "Efectivo esperado",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (s) => formatMoneyWithSymbol(s.expectedCash),
    },
    {
      key: "countedAmount",
      header: "Contado",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (s) => (s.countedAmount != null ? formatMoneyWithSymbol(s.countedAmount) : "—"),
    },
    {
      key: "difference",
      header: "Diferencia",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (s) => (s.difference != null ? formatMoneyWithSymbol(s.difference) : "—"),
    },
    { key: "movements", header: "Movimientos", align: "center", render: (s) => s.movementCount },
    {
      key: "actions",
      header: "Acciones",
      align: "center",
      render: (s) => (
        <>
          <ZHIconButton
            icon={expandedSessionId === s.id ? "expand_less" : "expand_more"}
            variant="ghost"
            title={expandedSessionId === s.id ? "Ocultar resumen" : "Ver resumen del turno"}
            ariaLabel={expandedSessionId === s.id ? "Ocultar resumen" : "Ver resumen del turno"}
            onClick={() => setExpandedSessionId((prev) => (prev === s.id ? null : s.id))}
          />
          <ZHIconButton
            icon="visibility"
            variant="ghost"
            title={`Ver detalle de sesión ${formatDateTime(s.openedAt)}`}
            ariaLabel={`Ver detalle de sesión ${formatDateTime(s.openedAt)}`}
            onClick={() => ctx.loadDetail(s.id)}
          />
        </>
      ),
    },
  ];

  const sessionListByMethodColumns: ZHDataTableColumn<CashSessionListCollectionByMethodDto>[] = [
    {
      key: "method",
      header: "Forma",
      render: (m) => (
        <>
          {m.paymentMethodName}{" "}
          <span className="cj-collection-code">({m.paymentMethodCode})</span>
        </>
      ),
    },
    { key: "invoices", header: "Facturas", align: "center", render: (m) => m.invoiceCount },
    {
      key: "amount",
      header: "Total",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (m) => formatMoneyWithSymbol(m.amount),
    },
  ];

  // TREASURY-CASH-MANUAL-MOVEMENTS-01 — Tipo usa la única fuente compartida de labels
  // (cashMovementTypeLabel, incluye SaleRefund); Motivo/Usuario vienen ya resueltos por el
  // backend en el propio movimiento (ReasonName/CreatedByName) — sin requests adicionales.
  const sessionMovementColumns: ZHDataTableColumn<CashMovementDto>[] = [
    { key: "date", header: "Fecha/Hora", render: (m) => formatDateTime(m.createdAt) },
    { key: "type", header: "Tipo", render: (m) => cashMovementTypeLabel(m.movementType) },
    { key: "reason", header: "Motivo", render: (m) => m.reasonName ?? "—" },
    { key: "description", header: "Descripción", render: (m) => m.description },
    { key: "amount", header: "Monto", align: "right", cellClassName: "zh-table-cell--num", render: (m) => formatMoneyWithSymbol(m.amount) },
    { key: "user", header: "Usuario", render: (m) => m.createdByName ?? "—" },
    { key: "reference", header: "Referencia", render: (m) => m.referenceNumber ?? "—" },
  ];

  const arqueoColumns: ZHDataTableColumn<CashClosingCountDto>[] = [
    { key: "denomination", header: "Denominación", render: (c) => c.denominationLabel },
    { key: "quantity", header: "Cantidad", align: "center", render: (c) => c.quantity },
    { key: "total", header: "Total", align: "right", cellClassName: "zh-table-cell--num", render: (c) => formatMoneyWithSymbol(c.total) },
  ];

  // ── CASH-SESSION-COLLECTION-SUMMARY-01/UX-02 — Cobros del turno != efectivo físico de caja ──
  // Esta tabla y sus detalles son puramente informativos: se calculan en vivo desde
  // SalesInvoice+SalesInvoicePayment+PaymentMethod y jamás afectan totalIncome/currentBalance
  // (efectivo físico), que siguen viniendo solo de CashSession/CashMovement arriba. El backend ya
  // entrega byPaymentMethod en el orden fijo de UX (Efectivo, Transferencia, Tarjeta, Cheque,
  // Crédito; otros después) — el frontend nunca reordena ni agrupa por nombre.
  const percentOfCollected = (
    m: CashSessionCollectionByMethodDto,
    totalCollected: number,
  ): string => {
    if (m.isCreditAllowed || totalCollected <= 0) return "—";
    return `${((m.amount / totalCollected) * 100).toFixed(1)}%`;
  };

  const collectionByMethodColumns = (
    totalCollected: number,
  ): ZHDataTableColumn<CashSessionCollectionByMethodDto>[] => [
    {
      key: "method",
      header: "Forma/código",
      render: (m) => (
        <>
          {m.paymentMethodName} <span className="cj-collection-code">({m.paymentMethodCode})</span>
          {m.isCreditAllowed && (
            <Badge variant="neutral" label="Crédito" className="cj-collection-credit-badge" />
          )}
        </>
      ),
    },
    { key: "invoices", header: "Facturas distintas", align: "center", render: (m) => m.invoiceCount },
    { key: "operations", header: "Operaciones", align: "center", render: (m) => m.operationCount },
    {
      key: "amount",
      header: "Total",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (m) => formatMoneyWithSymbol(m.amount),
    },
    {
      key: "percent",
      header: "% del cobrado",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (m) => percentOfCollected(m, totalCollected),
    },
    { key: "destination", header: "Destino", render: (m) => m.destination },
    {
      key: "detail",
      header: "Detalle",
      align: "center",
      render: (m) => (
        <ZHIconButton
          icon={expandedMethodId === m.paymentMethodId ? "expand_less" : "expand_more"}
          variant="ghost"
          title={
            expandedMethodId === m.paymentMethodId
              ? "Ocultar detalle"
              : `Ver detalle de ${m.paymentMethodName}`
          }
          ariaLabel={`Ver detalle de ${m.paymentMethodName}`}
          onClick={() =>
            setExpandedMethodId((prev) =>
              prev === m.paymentMethodId ? null : m.paymentMethodId,
            )
          }
        />
      ),
    },
  ];

  const collectionDetailColumns: ZHDataTableColumn<CashSessionCollectionDetailDto>[] = [
    { key: "authorizedAt", header: "Fecha/Hora", render: (d) => formatDateTime(d.authorizedAt) },
    { key: "invoiceNumber", header: "Factura", render: (d) => d.invoiceNumber },
    { key: "customer", header: "Cliente", render: (d) => d.customerName },
    {
      key: "invoiceTotal",
      header: "Total factura",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (d) => formatMoneyWithSymbol(d.invoiceTotal),
    },
    {
      key: "amount",
      header: "Monto (esta forma)",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (d) => formatMoneyWithSymbol(d.amount),
    },
    {
      key: "mixed",
      header: "Completa/Mixta",
      align: "center",
      render: (d) => (
        <Badge
          variant={d.isMixedPayment ? "warning" : "neutral"}
          label={d.isMixedPayment ? "Mixta" : "Completa"}
        />
      ),
    },
    { key: "reference", header: "Referencia/comprobante", render: (d) => d.reference ?? "—" },
    {
      key: "destination",
      header: "Destino",
      render: (d) => {
        if (!d.destinationBankName) return "—";
        return (
          <>
            {d.destinationBankName}
            {d.destinationAccountMasked ? ` — ${d.destinationAccountMasked}` : ""}
            {d.transferDate ? (
              <span className="cj-collection-transfer-date"> ({formatDate(d.transferDate)})</span>
            ) : null}
          </>
        );
      },
    },
  ];

  return (
    <PageShell
      title={t("caja.title", "Turno de caja")}
      kicker={t("caja.kicker", "Gestión de efectivo")}
    >
      <div className="cj-content">
        {ctx.mySession && ctx.tab === "listado" && (
          <ZHPageNotice
            variant="info"
            message={`Caja abierta — Saldo: ${formatMoneyWithSymbol(ctx.mySession.currentBalance)}`}
          />
        )}

        {ctx.saveError && (
          <ZHPageNotice variant="error" message={ctx.saveError} />
        )}

        {/* ═══════════════════════ LISTADO ═══════════════════════════ */}
        {ctx.tab === "listado" && (
          <div className="prd-section">
            <div className="cj-toolbar">
              {!ctx.mySession && (
                <ZHBtn
                  type="button"
                  variant="primary"
                  onClick={() => ctx.setTab("abrir")}
                >
                  <span className="material-symbols-outlined zh-icon-md">
                    add
                  </span>
                  Abrir Caja
                </ZHBtn>
              )}
              <div className="cj-toolbar-spacer" />
              <select
                value={ctx.statusFilter}
                onChange={(e) => ctx.setStatusFilter(e.target.value)}
                className="cj-filter-select"
              >
                <option value="">Todos</option>
                <option value="Open">Abiertas</option>
                <option value="Closed">Cerradas</option>
              </select>
              <ZHBtn
                variant="secondary"
                onClick={ctx.fetchList}
                disabled={ctx.listLoading}
              >
                <span className="material-symbols-outlined zh-icon-md">
                  refresh
                </span>
              </ZHBtn>
            </div>

            <ZHDataTable
              columns={sessionColumns}
              rows={ctx.listItems}
              rowKey={(s) => s.id}
              loading={ctx.listLoading}
              showRowNumber
              tableClassName="table--compact table--neutral"
              emptyMessage="Sin sesiones de caja."
            />

            {/* CASH-SESSION-LIST-SUMMARY-01 — expandible: resumen ventas / cobros por forma /
                caja física / fechas+usuario, todo ya presente en la fila (sin request adicional).
                "Caja física" es la única sección que afecta apertura/cierre/arqueo; el resto es
                informativo (SalesInvoice+SalesInvoicePayment+PaymentMethod). */}
            {expandedSessionId &&
              (() => {
                const s = ctx.listItems.find((i) => i.id === expandedSessionId);
                if (!s) return null;
                return (
                  <div className="cj-collection-detail-wrap">
                    <h5 className="cj-collection-detail-title">
                      Resumen del turno — {formatDateTime(s.openedAt)}
                    </h5>

                    <div className="cj-summary-grid">
                      <SummaryCard label="Facturas" value={String(s.invoiceCount)} />
                      <SummaryCard label="Total facturado" value={formatMoneyWithSymbol(s.totalInvoiced)} />
                      <SummaryCard label="Cajero" value={s.userName ?? "—"} />
                      <SummaryCard
                        label="Cerrado por"
                        value={s.closedByName ?? (s.status === "Open" ? "Turno abierto" : "—")}
                      />
                    </div>

                    <h6 className="cj-collection-detail-subtitle">Cobros por forma</h6>
                    <ZHDataTable
                      columns={sessionListByMethodColumns}
                      rows={s.byPaymentMethod}
                      rowKey={(m) => m.paymentMethodId}
                      tableClassName="table--compact table--neutral"
                      emptyMessage="Sin cobros registrados en este turno."
                    />

                    <h6 className="cj-collection-detail-subtitle">Caja física</h6>
                    <div className="cj-summary-grid">
                      <SummaryCard label="Apertura" value={formatMoneyWithSymbol(s.openingAmount)} />
                      <SummaryCard label="Ventas en efectivo" value={formatMoneyWithSymbol(s.saleIncomeCash)} />
                      <SummaryCard label="Ingresos manuales" value={formatMoneyWithSymbol(s.manualIncomeCash)} />
                      <SummaryCard label="Egresos manuales" value={formatMoneyWithSymbol(s.manualExpenseCash)} />
                      <SummaryCard label="Saldo esperado" value={formatMoneyWithSymbol(s.expectedCash)} highlight />
                      {s.status === "Closed" && (
                        <>
                          <SummaryCard
                            label="Contado"
                            value={formatMoneyWithSymbol(s.countedAmount ?? 0)}
                          />
                          <SummaryCard
                            label="Diferencia"
                            value={formatMoneyWithSymbol(s.difference ?? 0)}
                            highlight={(s.difference ?? 0) !== 0}
                          />
                        </>
                      )}
                    </div>
                  </div>
                );
              })()}
          </div>
        )}

        {/* ═══════════════════════ ABRIR CAJA ════════════════════════ */}
        {ctx.tab === "abrir" && (
          <div className="prd-section cj-form-narrow">
            <h3 className="cj-section-title">Abrir Caja</h3>
            {ctx.cashRegisters.length === 0 && (
              <ZHPageNotice
                variant="warning"
                message="No hay cajas disponibles en la sucursal activa. Contacte al administrador para configurar una."
              />
            )}
            <form onSubmit={ctx.handleOpen}>
              <ZHField
                density="compact"
                className="zh-mb-12"
                label="Sucursal activa"
              >
                <input
                  type="text"
                  value={ctx.branchName ?? "—"}
                  readOnly
                  disabled
                />
              </ZHField>

              <ZHField
                density="compact"
                className="zh-mb-12"
                label="Caja"
                required
                fieldError={
                  ctx.openForm.formState.errors.cashRegisterId?.message
                }
              >
                <select
                  {...ctx.openForm.register("cashRegisterId")}
                  disabled={ctx.cashRegisters.length === 0}
                >
                  <option value="">Seleccione...</option>
                  {ctx.cashRegisters.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.code} — {r.name}
                    </option>
                  ))}
                </select>
              </ZHField>

              {/* Confirmación visual antes de abrir: Sucursal/Establecimiento/Punto de emisión
                  son solo informativos, provienen del mismo CashRegisterDto que llena el <select>
                  de arriba — no se puede editar ni se hace una petición adicional. */}
              {ctx.selectedRegister && (
                <div className="cj-summary-grid zh-mb-12">
                  <SummaryCard
                    label="Sucursal"
                    value={ctx.selectedRegister.branchName}
                  />
                  <SummaryCard
                    label="Establecimiento"
                    value={ctx.selectedRegister.establishmentCode ?? "—"}
                  />
                  <SummaryCard
                    label="Punto de emisión"
                    value={ctx.selectedRegister.emissionPointCode ?? "—"}
                  />
                </div>
              )}

              <ZHField
                density="compact"
                className="zh-mb-12"
                label="Monto de apertura"
                required
                fieldError={
                  ctx.openForm.formState.errors.openingAmount?.message
                }
              >
                <ZhDecimalInput
                  {...ctx.openForm.register("openingAmount")}
                  decimals={2}
                  positiveOnly
                />
              </ZHField>

              <ZHField density="compact" className="zh-mb-12" label="Notas">
                <ZhTextarea {...ctx.openForm.register("notes")} rows={2} />
              </ZHField>

              <div className="cj-actions">
                <ZHBtn
                  variant="primary"
                  type="submit"
                  disabled={ctx.saving || ctx.cashRegisters.length === 0}
                >
                  {ctx.saving ? "Abriendo..." : "Abrir Caja"}
                </ZHBtn>
                <ZHBtn
                  variant="secondary"
                  onClick={() => ctx.setTab("listado")}
                >
                  Cancelar
                </ZHBtn>
              </div>
            </form>
          </div>
        )}

        {/* ═══════════════════════ DETALLE ═══════════════════════════ */}
        {ctx.tab === "detalle" && ctx.viewing && (
          <div className="prd-section">
            <div className="cj-detail-header">
              <ZHBtn
                variant="secondary"
                size="sm"
                onClick={() => {
                  ctx.setTab("listado");
                }}
              >
                <span className="material-symbols-outlined zh-icon-md">
                  arrow_back
                </span>{" "}
                Volver
              </ZHBtn>
              <h3>Sesión de Caja</h3>
              <Badge
                variant={statusBadge(ctx.viewing.status)}
                label={statusLabel(ctx.viewing.status)}
              />
              <div className="cj-detail-header-spacer" />
              {ctx.viewing.status === "Open" && (
                <>
                  <ZHBtn variant="secondary" onClick={ctx.openMovementModal}>
                    <span className="material-symbols-outlined zh-icon-md">
                      add
                    </span>{" "}
                    Registrar movimiento
                  </ZHBtn>
                  <ZHBtn variant="destructive" onClick={ctx.startClose}>
                    Cerrar Caja
                  </ZHBtn>
                </>
              )}
            </div>

            {/* Sucursal / Caja / Punto de emisión: solo informativos. El punto de emisión viene
                exclusivamente de CashSessionDto (emissionPointCodeSnapshot) — nunca de un lookup
                manual a EmissionPoint. */}
            <div className="cj-summary-grid">
              <SummaryCard label="Sucursal" value={ctx.branchName ?? "—"} />
              <SummaryCard
                label="Caja"
                value={`${ctx.viewing.cashRegisterCodeSnapshot} — ${ctx.viewing.cashRegisterNameSnapshot}`}
              />
              <SummaryCard
                label="Punto de emisión"
                value={ctx.viewing.emissionPointCodeSnapshot}
              />
              <SummaryCard
                label="Estado"
                value={statusLabel(ctx.viewing.status)}
              />
            </div>

            {/* CASH-SESSION-COLLECTION-SUMMARY-UX-02 — "Efectivo físico" es la única sección que
                afecta apertura/cierre/arqueo (CashSession/CashMovement). Todo lo que sigue debajo
                (Resumen de ventas y cobros / Cobros por forma) es informativo y nunca cambia estos
                valores, aunque haya ventas por Transferencia/Tarjeta/Cheque/Crédito. */}
            <h4 className="cj-section-title">Efectivo físico</h4>
            <div className="cj-summary-grid">
              <SummaryCard
                label="Apertura"
                value={formatMoneyWithSymbol(ctx.viewing.openingAmount)}
              />
              <SummaryCard
                label="Ingresos"
                value={formatMoneyWithSymbol(ctx.viewing.totalIncome)}
              />
              <SummaryCard
                label="Egresos"
                value={formatMoneyWithSymbol(ctx.viewing.totalExpense)}
              />
              <SummaryCard
                label="Saldo esperado"
                value={formatMoneyWithSymbol(ctx.viewing.currentBalance)}
                highlight
              />
              {ctx.viewing.status === "Closed" && (
                <>
                  <SummaryCard
                    label="Esperado"
                    value={formatMoneyWithSymbol(
                      ctx.viewing.expectedAmount ?? 0,
                    )}
                  />
                  <SummaryCard
                    label="Contado"
                    value={formatMoneyWithSymbol(
                      ctx.viewing.countedAmount ?? 0,
                    )}
                  />
                  <SummaryCard
                    label="Diferencia"
                    value={formatMoneyWithSymbol(ctx.viewing.difference ?? 0)}
                    highlight={(ctx.viewing.difference ?? 0) !== 0}
                  />
                </>
              )}
            </div>

            {/* Informativo: ventas/cobros del turno, separado del efectivo físico de arriba.
                "Cobros del turno" != "efectivo físico de caja" — Transferencia/Tarjeta/Cheque/
                Crédito aparecen aquí pero nunca modifican Apertura/Ingresos/Egresos/Saldo. */}
            <h4 className="cj-section-title cj-section-title--spaced">Resumen de ventas y cobros</h4>
            {ctx.collectionSummaryLoading && !ctx.collectionSummary ? (
              <p className="cj-collection-loading">Cargando resumen de cobros…</p>
            ) : (
              <>
                <div className="cj-summary-grid">
                  <SummaryCard
                    label="Facturas autorizadas"
                    value={String(ctx.collectionSummary?.invoiceCount ?? 0)}
                  />
                  <SummaryCard
                    label="Total facturado"
                    value={formatMoneyWithSymbol(ctx.collectionSummary?.totalInvoiced ?? 0)}
                  />
                  <SummaryCard
                    label="Total cobrado"
                    value={formatMoneyWithSymbol(ctx.collectionSummary?.totalCollected ?? 0)}
                  />
                  <SummaryCard
                    label="Vendido a crédito"
                    value={formatMoneyWithSymbol(ctx.collectionSummary?.totalCredit ?? 0)}
                  />
                </div>
                <h5 className="cj-collection-detail-title">Cobros por forma</h5>
                <ZHDataTable
                  columns={collectionByMethodColumns(ctx.collectionSummary?.totalCollected ?? 0)}
                  rows={ctx.collectionSummary?.byPaymentMethod ?? []}
                  rowKey={(m) => m.paymentMethodId}
                  tableClassName="table--compact table--neutral"
                  emptyMessage="Sin cobros registrados en este turno."
                />
                {expandedMethodId &&
                  (() => {
                    const expanded = ctx.collectionSummary?.byPaymentMethod.find(
                      (m) => m.paymentMethodId === expandedMethodId,
                    );
                    if (!expanded) return null;
                    return (
                      <div className="cj-collection-detail-wrap">
                        <h5 className="cj-collection-detail-title">
                          Detalle — {expanded.paymentMethodName}
                        </h5>
                        <ZHDataTable
                          columns={collectionDetailColumns}
                          rows={expanded.details}
                          rowKey={(d) => `${d.invoiceId}-${expanded.paymentMethodId}`}
                          tableClassName="table--compact table--neutral"
                          emptyMessage="Sin facturas."
                        />
                      </div>
                    );
                  })()}
              </>
            )}

            <h4 className="cj-section-title">Movimientos</h4>
            <ZHDataTable
              columns={sessionMovementColumns}
              rows={ctx.viewing.movements}
              rowKey={(m) => m.id}
              tableClassName="table--compact table--neutral"
              emptyMessage="Sin movimientos."
            />

            {ctx.viewing.status === "Closed" &&
              ctx.viewing.closingCounts.length > 0 && (
                <>
                  <h4 className="cj-section-title cj-section-title--spaced">
                    Arqueo
                  </h4>
                  <ZHDataTable
                    columns={arqueoColumns}
                    rows={ctx.viewing.closingCounts}
                    rowKey={(c) => c.id}
                    tableClassName="table--compact table--neutral cj-arqueo-table"
                  />
                  {ctx.viewing.closeNotes && (
                    <p className="cj-close-notes">
                      <strong>Notas:</strong> {ctx.viewing.closeNotes}
                    </p>
                  )}
                </>
              )}

            {/* TREASURY-CASH-MANUAL-MOVEMENT-MODAL-02 — mismo movementForm/handleRecordMovement/
                reasons/movementTypes de siempre (useCajaPage), solo cambia de formulario inline
                permanente a modal; solo alcanzable con turno abierto (botón "Registrar
                movimiento" arriba). */}
            {ctx.viewing.status === "Open" && (
              <ZHModal
                open={ctx.movementModalOpen}
                onClose={ctx.closeMovementModal}
                size="md"
                title="Registrar movimiento manual de efectivo"
                closeOnBackdrop={!ctx.saving}
              >
                <form
                  onSubmit={ctx.handleRecordMovement}
                  className="cj-movement-form"
                >
                  <ZHField
                    density="compact"
                    className="cj-movement-field--type"
                    label="Tipo"
                    required
                    fieldError={
                      ctx.movementForm.formState.errors.movementType?.message
                    }
                  >
                    <select {...ctx.movementForm.register("movementType")}>
                      <option value="">Seleccione...</option>
                      {ctx.movementTypes.map((mt) => (
                        <option key={mt.value} value={mt.value}>
                          {mt.label}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                  {/* TREASURY-CASH-MANUAL-MOVEMENTS-01 — motivo dinámico desde el backend
                      (CashMovementReason), filtrado por Tenant+Company+Tipo — nunca texto libre
                      ni una lista hardcodeada en el frontend. */}
                  <ZHField
                    density="compact"
                    className="cj-movement-field--reason"
                    label="Motivo"
                    required
                    fieldError={
                      ctx.movementForm.formState.errors.reasonId?.message
                    }
                  >
                    <select
                      {...ctx.movementForm.register("reasonId")}
                      disabled={
                        !ctx.movementForm.watch("movementType") ||
                        ctx.reasonsLoading ||
                        ctx.reasons.length === 0
                      }
                    >
                      <option value="">
                        {ctx.reasonsLoading
                          ? "Cargando motivos..."
                          : ctx.movementForm.watch("movementType") && ctx.reasons.length === 0
                            ? "Sin motivos configurados para este tipo"
                            : "Seleccione..."}
                      </option>
                      {ctx.reasons.map((r) => (
                        <option key={r.id} value={r.id}>
                          {r.name}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                  <ZHField
                    density="compact"
                    className="cj-movement-field--amount"
                    label="Monto"
                    required
                    fieldError={
                      ctx.movementForm.formState.errors.amount?.message
                    }
                  >
                    <ZhDecimalInput
                      {...ctx.movementForm.register("amount")}
                      decimals={2}
                      positiveOnly
                    />
                  </ZHField>
                  <ZHField
                    density="compact"
                    className="cj-movement-field--desc"
                    label="Descripción adicional"
                    required
                    fieldError={
                      ctx.movementForm.formState.errors.description?.message
                    }
                  >
                    <input
                      type="text"
                      {...ctx.movementForm.register("description")}
                    />
                  </ZHField>
                  {ctx.saveError && (
                    <ZHPageNotice variant="error" message="Error" detail={ctx.saveError} />
                  )}
                  <div className="cj-actions">
                    <ZHBtn variant="primary" type="submit" disabled={ctx.saving}>
                      {ctx.saving ? "Registrando..." : "Registrar"}
                    </ZHBtn>
                    <ZHBtn
                      variant="secondary"
                      type="button"
                      disabled={ctx.saving}
                      onClick={ctx.closeMovementModal}
                    >
                      Cancelar
                    </ZHBtn>
                  </div>
                </form>
              </ZHModal>
            )}
          </div>
        )}

        {/* ═══════════════════════ CERRAR CAJA ═══════════════════════ */}
        {ctx.tab === "cerrar" && ctx.viewing && (
          <div className="prd-section cj-close-narrow">
            <div className="cj-detail-header">
              <ZHBtn
                variant="secondary"
                size="sm"
                onClick={() => ctx.setTab("detalle")}
              >
                <span className="material-symbols-outlined zh-icon-md">
                  arrow_back
                </span>{" "}
                Volver
              </ZHBtn>
              <h3>Cerrar Caja — Arqueo</h3>
            </div>

            <div className="cj-close-summary">
              <div>
                <strong>Saldo esperado:</strong>{" "}
                {formatMoneyWithSymbol(ctx.viewing.currentBalance)}
              </div>
              <div>
                <strong>Contado:</strong>{" "}
                {formatMoneyWithSymbol(ctx.countedTotal)}
              </div>
              <div
                className={
                  ctx.countedTotal - ctx.viewing.currentBalance !== 0
                    ? "cj-close-diff--mismatch"
                    : "cj-close-diff--ok"
                }
              >
                <strong>Diferencia:</strong>{" "}
                {formatMoneyWithSymbol(
                  ctx.countedTotal - ctx.viewing.currentBalance,
                )}
              </div>
            </div>

            <form onSubmit={ctx.handleClose}>
              <table className="pf-table zh-mb-16">
                <thead>
                  <tr>
                    <th>Denominación</th>
                    <th className="zh-text-align-center">Cantidad</th>
                    <th className="zh-text-align-right">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {ctx.closeForm.watch("closingCounts").map((c, i) => (
                    <tr key={c._key}>
                      <td>{c.denominationLabel}</td>
                      <td className="zh-text-align-center">
                        <ZhNumberInput
                          positiveOnly
                          className="cj-arqueo-input"
                          {...ctx.closeForm.register(
                            `closingCounts.${i}.quantity`,
                            { valueAsNumber: true },
                          )}
                        />
                      </td>
                      <td className="zh-table-cell--num">
                        {formatMoneyWithSymbol(
                          c.denominationValue * c.quantity,
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr>
                    <td colSpan={2} className="cj-arqueo-total">
                      Total contado
                    </td>
                    <td className="zh-table-cell--num cj-arqueo-total">
                      {formatMoneyWithSymbol(ctx.countedTotal)}
                    </td>
                  </tr>
                </tfoot>
              </table>

              <ZHField
                density="compact"
                className="zh-mb-12"
                label="Notas de cierre"
              >
                <ZhTextarea {...ctx.closeForm.register("closeNotes")} rows={2} />
              </ZHField>

              <div className="cj-actions">
                <ZHBtn
                  variant="destructive"
                  type="submit"
                  disabled={ctx.saving}
                >
                  {ctx.saving ? "Cerrando..." : "Confirmar Cierre"}
                </ZHBtn>
                <ZHBtn
                  variant="secondary"
                  onClick={() => ctx.setTab("detalle")}
                >
                  Cancelar
                </ZHBtn>
              </div>
            </form>
          </div>
        )}
      </div>
    </PageShell>
  );
}

function SummaryCard({
  label,
  value,
  highlight,
}: {
  label: string;
  value: string;
  highlight?: boolean;
}) {
  return (
    <div
      className={`cj-summary-card${highlight ? " cj-summary-card--highlight" : ""}`}
    >
      <div className="cj-summary-card__label">{label}</div>
      <div className="cj-summary-card__value">{value}</div>
    </div>
  );
}
