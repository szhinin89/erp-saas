import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { roundToDecimals } from "../../../lib/sanitizers";
import type { RepricingRow } from "../hooks/useSalesCustomerRepricing";

/**
 * Tabla "Producto | Precio actual | Nuevo precio" del modal de cambio de cliente. Ambos precios
 * son UnitPrice, así que se muestran siempre con los decimales configurados para precio de venta
 * (`decimals` = salesUnitPriceDecimals) — nunca un formato fijo.
 */
export function SalesRepricingTable({
  rows,
  decimals,
}: {
  rows: RepricingRow[];
  decimals: number;
}) {
  return (
    <div className="zh-table-wrap">
      <table className="zh-table zh-table--compact">
        <thead>
          <tr>
            <th>Producto</th>
            <th>Precio actual</th>
            <th>Nuevo precio</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.key}>
              <td>{row.description}</td>
              <td>
                <ZHMoneyValue
                  value={roundToDecimals(row.currentUnitPrice, decimals)}
                  decimals={decimals}
                />
              </td>
              <td>
                <ZHMoneyValue
                  value={roundToDecimals(row.fields.unitPrice, decimals)}
                  decimals={decimals}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
