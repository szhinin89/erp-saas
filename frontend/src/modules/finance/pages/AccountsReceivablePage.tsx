import { useCallback, useEffect, useState } from "react";

import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import {
  ZHDataTable,
  type ZHDataTableColumn,
} from "../../../components/zh/ZHDataTable";
import {
  formatDate,
  formatDateTime,
} from "../../../lib/formatters/dateFormatters";
import {
  receivableService,
  type SalesReceivableDto,
} from "../api/receivableService";
import { RegisterCollectionModal } from "../components/RegisterCollectionModal";
import { ReverseCollectionModal } from "../components/ReverseCollectionModal";
import { ReceivableCustomerAdvanceModal } from "../components/ReceivableCustomerAdvanceModal";
import { ReceivableRowActions } from "../components/ReceivableRowActions";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useSessionStore } from "../../../store/sessionStore";

import "../../../styles/shared/items-catalog.css";
import "../styles/receivables-list.css";

/**
 * FINANCE-RECEIVABLES-LIST-ENTERPRISE-01 — deriva el label/color del badge a partir de
 * `statusLabel` (ya calculado por el backend desde status persistido + saldo + mora — ver
 * SalesReceivableDtoMapper). Mismo patrón que `PayableStatusBadge` en el módulo genérico de CxP
 * (`modules/payables`), adaptado a los 5 estados de CxC: Pendiente/Parcial/Pagada/Vencida/Anulada.
 */
function getReceivableStatusBadge(r: SalesReceivableDto): {
  label: string;
  variant: BadgeVariant;
} {
  switch (r.statusLabel) {
    case "Anulada":
      return { label: r.statusLabel, variant: "gray" };
    case "Pagada":
      return { label: r.statusLabel, variant: "green" };
    case "Vencida":
      return { label: r.statusLabel, variant: "red" };
    case "Parcial":
      return { label: r.statusLabel, variant: "blue" };
    default:
      return { label: r.statusLabel, variant: "orange" };
  }
}

/**
 * P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — pantalla mínima de Cuentas por Cobrar: consulta,
 * selección de la deuda y registro de cobro contra ella (RegisterCollectionModal).
 * No implementa devoluciones, notas de crédito ni reportes — fuera de este alcance.
 */
export function AccountsReceivablePage() {
  const { isAdminRole, canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canSeeAdvances = canShow("finance.view");
  const [advanceTarget, setAdvanceTarget] = useState<SalesReceivableDto | null>(
    null,
  );
  const permissions = useSessionStore((s) => s.authorization?.permissions);
  const canReverse =
    isAdminRole ||
    !!permissions?.some(
      (p) => p === "*" || p === "finance.collections.reverse",
    );
  const [reverseTarget, setReverseTarget] = useState<SalesReceivableDto | null>(
    null,
  );
  const [items, setItems] = useState<SalesReceivableDto[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState("pending");
  const [selected, setSelected] = useState<SalesReceivableDto | null>(null);

  const fetchItems = useCallback(async () => {
    setLoading(true);
    try {
      const res = await receivableService.list(status || undefined, 1, 50);
      setItems(res.items);
      setTotal(res.total);
    } catch {
      /* la tabla queda vacía; el usuario puede reintentar con el botón Actualizar */
    }
    setLoading(false);
  }, [status]);

  useEffect(() => {
    fetchItems();
  }, [fetchItems]);

  const text = (key: string, params?: Record<string, string>) =>
    t(`finance.receivables.list.${key}`, params);
  // Saldo inicial (sin factura): emisión propia del documento, no la fecha de carga.
  const issuedAt = (r: SalesReceivableDto) =>
    r.invoiceId
      ? formatDateTime(r.invoiceCreatedAt)
      : formatDate(r.invoiceIssueDate);
  const dueDate = (r: SalesReceivableDto) =>
    r.dueDate ? formatDate(r.dueDate) : "—";
  const branchName = (r: SalesReceivableDto) =>
    r.branchName || text("branchUnavailable");
  const issuedBy = (r: SalesReceivableDto) =>
    text("issuedBy", { user: r.createdByName || text("userUnavailable") });
  const statusBadge = (r: SalesReceivableDto) => (
    <Badge {...getReceivableStatusBadge(r)} />
  );
  const amountRow = (
    key: string,
    value: number,
    emphasis: "muted" | "total",
  ) => (
    <ZHInfoRow
      label={<ZHDataValue variant="muted">{text(key)}</ZHDataValue>}
      value={
        <ZHMoneyValue precision="money" value={value} emphasis={emphasis} />
      }
    />
  );

  // ZH-LISTING-MAIN-ROW-NUMBER-FIX-07: showRowNumber activo — "Factura" sigue siendo el
  // identificador funcional del documento; "N°" es solo el índice visual de fila.
  // Importes agrupados en "Valores" por decisión del propietario (excepción a la regla de una
  // columna por importe): se leen juntos dentro de cada documento.
  const receivableColumns: ZHDataTableColumn<SalesReceivableDto>[] = [
    {
      key: "invoice",
      header: text("col.invoice"),
      cellClassName: "fin-receivables__col-wide",
      render: (r) => (
        <div className="fin-receivables__stack">
          <ZHDataValue variant="code" className="fin-receivables__nowrap">
            {r.invoiceNumber}
          </ZHDataValue>
          <ZHDataValue variant="muted" className="fin-receivables__nowrap">
            {issuedAt(r)}
          </ZHDataValue>
        </div>
      ),
    },
    {
      key: "customer",
      header: text("col.customer"),
      render: (r) => (
        <div className="fin-receivables__stack">
          <ZHDataValue>{r.customerName}</ZHDataValue>
          <ZHDataValue variant="code" className="fin-receivables__nowrap">
            {r.customerIdentification}
          </ZHDataValue>
          <div className="fin-receivables__compact">
            <div className="fin-receivables__line">
              {statusBadge(r)}
              <ZHDataValue variant="muted" className="fin-receivables__nowrap">
                {text("due", { date: dueDate(r) })}
              </ZHDataValue>
            </div>
            <div className="fin-receivables__line">
              <ZHDataValue variant="muted" className="fin-receivables__nowrap">
                {text("invoice", { number: r.invoiceNumber })}
              </ZHDataValue>
              <ZHDataValue variant="muted" className="fin-receivables__nowrap">
                {issuedAt(r)}
              </ZHDataValue>
            </div>
            <div className="fin-receivables__line">
              <ZHDataValue variant="muted">{branchName(r)}</ZHDataValue>
              <ZHDataValue variant="muted">{issuedBy(r)}</ZHDataValue>
            </div>
          </div>
        </div>
      ),
    },
    {
      key: "branch",
      header: text("col.branch"),
      cellClassName: "fin-receivables__col-wide",
      render: (r) => (
        <div className="fin-receivables__stack">
          <ZHDataValue>{branchName(r)}</ZHDataValue>
          <ZHDataValue variant="muted">{issuedBy(r)}</ZHDataValue>
        </div>
      ),
    },
    {
      key: "due",
      header: text("col.due"),
      cellClassName: "fin-receivables__col-wide",
      render: (r) => (
        <div className="fin-receivables__stack">
          <ZHDataValue className="fin-receivables__nowrap">
            {dueDate(r)}
          </ZHDataValue>
          {statusBadge(r)}
        </div>
      ),
    },
    {
      key: "values",
      header: text("col.values"),
      align: "right",
      render: (r) => (
        <div className="fin-receivables__values">
          {amountRow("original", r.originalAmount, "muted")}
          {amountRow("paid", r.paidAmount, "muted")}
          {amountRow("balance", r.balanceDue, "total")}
        </div>
      ),
    },
    {
      key: "actions",
      header: text("col.actions"),
      cellClassName: "zh-table-cell--actions",
      render: (r) => (
        <ReceivableRowActions
          receivable={r}
          canReverse={canReverse}
          canSeeAdvances={canSeeAdvances}
          onCollect={setSelected}
          onReverse={setReverseTarget}
          onAdvances={setAdvanceTarget}
        />
      ),
    },
  ];

  return (
    <ErpPageTemplate title={text("title")} subtitle={text("subtitle")}>
      <div className="prd-section fin-receivables">
        <div className="prd-crud-toolbar">
          <ZhSelect
            className="zh-input prd-status-filter"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            <option value="pending">{text("filter.pending")}</option>
            <option value="paid">{text("filter.paid")}</option>
            <option value="cancelled">{text("filter.cancelled")}</option>
            <option value="">{text("filter.all")}</option>
          </ZhSelect>
          <ZHBtn onClick={fetchItems} disabled={loading}>
            <span className="material-symbols-outlined zh-icon-lg">
              refresh
            </span>
          </ZHBtn>
        </div>

        <ZHDataTable
          columns={receivableColumns}
          rows={items}
          rowKey={(r) => r.id}
          loading={loading}
          showRowNumber
          tableClassName="table--compact table--align-top"
          emptyMessage={text("empty")}
        />
        {!loading && total > items.length && (
          <p className="zh-text-muted">
            {text("truncated", {
              shown: String(items.length),
              total: String(total),
            })}
          </p>
        )}
      </div>

      <RegisterCollectionModal
        open={selected !== null}
        receivable={selected}
        onClose={() => setSelected(null)}
        onRegistered={fetchItems}
      />
      {reverseTarget && (
        <ReverseCollectionModal
          receivable={reverseTarget}
          onClose={() => setReverseTarget(null)}
          onReversed={fetchItems}
        />
      )}
      {advanceTarget && (
        <ReceivableCustomerAdvanceModal
          receivable={advanceTarget}
          onClose={() => setAdvanceTarget(null)}
          onApplied={fetchItems}
        />
      )}
    </ErpPageTemplate>
  );
}
