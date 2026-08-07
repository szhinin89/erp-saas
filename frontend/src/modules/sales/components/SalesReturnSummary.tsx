import { ZHCard } from "../../../components/zh/ZHCard";
import { formatMoney } from "../../../lib/sanitizers";
import type { SalesReturnDto } from "../api/salesReturnService";
import "../../../styles/shared/erp-form-core.css";

interface Props {
  salesReturn: SalesReturnDto;
  decimals: number;
}

/**
 * Muestra lo ya congelado/persistido de una devolución: líneas (una vez
 * autorizada), resumen de impuestos/total y asignaciones de reembolso.
 * Nunca recalcula nada — todos los valores vienen tal cual del servidor.
 */
export function SalesReturnSummary({ salesReturn, decimals }: Props) {
  return (
    <>
      {salesReturn.status !== "Draft" && (
        <ZHCard title="Líneas devueltas">
          <div className="table-scroll">
            <table className="table table--compact table--neutral sr-lines-table">
              <thead>
                <tr>
                  <th>Producto</th>
                  <th className="zh-text-align-right">Cantidad</th>
                  <th className="zh-text-align-right">P. unitario</th>
                  <th className="zh-text-align-right">IVA</th>
                  <th className="zh-text-align-right">ICE</th>
                  <th className="zh-text-align-right">Total línea</th>
                </tr>
              </thead>
              <tbody>
                {salesReturn.lines.map((line) => (
                  <tr key={line.id}>
                    <td>
                      <div className="sr-lines-table__desc">{line.description}</div>
                      {line.snapshotSku && (
                        <div className="sr-lines-table__sku">{line.snapshotSku}</div>
                      )}
                    </td>
                    <td className="zh-table-cell--num">{line.quantity}</td>
                    <td className="zh-table-cell--num">
                      {formatMoney(line.unitPrice, decimals)}
                    </td>
                    <td className="zh-table-cell--num">
                      {formatMoney(line.vatAmount, decimals)}
                    </td>
                    <td className="zh-table-cell--num">
                      {formatMoney(line.iceAmount, decimals)}
                    </td>
                    <td className="zh-table-cell--num">
                      {formatMoney(line.taxInclusiveTotal, decimals)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ZHCard>
      )}

      <ZHCard title="Resumen de impuestos y total">
        <div className="sr-totals-grid">
          <div>
            <span className="sr-general-grid__label">Subtotal</span>
            <span className="sr-general-grid__value">
              {formatMoney(salesReturn.subtotal, decimals)}
            </span>
          </div>
          <div>
            <span className="sr-general-grid__label">Descuento</span>
            <span className="sr-general-grid__value">
              -{formatMoney(salesReturn.totalDiscount, decimals)}
            </span>
          </div>
          <div>
            <span className="sr-general-grid__label">IVA</span>
            <span className="sr-general-grid__value">
              {formatMoney(salesReturn.totalVat, decimals)}
            </span>
          </div>
          <div>
            <span className="sr-general-grid__label">ICE</span>
            <span className="sr-general-grid__value">
              {formatMoney(salesReturn.totalIce, decimals)}
            </span>
          </div>
          <div className="sr-totals-grid__grand">
            <span className="sr-general-grid__label">Total a reembolsar</span>
            <span className="sr-general-grid__value">
              {formatMoney(salesReturn.grandTotal, decimals)}
            </span>
          </div>
        </div>
      </ZHCard>

      {salesReturn.refundAllocations.length > 0 && (
        <ZHCard title="Asignación de reembolso">
          <div className="table-scroll">
            <table className="table table--compact table--neutral">
              <thead>
                <tr>
                  <th>Forma de reembolso</th>
                  <th className="zh-text-align-right">Monto</th>
                </tr>
              </thead>
              <tbody>
                {salesReturn.refundAllocations.map((a) => (
                  <tr key={a.id}>
                    <td>
                      {a.method === "Cash"
                        ? "Efectivo (Caja)"
                        : "Crédito a Cuenta por Cobrar"}
                    </td>
                    <td className="zh-table-cell--num">{formatMoney(a.amount, decimals)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ZHCard>
      )}
    </>
  );
}
