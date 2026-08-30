import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
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
export function SalesReturnSummary({ salesReturn, decimals }: Readonly<Props>) {
  const lineColumns: ZHDataTableColumn<SalesReturnDto["lines"][number]>[] = [
    {
      key: "product",
      header: "Producto",
      render: (line) => (
        <>
          <div className="sr-lines-table__desc">{line.description}</div>
          {line.snapshotSku && <div className="sr-lines-table__sku zh-code-value">{line.snapshotSku}</div>}
        </>
      ),
    },
    { key: "quantity", header: "Cantidad", align: "right", cellClassName: "zh-table-cell--num", render: (line) => line.quantity },
    {
      key: "unitPrice",
      header: "P. unitario",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => <ZHMoneyValue value={line.unitPrice} decimals={decimals} currencySymbol="" />,
    },
    {
      key: "vat",
      header: "IVA",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => <ZHMoneyValue value={line.vatAmount} decimals={decimals} currencySymbol="" />,
    },
    {
      key: "ice",
      header: "ICE",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => <ZHMoneyValue value={line.iceAmount} decimals={decimals} currencySymbol="" />,
    },
    {
      key: "total",
      header: "Total línea",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => <ZHMoneyValue value={line.taxInclusiveTotal} decimals={decimals} currencySymbol="" />,
    },
  ];

  const refundColumns: ZHDataTableColumn<SalesReturnDto["refundAllocations"][number]>[] = [
    {
      key: "method",
      header: "Forma de reembolso",
      render: (a) => (a.method === "Cash" ? "Efectivo (Caja)" : "Crédito a Cuenta por Cobrar"),
    },
    {
      key: "amount",
      header: "Monto",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (a) => <ZHMoneyValue value={a.amount} decimals={decimals} currencySymbol="" />,
    },
  ];

  return (
    <>
      {salesReturn.status !== "Draft" && (
        <ZHCard title="Líneas devueltas">
          <ZHDataTable
            columns={lineColumns}
            rows={salesReturn.lines}
            rowKey={(line) => line.id}
            tableClassName="table--compact table--neutral sr-lines-table"
          />
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
          <ZHDataTable
            columns={refundColumns}
            rows={salesReturn.refundAllocations}
            rowKey={(a) => a.id}
            tableClassName="table--compact table--neutral"
          />
        </ZHCard>
      )}
    </>
  );
}
