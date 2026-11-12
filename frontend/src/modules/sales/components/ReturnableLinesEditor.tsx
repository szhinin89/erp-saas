import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import type { ReturnableLineDto } from "../api/salesReturnService";
import "../../../styles/shared/erp-form-core.css";

interface Props {
  lines: ReturnableLineDto[];
  /** Cantidad ingresada por línea (string crudo del input), keyed por invoiceDetailId. 0/"" = línea no incluida. */
  quantities: Record<string, string>;
  onChangeQuantity: (invoiceDetailId: string, value: string) => void;
  disabled?: boolean;
}

/**
 * Tabla de líneas devolvibles de la factura origen con el remanente ya
 * calculado por el servidor (`GET /sales/invoices/{id}/returnable-lines`).
 * Solo permite capturar la cantidad a devolver por línea — el precio,
 * descuento e impuestos se muestran tal cual vienen de la factura, nunca se
 * recalculan aquí.
 */
export function ReturnableLinesEditor({
  lines,
  quantities,
  onChangeQuantity,
  disabled,
}: Props) {

  if (lines.length === 0) {
    return <p className="zh-inline-empty">Esta factura no tiene líneas devolvibles.</p>;
  }

  const columns: ZHDataTableColumn<ReturnableLineDto>[] = [
    {
      key: "product",
      header: "Producto",
      render: (line) => (
        <>
          <div className="sr-lines-table__desc">{line.description}</div>
          {line.snapshotSku && (
            <div className="sr-lines-table__sku zh-code-value">{line.snapshotSku}</div>
          )}
        </>
      ),
    },
    {
      key: "originalQuantity",
      header: "Cant. original",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => (
        <>
          {line.originalQuantity} {line.uomCode}
          {line.conversionFactor !== 1 && (
            <div className="sr-lines-table__equivalence">
              = {line.originalQuantityInBaseUom} {line.baseUomCode}
            </div>
          )}
        </>
      ),
    },
    {
      key: "returnedQuantity",
      header: "Ya devuelto",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => (
        <>
          {line.returnedQuantity} {line.uomCode}
        </>
      ),
    },
    {
      key: "remainingQuantity",
      header: "Remanente",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => (
        <>
          {line.remainingQuantity} {line.uomCode}
          {line.conversionFactor !== 1 && (
            <div className="sr-lines-table__equivalence">
              = {line.remainingQuantityInBaseUom} {line.baseUomCode}
            </div>
          )}
        </>
      ),
    },
    {
      key: "unitPrice",
      header: "P. unitario",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => (
        <ZHMoneyValue value={line.unitPrice} precision="salesUnitPrice" currencySymbol="" />
      ),
    },
    {
      key: "quantityToReturn",
      header: "Cant. a devolver",
      align: "right",
      render: (line) => {
        const qty = quantities[line.invoiceDetailId] ?? "";
        const exceeds = Number(qty) > line.remainingQuantity;
        return (
          <>
            <ZhDecimalInput
              aria-label={`Cantidad a devolver: ${line.description}`}
              aria-invalid={exceeds}
              precision="quantity"
              positiveOnly
              disabled={disabled || line.remainingQuantity <= 0}
              value={qty}
              onChange={(e) => onChangeQuantity(line.invoiceDetailId, e.target.value)}
              className={exceeds ? "sr-qty-input--error" : undefined}
            />
            {exceeds && (
              <div className="zh-field-hint zh-field-hint--error">
                Excede el remanente ({line.remainingQuantity}).
              </div>
            )}
          </>
        );
      },
    },
  ];

  return (
    <ZHDataTable
      columns={columns}
      rows={lines}
      rowKey={(line) => line.invoiceDetailId}
      tableClassName="table--compact table--neutral sr-lines-table"
    />
  );
}
