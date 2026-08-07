import { useCallback, useEffect, useState } from "react";

import { Badge } from "../../../components/PageShell";
import { useNavigate } from "react-router-dom";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatMoney } from "../../../lib/sanitizers";
import { payableService, type PurchasePayableDto } from "../api/payableService";
import { RegisterPaymentModal } from "../components/RegisterPaymentModal";

import "../../../styles/shared/items-catalog.css";

/**
 * P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) â€” pantalla mÃ­nima de Cuentas por Pagar: consulta,
 * selecciÃ³n de la deuda y registro de pago contra ella (RegisterPaymentModal). Mismo patrÃ³n que
 * AccountsReceivablePage (CxC).
 */
export function AccountsPayablePage() {
  const navigate = useNavigate();
  const [items, setItems] = useState<PurchasePayableDto[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState("pending");
  const [selected, setSelected] = useState<PurchasePayableDto | null>(null);

  const fetchItems = useCallback(async () => {
    setLoading(true);
    try {
      const res = await payableService.list(status || undefined, undefined, 1, 50);
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
      title="Cuentas por Pagar"
      subtitle="Consulta las facturas de compra a crÃ©dito pendientes y registra pagos."
      action={
        <ZHBtn
          type="button"
          variant="ghost"
          onClick={() => navigate("/finance/supplier-credits")}
        >
          CrÃ©ditos de proveedor
        </ZHBtn>
      }
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
                <th>Proveedor</th>
                <th>Monto total</th>
                <th>Pagado</th>
                <th>Retenido</th>
                <th>Saldo pendiente</th>
                <th>Estado</th>
                <th>Creada</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {items.map((p) => (
                <tr key={p.id}>
                  <td>{p.supplierId}</td>
                  <td>{formatMoney(p.totalAmount)}</td>
                  <td>{formatMoney(p.paidAmount)}</td>
                  <td>{formatMoney(p.totalRetained)}</td>
                  <td>
                    <strong>{formatMoney(p.balanceDue)}</strong>
                  </td>
                  <td>
                    <Badge label={"Estado"} variant="neutral" />
                  </td>
                  <td>{formatDate(p.createdAt)}</td>
                  <td className="prd-td-actions">
                    {p.balanceDue > 0 && p.status !== "cancelled" && (
                      <ZHBtn onClick={() => setSelected(p)}>
                        <span className="material-symbols-outlined zh-icon-md">
                          payments
                        </span>
                        Registrar pago
                      </ZHBtn>
                    )}
                  </td>
                </tr>
              ))}
              {items.length === 0 && (
                <tr className="prd-empty-row">
                  <td colSpan={8}>Sin cuentas por pagar.</td>
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

      <RegisterPaymentModal
        open={selected !== null}
        payable={selected}
        onClose={() => setSelected(null)}
        onRegistered={fetchItems}
      />
    </ErpPageTemplate>
  );
}





