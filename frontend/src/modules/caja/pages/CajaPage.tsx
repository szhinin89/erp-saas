import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
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
import { ManualCashMovementModal } from "../components/ManualCashMovementModal";
import "../../../styles/shared/erp-form-core.css";
import "../../../styles/shared/items-catalog.css";
import "./CajaPage.css";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";

export function CajaPage() {
  const moneyDecimals = usePrecisionDecimals("money"); // presentación (04F)
  const { t } = useI18n();
  const ctx = useCajaPage();
  const [expandedMethodId, setExpandedMethodId] = useState<string | null>(null);
  const [expandedSessionId, setExpandedSessionId] = useState<string | null>(null);

  const statusLabel = (s: string) => (s === "Open" ? t("caja.session.status.open") : t("caja.session.status.closed"));
  const statusBadge = (s: string): BadgeVariant =>
    s === "Open" ? "success" : "neutral";

  // ── CASH-SESSION-LIST-SUMMARY-01 — tabla compacta + fila expandible ────────────────
  // Efectivo esperado/Monto contado/Diferencia/Movimientos siguen viniendo de CashSession/
  // CashMovement (efectivo físico, SSOT sin cambios). Facturas del turno/Total facturado y el
  // desglose por forma del expandible vienen de SalesInvoice+SalesInvoicePayment+PaymentMethod
  // (informativo) — ya incluidos en la fila por el backend, sin requests adicionales al expandir.
  const sessionColumns: ZHDataTableColumn<CashSessionListItemDto>[] = [
    { key: "openedAt", header: t("caja.movementType.opening"), render: (s) => formatDateTime(s.openedAt) },
    { key: "closedAt", header: t("caja.session.closing"), render: (s) => (s.closedAt ? formatDateTime(s.closedAt) : "—") },
    { key: "cashRegister", header: t("caja.session.register"), render: (s) => s.cashRegisterCodeSnapshot },
    { key: "emissionPoint", header: t("caja.session.emissionPointShort"), render: (s) => s.emissionPointCodeSnapshot },
    { key: "user", header: t("caja.session.cashier"), render: (s) => s.userName ?? "—" },
    {
      key: "status",
      header: t("common.status"),
      render: (s) => <Badge variant={statusBadge(s.status)} label={statusLabel(s.status)} />,
    },
    { key: "invoices", header: t("caja.session.invoices"), align: "center", render: (s) => s.invoiceCount },
    {
      key: "totalInvoiced",
      header: t("caja.session.totalInvoiced"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (s) => <ZHMoneyValue value={s.totalInvoiced} precision="money" />,
    },
    {
      key: "expectedCash",
      header: t("caja.session.expectedCash"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (s) => <ZHMoneyValue value={s.expectedCash} precision="money" />,
    },
    {
      key: "countedAmount",
      header: t("caja.session.counted"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (s) => (s.countedAmount != null ? <ZHMoneyValue value={s.countedAmount} precision="money" /> : "—"),
    },
    {
      key: "difference",
      header: t("caja.session.difference"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (s) => (s.difference != null ? <ZHMoneyValue value={s.difference} precision="money" /> : "—"),
    },
    { key: "movements", header: t("caja.session.movements"), align: "center", render: (s) => s.movementCount },
    {
      key: "actions",
      header: t("common.actions"),
      align: "center",
      render: (s) => (
        <>
          <ZHIconButton
            icon={expandedSessionId === s.id ? "expand_less" : "expand_more"}
            variant="ghost"
            title={expandedSessionId === s.id ? t("caja.session.hideSummary") : t("caja.session.viewSummary")}
            ariaLabel={expandedSessionId === s.id ? t("caja.session.hideSummary") : t("caja.session.viewSummary")}
            onClick={() => setExpandedSessionId((prev) => (prev === s.id ? null : s.id))}
          />
          <ZHIconButton
            icon="visibility"
            variant="ghost"
            title={t("caja.session.viewSession", { date: formatDateTime(s.openedAt) })}
            ariaLabel={t("caja.session.viewSession", { date: formatDateTime(s.openedAt) })}
            onClick={() => ctx.loadDetail(s.id)}
          />
        </>
      ),
    },
  ];

  const sessionListByMethodColumns: ZHDataTableColumn<CashSessionListCollectionByMethodDto>[] = [
    {
      key: "method",
      header: t("caja.session.method"),
      render: (m) => (
        <>
          {m.paymentMethodName}{" "}
          <span className="cj-collection-code">({m.paymentMethodCode})</span>
        </>
      ),
    },
    { key: "invoices", header: t("caja.session.invoices"), align: "center", render: (m) => m.invoiceCount },
    {
      key: "amount",
      header: t("common.total"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (m) => <ZHMoneyValue value={m.amount} precision="money" />,
    },
  ];

  // TREASURY-CASH-MANUAL-MOVEMENTS-01 — Tipo usa la única fuente compartida de labels
  // (cashMovementTypeLabel, incluye SaleRefund); Motivo/Usuario vienen ya resueltos por el
  // backend en el propio movimiento (ReasonName/CreatedByName) — sin requests adicionales.
  const sessionMovementColumns: ZHDataTableColumn<CashMovementDto>[] = [
    { key: "date", header: t("caja.movements.table.date"), render: (m) => formatDateTime(m.createdAt) },
    { key: "type", header: t("caja.movements.table.type"), render: (m) => cashMovementTypeLabel(t, m.movementType) },
    { key: "reason", header: t("caja.movements.table.reason"), render: (m) => m.reasonName ?? "—" },
    { key: "description", header: t("caja.movements.table.description"), render: (m) => m.description },
    { key: "amount", header: t("caja.movements.table.amount"), align: "right", cellClassName: "zh-table-cell--num", render: (m) => <ZHMoneyValue value={m.amount} precision="money" /> },
    { key: "user", header: t("caja.movements.table.user"), render: (m) => m.createdByName ?? "—" },
    { key: "reference", header: t("caja.movements.table.reference"), render: (m) => m.referenceNumber ?? "—" },
  ];

  const arqueoColumns: ZHDataTableColumn<CashClosingCountDto>[] = [
    { key: "denomination", header: t("caja.session.denomination"), render: (c) => c.denominationLabel },
    { key: "quantity", header: t("caja.session.quantity"), align: "center", render: (c) => c.quantity },
    { key: "total", header: t("common.total"), align: "right", cellClassName: "zh-table-cell--num", render: (c) => <ZHMoneyValue value={c.total} precision="money" /> },
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
      header: t("caja.session.methodCode"),
      render: (m) => (
        <>
          {m.paymentMethodName} <span className="cj-collection-code">({m.paymentMethodCode})</span>
          {m.isCreditAllowed && (
            <Badge variant="neutral" label={t("caja.session.credit")} className="cj-collection-credit-badge" />
          )}
        </>
      ),
    },
    { key: "invoices", header: t("caja.session.distinctInvoices"), align: "center", render: (m) => m.invoiceCount },
    { key: "operations", header: t("caja.session.operations"), align: "center", render: (m) => m.operationCount },
    {
      key: "amount",
      header: t("common.total"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (m) => <ZHMoneyValue value={m.amount} precision="money" />,
    },
    {
      key: "percent",
      header: t("caja.session.percentCollected"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (m) => percentOfCollected(m, totalCollected),
    },
    { key: "destination", header: t("caja.session.destination"), render: (m) => m.destination },
    {
      key: "detail",
      header: t("caja.session.detail"),
      align: "center",
      render: (m) => (
        <ZHIconButton
          icon={expandedMethodId === m.paymentMethodId ? "expand_less" : "expand_more"}
          variant="ghost"
          title={
            expandedMethodId === m.paymentMethodId
              ? t("caja.session.hideDetail")
              : t("caja.session.viewMethod", { method: m.paymentMethodName })
          }
          ariaLabel={t("caja.session.viewMethod", { method: m.paymentMethodName })}
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
    { key: "authorizedAt", header: t("caja.movements.table.date"), render: (d) => formatDateTime(d.authorizedAt) },
    { key: "invoiceNumber", header: t("caja.session.invoice"), render: (d) => d.invoiceNumber },
    { key: "customer", header: t("caja.session.customer"), render: (d) => d.customerName },
    {
      key: "invoiceTotal",
      header: t("caja.session.invoiceTotal"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (d) => <ZHMoneyValue value={d.invoiceTotal} precision="money" />,
    },
    {
      key: "amount",
      header: t("caja.session.methodAmount"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (d) => <ZHMoneyValue value={d.amount} precision="money" />,
    },
    {
      key: "mixed",
      header: t("caja.session.paymentScope"),
      align: "center",
      render: (d) => (
        <Badge
          variant={d.isMixedPayment ? "warning" : "neutral"}
          label={d.isMixedPayment ? t("caja.session.mixed") : t("caja.session.full")}
        />
      ),
    },
    { key: "reference", header: t("caja.session.referenceReceipt"), render: (d) => d.reference ?? "—" },
    {
      key: "destination",
      header: t("caja.session.destination"),
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
      title={t("caja.title")}
      kicker={t("caja.kicker")}
    >
      <div className="cj-content">
        {ctx.mySession && ctx.tab === "listado" && (
          <ZHPageNotice
            variant="info"
            message={t("caja.session.openBalance", { balance: formatMoneyWithSymbol(ctx.mySession.currentBalance, moneyDecimals) })}
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
                  {t("caja.session.open")}
                </ZHBtn>
              )}
              <div className="cj-toolbar-spacer" />
              <select
                value={ctx.statusFilter}
                onChange={(e) => ctx.setStatusFilter(e.target.value)}
                className="cj-filter-select"
              >
                <option value="">{t("common.all")}</option>
                <option value="Open">{t("caja.session.status.openPlural")}</option>
                <option value="Closed">{t("caja.session.status.closedPlural")}</option>
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
              emptyMessage={t("caja.session.emptySessions")}
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
                      {t("caja.session.shiftSummary", { date: formatDateTime(s.openedAt) })}
                    </h5>

                    <div className="cj-summary-grid">
                      <SummaryCard label={t("caja.session.invoices")} value={String(s.invoiceCount)} />
                      <SummaryCard label={t("caja.session.totalInvoiced")} value={formatMoneyWithSymbol(s.totalInvoiced, moneyDecimals)} />
                      <SummaryCard label={t("caja.session.cashier")} value={s.userName ?? "—"} />
                      <SummaryCard
                        label={t("caja.session.closedBy")}
                        value={s.closedByName ?? (s.status === "Open" ? t("caja.session.openShift") : "—")}
                      />
                    </div>

                    <h6 className="cj-collection-detail-subtitle">{t("caja.session.collectionsByMethod")}</h6>
                    <ZHDataTable
                      columns={sessionListByMethodColumns}
                      rows={s.byPaymentMethod}
                      rowKey={(m) => m.paymentMethodId}
                      tableClassName="table--compact table--neutral"
                      emptyMessage={t("caja.session.emptyCollections")}
                    />

                    <h6 className="cj-collection-detail-subtitle">{t("caja.session.physicalRegister")}</h6>
                    <div className="cj-summary-grid">
                      <SummaryCard label={t("caja.movementType.opening")} value={formatMoneyWithSymbol(s.openingAmount, moneyDecimals)} />
                      <SummaryCard label={t("caja.session.cashSales")} value={formatMoneyWithSymbol(s.saleIncomeCash, moneyDecimals)} />
                      <SummaryCard label={t("caja.session.manualIncome")} value={formatMoneyWithSymbol(s.manualIncomeCash, moneyDecimals)} />
                      <SummaryCard label={t("caja.session.manualExpense")} value={formatMoneyWithSymbol(s.manualExpenseCash, moneyDecimals)} />
                      <SummaryCard label={t("caja.session.expectedBalance")} value={formatMoneyWithSymbol(s.expectedCash, moneyDecimals)} highlight />
                      {s.status === "Closed" && (
                        <>
                          <SummaryCard
                            label={t("caja.session.counted")}
                            value={formatMoneyWithSymbol(s.countedAmount ?? 0, moneyDecimals)}
                          />
                          <SummaryCard
                            label={t("caja.session.difference")}
                            value={formatMoneyWithSymbol(s.difference ?? 0, moneyDecimals)}
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
            <h3 className="cj-section-title">{t("caja.session.open")}</h3>
            {ctx.cashRegisters.length === 0 && (
              <ZHPageNotice
                variant="warning"
                message={t("caja.session.noRegisters")}
              />
            )}
            <form onSubmit={ctx.handleOpen}>
              <ZHField
                density="compact"
                className="zh-mb-12"
                label={t("caja.session.activeBranch")}
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
                label={t("caja.session.register")}
                required
                fieldError={
                  ctx.openForm.formState.errors.cashRegisterId?.message
                }
              >
                <select
                  {...ctx.openForm.register("cashRegisterId")}
                  disabled={ctx.cashRegisters.length === 0}
                >
                  <option value="">{t("caja.movements.form.selectPlaceholder")}</option>
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
                    label={t("caja.session.branch")}
                    value={ctx.selectedRegister.branchName}
                  />
                  <SummaryCard
                    label={t("caja.session.establishment")}
                    value={ctx.selectedRegister.establishmentCode ?? "—"}
                  />
                  <SummaryCard
                    label={t("caja.session.emissionPoint")}
                    value={ctx.selectedRegister.emissionPointCode ?? "—"}
                  />
                </div>
              )}

              <ZHField
                density="compact"
                className="zh-mb-12"
                label={t("caja.session.openingAmount")}
                required
                fieldError={
                  ctx.openForm.formState.errors.openingAmount?.message
                }
              >
                <ZhDecimalInput
                  {...ctx.openForm.register("openingAmount")}
                  precision="money"
                  positiveOnly
                />
              </ZHField>

              <ZHField density="compact" className="zh-mb-12" label={t("caja.session.notes")}>
                <ZhTextarea {...ctx.openForm.register("notes")} rows={2} />
              </ZHField>

              <div className="cj-actions">
                <ZHBtn
                  variant="primary"
                  type="submit"
                  disabled={ctx.saving || ctx.cashRegisters.length === 0}
                >
                  {ctx.saving ? t("caja.session.opening") : t("caja.session.open")}
                </ZHBtn>
                <ZHBtn
                  variant="secondary"
                  onClick={() => ctx.setTab("listado")}
                >
                  {t("common.cancel")}
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
                {t("common.back")}
              </ZHBtn>
              <h3>{t("caja.session.title")}</h3>
              <Badge
                variant={statusBadge(ctx.viewing.status)}
                label={statusLabel(ctx.viewing.status)}
              />
              <div className="cj-detail-header-spacer" />
              {ctx.viewing.status === "Open" && (
                <>
                  {/* TREASURY-CASH-MANUAL-MOVEMENTS-COMPANY-SETTING-05 / -PERMISSION-06 — el
                      botón (y por lo tanto el modal, más abajo) solo existen si se cumplen las
                      tres condiciones: la empresa activa permite movimientos manuales, el usuario
                      tiene el permiso `caja.record`, y el turno está abierto (ya cubierto por la
                      condición externa de este bloque). El catálogo de motivos, el modal y el
                      endpoint son exactamente los mismos, solo cambia si se muestran. */}
                  {ctx.allowManualMovements && ctx.canRecordManualMovements && (
                    <ZHBtn variant="secondary" onClick={ctx.openMovementModal}>
                      <span className="material-symbols-outlined zh-icon-md">
                        add
                      </span>{" "}
                      {t("caja.movements.recordButton")}
                    </ZHBtn>
                  )}
                  <ZHBtn variant="destructive" onClick={ctx.startClose}>
                    {t("caja.session.close")}
                  </ZHBtn>
                </>
              )}
            </div>

            {/* Sucursal / Caja / Punto de emisión: solo informativos. El punto de emisión viene
                exclusivamente de CashSessionDto (emissionPointCodeSnapshot) — nunca de un lookup
                manual a EmissionPoint. */}
            <div className="cj-summary-grid">
              <SummaryCard label={t("caja.session.branch")} value={ctx.branchName ?? "—"} />
              <SummaryCard
                label={t("caja.session.register")}
                value={`${ctx.viewing.cashRegisterCodeSnapshot} — ${ctx.viewing.cashRegisterNameSnapshot}`}
              />
              <SummaryCard
                label={t("caja.session.emissionPoint")}
                value={ctx.viewing.emissionPointCodeSnapshot}
              />
              <SummaryCard
                label={t("common.status")}
                value={statusLabel(ctx.viewing.status)}
              />
            </div>

            {/* CASH-SESSION-COLLECTION-SUMMARY-UX-02 — "Efectivo físico" es la única sección que
                afecta apertura/cierre/arqueo (CashSession/CashMovement). Todo lo que sigue debajo
                (Resumen de ventas y cobros / Cobros por forma) es informativo y nunca cambia estos
                valores, aunque haya ventas por Transferencia/Tarjeta/Cheque/Crédito.
                TREASURY-CASH-ARCHITECTURE-I18N-AUDIT-04 — esta misma regla, ya documentada para
                desarrolladores arriba, se explica también al usuario (mismo patrón ZHPageNotice
                variant="info" que documentFlows.separationNotice): esta confusión ya generó un
                audit dedicado (AUDIT-CASH-SESSION-COLLECTION-SUMMARY-MISMATCH-01). */}
            <ZHPageNotice
              variant="info"
              message={t(
                "caja.session.separationNotice")}
            />
            <h4 className="cj-section-title">{t("caja.session.physicalCash")}</h4>
            <div className="cj-summary-grid">
              <SummaryCard
                label={t("caja.movementType.opening")}
                value={formatMoneyWithSymbol(ctx.viewing.openingAmount, moneyDecimals)}
              />
              <SummaryCard
                label={t("caja.session.income")}
                value={formatMoneyWithSymbol(ctx.viewing.totalIncome, moneyDecimals)}
              />
              <SummaryCard
                label={t("caja.session.expense")}
                value={formatMoneyWithSymbol(ctx.viewing.totalExpense, moneyDecimals)}
              />
              <SummaryCard
                label={t("caja.session.expectedBalance")}
                value={formatMoneyWithSymbol(ctx.viewing.currentBalance, moneyDecimals)}
                highlight
              />
              {ctx.viewing.status === "Closed" && (
                <>
                  <SummaryCard
                    label={t("caja.session.expected")}
                    value={formatMoneyWithSymbol(ctx.viewing.expectedAmount ?? 0, moneyDecimals)}
                  />
                  <SummaryCard
                    label={t("caja.session.counted")}
                    value={formatMoneyWithSymbol(ctx.viewing.countedAmount ?? 0, moneyDecimals)}
                  />
                  <SummaryCard
                    label={t("caja.session.difference")}
                    value={formatMoneyWithSymbol(ctx.viewing.difference ?? 0, moneyDecimals)}
                    highlight={(ctx.viewing.difference ?? 0) !== 0}
                  />
                </>
              )}
            </div>

            {/* Informativo: ventas/cobros del turno, separado del efectivo físico de arriba.
                "Cobros del turno" != "efectivo físico de caja" — Transferencia/Tarjeta/Cheque/
                Crédito aparecen aquí pero nunca modifican Apertura/Ingresos/Egresos/Saldo. */}
            <h4 className="cj-section-title cj-section-title--spaced">{t("caja.session.salesCollections")}</h4>
            {ctx.collectionSummaryLoading && !ctx.collectionSummary ? (
              <p className="cj-collection-loading">{t("caja.session.loadingCollections")}</p>
            ) : (
              <>
                <div className="cj-summary-grid">
                  <SummaryCard
                    label={t("caja.session.authorizedInvoices")}
                    value={String(ctx.collectionSummary?.invoiceCount ?? 0)}
                  />
                  <SummaryCard
                    label={t("caja.session.totalInvoiced")}
                    value={formatMoneyWithSymbol(ctx.collectionSummary?.totalInvoiced ?? 0, moneyDecimals)}
                  />
                  <SummaryCard
                    label={t("caja.session.totalCollected")}
                    value={formatMoneyWithSymbol(ctx.collectionSummary?.totalCollected ?? 0, moneyDecimals)}
                  />
                  <SummaryCard
                    label={t("caja.session.creditSales")}
                    value={formatMoneyWithSymbol(ctx.collectionSummary?.totalCredit ?? 0, moneyDecimals)}
                  />
                </div>
                <h5 className="cj-collection-detail-title">{t("caja.session.collectionsByMethod")}</h5>
                <ZHDataTable
                  columns={collectionByMethodColumns(ctx.collectionSummary?.totalCollected ?? 0)}
                  rows={ctx.collectionSummary?.byPaymentMethod ?? []}
                  rowKey={(m) => m.paymentMethodId}
                  tableClassName="table--compact table--neutral"
                  emptyMessage={t("caja.session.emptyCollections")}
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
                          {t("caja.session.methodDetail", { method: expanded.paymentMethodName })}
                        </h5>
                        <ZHDataTable
                          columns={collectionDetailColumns}
                          rows={expanded.details}
                          rowKey={(d) => `${d.invoiceId}-${expanded.paymentMethodId}`}
                          tableClassName="table--compact table--neutral"
                          emptyMessage={t("caja.session.emptyInvoices")}
                        />
                      </div>
                    );
                  })()}
              </>
            )}

            <h4 className="cj-section-title">{t("caja.session.movements")}</h4>
            <ZHDataTable
              columns={sessionMovementColumns}
              rows={ctx.viewing.movements}
              rowKey={(m) => m.id}
              tableClassName="table--compact table--neutral"
              emptyMessage={t("caja.session.emptyMovements")}
            />

            {ctx.viewing.status === "Closed" &&
              ctx.viewing.closingCounts.length > 0 && (
                <>
                  <h4 className="cj-section-title cj-section-title--spaced">
                    {t("caja.session.cashCount")}
                  </h4>
                  <ZHDataTable
                    columns={arqueoColumns}
                    rows={ctx.viewing.closingCounts}
                    rowKey={(c) => c.id}
                    tableClassName="table--compact table--neutral cj-arqueo-table"
                  />
                  {ctx.viewing.closeNotes && (
                    <p className="cj-close-notes">
                      <strong>{t("caja.session.notes")}:</strong> {ctx.viewing.closeNotes}
                    </p>
                  )}
                </>
              )}

            {/* TREASURY-CASH-MANUAL-MOVEMENT-SHARED-MODAL-07 — el markup vive en
                ManualCashMovementModal (componente compartido, puramente presentacional);
                CajaPage/useCajaPage siguen siendo los únicos dueños de las reglas de habilitación
                (empresa lo permite, usuario tiene `caja.record`, turno abierto — ver guard en
                openMovementModal) y de toda la lógica de negocio (endpoint, TenantId/CompanyId,
                refresco de detalle/saldo/movimientos/resumen). Solo alcanzable con turno abierto
                (botón "Registrar movimiento" arriba). */}
            {ctx.viewing.status === "Open" &&
              ctx.allowManualMovements &&
              ctx.canRecordManualMovements && (
                <ManualCashMovementModal
                  open={ctx.movementModalOpen}
                  saving={ctx.movementSaving}
                  saveError={ctx.movementSaveError}
                  register={ctx.movementForm.register}
                  errors={ctx.movementForm.formState.errors}
                  selectedMovementType={ctx.selectedMovementType}
                  movementTypes={ctx.movementTypes}
                  reasons={ctx.reasons}
                  reasonsLoading={ctx.reasonsLoading}
                  onSubmit={ctx.handleRecordMovement}
                  onClose={ctx.closeMovementModal}
                />
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
                {t("common.back")}
              </ZHBtn>
              <h3>{t("caja.session.closeCount")}</h3>
            </div>

            <div className="cj-close-summary">
              <div>
                <strong>{t("caja.session.expectedBalance")}:</strong>{" "}
                {formatMoneyWithSymbol(ctx.viewing.currentBalance, moneyDecimals)}
              </div>
              <div>
                <strong>{t("caja.session.counted")}:</strong>{" "}
                {formatMoneyWithSymbol(ctx.countedTotal, moneyDecimals)}
              </div>
              <div
                className={
                  ctx.countedTotal - ctx.viewing.currentBalance !== 0
                    ? "cj-close-diff--mismatch"
                    : "cj-close-diff--ok"
                }
              >
                <strong>{t("caja.session.difference")}:</strong>{" "}
                {formatMoneyWithSymbol(ctx.countedTotal - ctx.viewing.currentBalance, moneyDecimals)}
              </div>
            </div>

            <form onSubmit={ctx.handleClose}>
              <table className="pf-table zh-mb-16">
                <thead>
                  <tr>
                    <th>{t("caja.session.denomination")}</th>
                    <th className="zh-text-align-center">{t("caja.session.quantity")}</th>
                    <th className="zh-text-align-right">{t("common.total")}</th>
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
                        {formatMoneyWithSymbol(c.denominationValue * c.quantity, moneyDecimals)}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr>
                    <td colSpan={2} className="cj-arqueo-total">
                      {t("caja.session.totalCounted")}
                    </td>
                    <td className="zh-table-cell--num cj-arqueo-total">
                      {formatMoneyWithSymbol(ctx.countedTotal, moneyDecimals)}
                    </td>
                  </tr>
                </tfoot>
              </table>

              <ZHField
                density="compact"
                className="zh-mb-12"
                label={t("caja.session.closeNotes")}
              >
                <ZhTextarea {...ctx.closeForm.register("closeNotes")} rows={2} />
              </ZHField>

              <div className="cj-actions">
                <ZHBtn
                  variant="destructive"
                  type="submit"
                  disabled={ctx.saving}
                >
                  {ctx.saving ? t("caja.session.closingProgress") : t("caja.session.confirmClose")}
                </ZHBtn>
                <ZHBtn
                  variant="secondary"
                  onClick={() => ctx.setTab("detalle")}
                >
                  {t("common.cancel")}
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
