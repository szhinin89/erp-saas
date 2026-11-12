import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import type { RepricingRow } from "../hooks/useSalesCustomerRepricing";

/**
 * Tabla "Producto | Precio actual | Nuevo precio" del modal de cambio de cliente. Ambos precios
 * son UnitPrice: declaran `precision="salesUnitPrice"` (ZH-DESIGN-SYSTEM-PRECISION-04E) y el
 * Design System resuelve la escala de la PrecisionPolicy — nunca un formato fijo ni una prop.
 *
 * Usa `ZHDataTable` (`table--compact`); las columnas monetarias se alinean a la derecha con las
 * utilidades oficiales (`align="right"` + `zh-table-cell--num`). El modal solo se abre con al
 * menos una fila (`priceChangedRows.length > 0`), por lo que el estado vacío no es alcanzable.
 */
const columns: ZHDataTableColumn<RepricingRow>[] = [
  { key: "description", header: "Producto", render: (row) => row.description },
  {
    key: "currentUnitPrice",
    header: "Precio actual",
    align: "right",
    cellClassName: "zh-table-cell--num",
    render: (row) => <ZHMoneyValue value={row.currentUnitPrice} precision="salesUnitPrice" />,
  },
  {
    key: "newUnitPrice",
    header: "Nuevo precio",
    align: "right",
    cellClassName: "zh-table-cell--num",
    render: (row) => <ZHMoneyValue value={row.fields.unitPrice} precision="salesUnitPrice" />,
  },
];

export function SalesRepricingTable({ rows }: { rows: RepricingRow[] }) {
  return (
    <ZHDataTable
      columns={columns}
      rows={rows}
      rowKey={(row) => String(row.key)}
      tableClassName="table--compact"
    />
  );
}
