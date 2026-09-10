import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import type { PurchaseLineDto } from "../api/purchaseService";
import { purchaseReturnPreview } from "../utils/purchaseReturnPreview";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import type {
  PurchaseReturnLineFormValues,
} from "../schemas/purchaseReturnSchema";
import type { ReturnableLineDto } from "../api/purchaseReturnService";
import "../../../styles/shared/erp-form-core.css";

interface Props {
  /** Líneas devolvibles de la factura origen, con el remanente ya calculado por el servidor. */
  returnableLines: ReturnableLineDto[];
  /** Líneas actualmente seleccionadas en el `useFieldArray` de react-hook-form (cantidad > 0). */
  selected: PurchaseReturnLineFormValues[];
  append: (line: PurchaseReturnLineFormValues) => void;
  remove: (index: number) => void;
  disabled?: boolean;
  invoiceLines?: PurchaseLineDto[];
}

/**
 * Tabla de líneas devolvibles de la factura de compra origen con el remanente
 * ya calculado por el servidor (`GET /purchases/invoices/{id}/returnable-lines`).
 * Solo permite capturar la cantidad a devolver por línea — la bodega
 * (`WarehouseId`) se muestra siempre de solo lectura, nunca como campo
 * seleccionable (§14.2, decisión de negocio cerrada, Fase 12). Reutiliza la
 * edición con react-hook-form `useFieldArray`. Extiende el editor existente con el
 * preview fiscal de la factura para NC, usando ZHDataTable, ZhDecimalInput y ZHMoneyValue.
 */
export function PurchaseReturnableLinesEditor({
  returnableLines,
  selected,
  append,
  remove,
  disabled,
  invoiceLines,
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

  const source = (line: ReturnableLineDto) => invoiceLines?.find((l) => l.id === line.invoiceDetailId);
  const quantity = (line: ReturnableLineDto) => selected.find((l) => l.originalInvoiceDetailId === line.invoiceDetailId)?.quantity ?? 0;
  const columns: ZHDataTableColumn<ReturnableLineDto>[] = [
    { key: "product", header: "Producto", render: (line) => line.description },
    { key: "bought", header: "Cantidad comprada", align: "right", render: (line) => line.originalQuantity },
    { key: "returned", header: "Ya devuelto", align: "right", render: (line) => line.returnedQuantity },
    { key: "available", header: "Disponible", align: "right", render: (line) => line.remainingQuantity },
    { key: "quantity", header: "Cantidad a devolver", render: (line) => {
      const qty = quantity(line);
      const exceeds = qty > line.remainingQuantity;
      return <>
        <ZhDecimalInput aria-label={`Cantidad a devolver: ${line.description}`} aria-invalid={exceeds}
          decimals={4} positiveOnly disabled={disabled || line.remainingQuantity <= 0}
          value={qty ? String(qty) : ""} onChange={(e) => handleQuantityChange(line, e.target.value)} />
        {exceeds && <div role="alert" className="sr-lines-table__error">Excede lo disponible ({line.remainingQuantity}).</div>}
      </>;
    } },
  ];
  if (invoiceLines) {
    columns.push({ key: "price", header: "Precio/costo", align: "right", render: (line) =>
      <ZHMoneyValue value={source(line)?.unitPrice ?? 0} currencySymbol="" /> });
    for (const [key, header] of [["base", "Base"], ["vat", "IVA"], ["ice", "ICE"], ["irbpnr", "IRBPNR"], ["total", "Total"]] as const) {
      columns.push({ key, header, align: "right", render: (line) =>
        <ZHMoneyValue value={purchaseReturnPreview(source(line), quantity(line))[key]} currencySymbol="" /> });
    }
  }
  columns.push({ key: "warehouse", header: "Bodega", render: (line) => source(line)?.snapshotWarehouseCode ?? line.warehouseId });
  return <ZHDataTable columns={columns} rows={returnableLines} rowKey={(line) => line.invoiceDetailId} />;
}
