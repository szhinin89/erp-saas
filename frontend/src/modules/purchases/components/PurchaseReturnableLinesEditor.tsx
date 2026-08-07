import type { FieldArrayWithId, UseFieldArrayAppend, UseFieldArrayRemove } from "react-hook-form";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import type {
  PurchaseReturnDraftFormValues,
  PurchaseReturnLineFormValues,
} from "../schemas/purchaseReturnSchema";
import type { ReturnableLineDto } from "../api/purchaseReturnService";
import "../../../styles/shared/erp-form-core.css";

interface Props {
  /** Líneas devolvibles de la factura origen, con el remanente ya calculado por el servidor. */
  returnableLines: ReturnableLineDto[];
  /** Líneas actualmente seleccionadas en el `useFieldArray` de react-hook-form (cantidad > 0). */
  selected: FieldArrayWithId<PurchaseReturnDraftFormValues, "lines", "id">[];
  append: UseFieldArrayAppend<PurchaseReturnDraftFormValues, "lines">;
  remove: UseFieldArrayRemove;
  disabled?: boolean;
}

/**
 * Tabla de líneas devolvibles de la factura de compra origen con el remanente
 * ya calculado por el servidor (`GET /purchases/invoices/{id}/returnable-lines`).
 * Solo permite capturar la cantidad a devolver por línea — la bodega
 * (`WarehouseId`) se muestra siempre de solo lectura, nunca como campo
 * seleccionable (§14.2, decisión de negocio cerrada, Fase 12). Reutiliza la
 * estructura de `ReturnableLinesEditor.tsx` (SalesReturn) adaptada a
 * react-hook-form `useFieldArray` (F-V1 — RHF como motor, a diferencia del
 * `Record<string,string>` manual que usaba el componente de referencia).
 */
export function PurchaseReturnableLinesEditor({
  returnableLines,
  selected,
  append,
  remove,
  disabled,
}: Props) {
  if (returnableLines.length === 0) {
    return <p className="sr-lines-empty">Esta factura no tiene líneas devolvibles.</p>;
  }

  const indexOf = (invoiceDetailId: string) =>
    selected.findIndex((l) => l.originalInvoiceDetailId === invoiceDetailId);

  const handleQuantityChange = (line: ReturnableLineDto, raw: string) => {
    const qty = Number(raw);
    const idx = indexOf(line.invoiceDetailId);
    if (!raw || qty <= 0) {
      if (idx >= 0) remove(idx);
      return;
    }
    const entry: PurchaseReturnLineFormValues = {
      originalInvoiceDetailId: line.invoiceDetailId,
      quantity: qty,
    };
    if (idx >= 0) remove(idx);
    append(entry);
  };

  return (
    <div className="table-scroll">
      <table className="pf-table sr-lines-table">
        <thead>
          <tr>
            <th>Producto</th>
            <th className="zh-text-align-right">Cant. original</th>
            <th className="zh-text-align-right">Ya devuelto</th>
            <th className="zh-text-align-right">Remanente</th>
            <th>Bodega</th>
            <th className="zh-text-align-right">Cant. a devolver</th>
          </tr>
        </thead>
        <tbody>
          {returnableLines.map((line) => {
            const idx = indexOf(line.invoiceDetailId);
            const qty = idx >= 0 ? String(selected[idx].quantity) : "";
            const exceeds = Number(qty || 0) > line.remainingQuantity;
            return (
              <tr key={line.invoiceDetailId}>
                <td>
                  <div className="sr-lines-table__desc">{line.description}</div>
                </td>
                <td className="zh-table-cell--num">{line.originalQuantity}</td>
                <td className="zh-table-cell--num">{line.returnedQuantity}</td>
                <td className="zh-table-cell--num">{line.remainingQuantity}</td>
                <td className="sr-lines-table__desc">{line.warehouseId}</td>
                <td className="zh-text-align-right">
                  <ZhDecimalInput
                    decimals={4}
                    positiveOnly
                    disabled={disabled || line.remainingQuantity <= 0}
                    value={qty}
                    onChange={(e) => handleQuantityChange(line, e.target.value)}
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
