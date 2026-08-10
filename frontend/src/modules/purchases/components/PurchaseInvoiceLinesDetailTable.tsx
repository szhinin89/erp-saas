import { formatMoney } from "../../../lib/sanitizers";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseLineDto } from "../api/purchaseService";
import "../styles/purchase-credit-note.css";

interface Props {
  lines: PurchaseLineDto[];
}

/**
 * FLOW-READY-02C-R1.2 — "Detalle de compra afectada": líneas reales de la factura
 * (`invoice.lines`, ya cargadas por `purchaseService.getById`), siempre de solo lectura. Fuente
 * única para ambos tipos de NC — nunca líneas libres inventadas en frontend. Reutilizado tanto por
 * el paso Return (contexto, la edición de cantidades vive en `PurchaseReturnDraftFormSection`, sin
 * tocar) como por el paso Discount (contexto de bases/impuestos reales antes del editor por
 * resumen fiscal).
 */
export function PurchaseInvoiceLinesDetailTable({ lines }: Readonly<Props>) {
  const { t } = useI18n();

  if (lines.length === 0) {
    return (
      <p className="pcn-lines-empty">
        {t("purchases.creditNote.invoiceLines.empty", "Esta factura no tiene líneas.")}
      </p>
    );
  }

  return (
    <div className="table-scroll">
      <table className="pf-table pcn-lines-table">
        <thead>
          <tr>
            <th>{t("purchases.creditNote.invoiceLines.product", "Producto")}</th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.invoiceLines.quantity", "Cantidad")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.invoiceLines.unitPrice", "Precio")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.invoiceLines.discount", "Descuento")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.invoiceLines.taxableBase", "Base")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.invoiceLines.vat", "IVA")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.invoiceLines.ice", "ICE")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.invoiceLines.irbpnr", "IRBPNR")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.invoiceLines.total", "Total")}
            </th>
            <th>{t("purchases.creditNote.invoiceLines.warehouse", "Bodega")}</th>
          </tr>
        </thead>
        <tbody>
          {lines.map((line) => (
            <tr key={line.id}>
              <td>
                <div className="pcn-lines-table__desc">{line.description}</div>
                {line.snapshotSku && (
                  <div className="pcn-lines-table__meta">{line.snapshotSku}</div>
                )}
              </td>
              <td className="zh-table-cell--num">
                {line.quantity} {line.uomCode}
              </td>
              <td className="zh-table-cell--num">{formatMoney(line.unitPrice)}</td>
              <td className="zh-table-cell--num">{formatMoney(line.discountAmount)}</td>
              <td className="zh-table-cell--num">{formatMoney(line.taxableBase)}</td>
              <td className="zh-table-cell--num">{formatMoney(line.vatAmount)}</td>
              <td className="zh-table-cell--num">{formatMoney(line.iceAmount)}</td>
              <td className="zh-table-cell--num">{formatMoney(line.irbpnrAmount)}</td>
              <td className="zh-table-cell--num">{formatMoney(line.taxInclusiveTotal)}</td>
              <td className="pcn-lines-table__meta">
                {line.snapshotWarehouseCode ?? "—"}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
