import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { formatMoney } from "../../../lib/sanitizers";
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
    return <p className="sr-lines-empty">Esta factura no tiene líneas devolvibles.</p>;
  }

  return (
    <div className="table-scroll">
      <table className="pf-table sr-lines-table">
        <thead>
          <tr>
            <th>Producto</th>
            <th className="zh-text-align-right">Cant. original</th>
            <th className="zh-text-align-right">Ya devuelto</th>
            <th className="zh-text-align-right">Remanente</th>
            <th className="zh-text-align-right">P. unitario</th>
            <th className="zh-text-align-right">Cant. a devolver</th>
          </tr>
        </thead>
        <tbody>
          {lines.map((line) => {
            const qty = quantities[line.invoiceDetailId] ?? "";
            const exceeds = Number(qty) > line.remainingQuantity;
            return (
              <tr key={line.invoiceDetailId}>
                <td>
                  <div className="sr-lines-table__desc">{line.description}</div>
                  {line.snapshotSku && (
                    <div className="sr-lines-table__sku">{line.snapshotSku}</div>
                  )}
                </td>
                <td className="zh-table-cell--num">{line.originalQuantity}</td>
                <td className="zh-table-cell--num">{line.returnedQuantity}</td>
                <td className="zh-table-cell--num">{line.remainingQuantity}</td>
                <td className="zh-table-cell--num">{formatMoney(line.unitPrice)}</td>
                <td className="zh-text-align-right">
                  <ZhDecimalInput
                    decimals={4}
                    positiveOnly
                    disabled={disabled || line.remainingQuantity <= 0}
                    value={qty}
                    onChange={(e) =>
                      onChangeQuantity(line.invoiceDetailId, e.target.value)
                    }
                    className={exceeds ? "sr-qty-input--error" : undefined}
                  />
                  {exceeds && (
                    <div className="sr-lines-table__error">
                      Excede el remanente ({line.remainingQuantity}).
                    </div>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
