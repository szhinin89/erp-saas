import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
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
                        <div className="sr-lines-table__sku zh-code-value">{line.snapshotSku}</div>
                      )}
                    </td>
                    <td className="zh-table-cell--num">{line.quantity}</td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue
                        value={line.unitPrice}
                        decimals={decimals}
                        currencySymbol=""
                      />
                    </td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue
                        value={line.vatAmount}
                        decimals={decimals}
                        currencySymbol=""
                      />
                    </td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue
                        value={line.iceAmount}
                        decimals={decimals}
                        currencySymbol=""
                      />
                    </td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue
                        value={line.taxInclusiveTotal}
                        decimals={decimals}
                        currencySymbol=""
                      />
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
              <ZHMoneyValue
                value={salesReturn.subtotal}
                decimals={decimals}
                currencySymbol=""
              />
            </span>
          </div>
          <div>
            <span className="sr-general-grid__label">Descuento</span>
            <span className="sr-general-grid__value">
              -
              <ZHMoneyValue
                value={salesReturn.totalDiscount}
                decimals={decimals}
                currencySymbol=""
              />
            </span>
          </div>
          <div>
            <span className="sr-general-grid__label">IVA</span>
            <span className="sr-general-grid__value">
              <ZHMoneyValue
                value={salesReturn.totalVat}
                decimals={decimals}
                currencySymbol=""
              />
            </span>
          </div>
          <div>
            <span className="sr-general-grid__label">ICE</span>
            <span className="sr-general-grid__value">
              <ZHMoneyValue
                value={salesReturn.totalIce}
                decimals={decimals}
                currencySymbol=""
              />
            </span>
          </div>
          <div className="sr-totals-grid__grand">
            <span className="sr-general-grid__label">Total a reembolsar</span>
            <span className="sr-general-grid__value">
              <ZHMoneyValue
                value={salesReturn.grandTotal}
                decimals={decimals}
                currencySymbol=""
              />
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
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue
                        value={a.amount}
                        decimals={decimals}
                        currencySymbol=""
                      />
                    </td>
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
