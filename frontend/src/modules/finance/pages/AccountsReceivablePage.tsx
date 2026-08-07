import { useCallback, useEffect, useState } from "react";

import { Badge } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatMoney } from "../../../lib/sanitizers";
import { receivableService, type SalesReceivableDto } from "../api/receivableService";
import { RegisterCollectionModal } from "../components/RegisterCollectionModal";

import "../../../styles/shared/items-catalog.css";

/**
 * P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) â€” pantalla mÃ­nima de Cuentas por Cobrar: consulta,
 * selecciÃ³n de la deuda y registro de cobro contra ella (RegisterCollectionModal).
 * No implementa devoluciones, notas de crÃ©dito ni reportes â€” fuera de este alcance.
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
      /* la tabla queda vacÃ­a; el usuario puede reintentar con el botÃ³n Actualizar */
    }
    setLoading(false);
  }, [status]);

  useEffect(() => {
    fetchItems();
  }, [fetchItems]);

  return (
    <ErpPageTemplate
      title="Cuentas por Cobrar"
      subtitle="Consulta las facturas de venta a crÃ©dito pendientes y registra cobros."
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
                <th>Cliente</th>
                <th>Monto original</th>
                <th>Cobrado</th>
                <th>Saldo pendiente</th>
                <th>Estado</th>
                <th>Creada</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {items.map((r) => (
                <tr key={r.id}>
                  <td>{r.customerId}</td>
                  <td>{formatMoney(r.originalAmount)}</td>
                  <td>{formatMoney(r.paidAmount)}</td>
                  <td>
                    <strong>{formatMoney(r.balanceDue)}</strong>
                  </td>
                  <td>
                    <Badge label={"Estado"} variant="neutral" />
                  </td>
                  <td>{formatDate(r.createdAt)}</td>
                  <td className="prd-td-actions">
                    {r.balanceDue > 0 && r.status !== "cancelled" && (
                      <ZHBtn onClick={() => setSelected(r)}>
                        <span className="material-symbols-outlined zh-icon-md">
                          payments
                        </span>
                        Registrar cobro
                      </ZHBtn>
                    )}
                  </td>
                </tr>
              ))}
              {items.length === 0 && (
                <tr className="prd-empty-row">
                  <td colSpan={7}>Sin cuentas por cobrar.</td>
                </tr>
              )}
            </tbody>
          </table>
        )}
        {!loading && total > items.length && (
          <p className="zh-text-muted">
            Mostrando {items.length} de {total} â€” refina el filtro de estado para ver mÃ¡s.
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





