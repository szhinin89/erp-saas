import { useCallback, useEffect, useState } from "react";

import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import {
  formatDate,
  formatDateTime,
} from "../../../lib/formatters/dateFormatters";
import { receivableService, type SalesReceivableDto } from "../api/receivableService";
import { RegisterCollectionModal } from "../components/RegisterCollectionModal";

import "../../../styles/shared/items-catalog.css";

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

  // ZH-LISTING-GLOBAL-STANDARD-06: sin showRowNumber — "Factura" ya es el número funcional
  // del documento, un N° de referencia visual sería redundante.
  const receivableColumns: ZHDataTableColumn<SalesReceivableDto>[] = [
    { key: "invoice", header: "Factura", render: (r) => <span className="mono">{r.invoiceNumber}</span> },
    { key: "customer", header: "Cliente", render: (r) => r.customerName },
    { key: "identification", header: "Identificación", render: (r) => <span className="mono">{r.customerIdentification}</span> },
    { key: "branch", header: "Sucursal", render: (r) => r.branchName || "Sucursal no disponible" },
    { key: "createdBy", header: "Emitido por", render: (r) => r.createdByName || "Usuario no disponible" },
    { key: "invoiceDate", header: "Fecha factura", render: (r) => formatDateTime(r.invoiceCreatedAt) },
    { key: "dueDate", header: "Vence", render: (r) => (r.dueDate ? formatDate(r.dueDate) : "—") },
    { key: "originalAmount", header: "Monto original", render: (r) => <ZHMoneyValue value={r.originalAmount} /> },
    { key: "paidAmount", header: "Cobrado", render: (r) => <ZHMoneyValue value={r.paidAmount} /> },
    { key: "balanceDue", header: "Saldo pendiente", render: (r) => <ZHMoneyValue value={r.balanceDue} emphasis="strong" /> },
    {
      key: "status",
      header: "Estado",
      render: (r) => {
        const statusBadge = getReceivableStatusBadge(r);
        return <Badge label={statusBadge.label} variant={statusBadge.variant} />;
      },
    },
    {
      key: "actions",
      header: "Acciones",
      render: (r) =>
        r.balanceDue > 0 && r.status !== "cancelled" ? (
          <ZHBtn onClick={() => setSelected(r)} aria-label={`Registrar cobro de factura ${r.invoiceNumber}`}>
            <span className="material-symbols-outlined zh-icon-md">payments</span>
            Registrar cobro
          </ZHBtn>
        ) : null,
    },
  ];

  return (
    <ErpPageTemplate
      title="Cuentas por Cobrar"
      subtitle="Consulta las facturas de venta a crédito pendientes y registra cobros."
    >
      <div className="prd-section">
        <div className="prd-crud-toolbar">
          <ZhSelect
            className="zh-input prd-status-filter"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            <option value="pending">Pendientes</option>
            <option value="paid">Pagadas</option>
            <option value="cancelled">Anuladas</option>
            <option value="">Todas</option>
          </ZhSelect>
          <ZHBtn onClick={fetchItems} disabled={loading}>
            <span className="material-symbols-outlined zh-icon-lg">refresh</span>
          </ZHBtn>
        </div>

        <ZHDataTable
          columns={receivableColumns}
          rows={items}
          rowKey={(r) => r.id}
          loading={loading}
          emptyMessage="Sin cuentas por cobrar."
        />
        {!loading && total > items.length && (
          <p className="zh-text-muted">
            Mostrando {items.length} de {total} — refina el filtro de estado para ver más.
          </p>
        )}
      </div>

      <RegisterCollectionModal
        open={selected !== null}
        receivable={selected}
        onClose={() => setSelected(null)}
        onRegistered={fetchItems}
      />
    </ErpPageTemplate>
  );
}
