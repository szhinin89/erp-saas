import { useCallback, useEffect, useState } from "react";

import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
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
 * SalesReceivableDtoMapper). Mismo patrón que `getPayableStatusBadge` en AccountsPayablePage
 * (CxP), adaptado a los 5 estados de CxC: Pendiente/Parcial/Pagada/Vencida/Anulada.
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

        {loading ? (
          <p>Cargando...</p>
        ) : (
          <table className="prd-crud-table">
            <thead>
              <tr>
                <th>Factura</th>
                <th>Cliente</th>
                <th>Identificación</th>
                <th>Sucursal</th>
                <th>Emitido por</th>
                <th>Fecha factura</th>
                <th>Vence</th>
                <th>Monto original</th>
                <th>Cobrado</th>
                <th>Saldo pendiente</th>
                <th>Estado</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {items.map((r) => {
                const statusBadge = getReceivableStatusBadge(r);
                return (
                  <tr key={r.id}>
                    <td className="mono">{r.invoiceNumber}</td>
                    <td>{r.customerName}</td>
                    <td className="mono">{r.customerIdentification}</td>
                    <td>{r.branchName || "Sucursal no disponible"}</td>
                    <td>{r.createdByName || "Usuario no disponible"}</td>
                    <td>{formatDateTime(r.invoiceCreatedAt)}</td>
                    <td>{r.dueDate ? formatDate(r.dueDate) : "—"}</td>
                    <td>
                      <ZHMoneyValue value={r.originalAmount} />
                    </td>
                    <td>
                      <ZHMoneyValue value={r.paidAmount} />
                    </td>
                    <td>
                      <ZHMoneyValue value={r.balanceDue} emphasis="strong" />
                    </td>
                    <td>
                      <Badge label={statusBadge.label} variant={statusBadge.variant} />
                    </td>
                    <td className="prd-td-actions">
                      {r.balanceDue > 0 && r.status !== "cancelled" ? (
                        <ZHBtn onClick={() => setSelected(r)}>
                          <span className="material-symbols-outlined zh-icon-md">
                            payments
                          </span>
                          Registrar cobro
                        </ZHBtn>
                      ) : null}
                    </td>
                  </tr>
                );
              })}
              {items.length === 0 && (
                <tr className="prd-empty-row">
                  <td colSpan={12}>Sin cuentas por cobrar.</td>
                </tr>
              )}
            </tbody>
          </table>
        )}
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
