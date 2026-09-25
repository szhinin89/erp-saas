import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import type { RepricingRow } from "../hooks/useSalesCustomerRepricing";

/**
 * Tabla "Producto | Precio actual | Nuevo precio" del modal de cambio de cliente. Ambos precios
 * son UnitPrice: declaran `precision="salesUnitPrice"` (ZH-DESIGN-SYSTEM-PRECISION-04E) y el
 * Design System resuelve la escala de la PrecisionPolicy — nunca un formato fijo ni una prop.
 */
export function SalesRepricingTable({ rows }: { rows: RepricingRow[] }) {
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
                <ZHMoneyValue value={row.currentUnitPrice} precision="salesUnitPrice" />
              </td>
              <td>
                <ZHMoneyValue value={row.fields.unitPrice} precision="salesUnitPrice" />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
