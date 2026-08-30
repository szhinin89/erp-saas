import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
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
 *
 * ZH-LISTING-GLOBAL-STANDARD-06: migrado a ZHDataTable — sin showRowNumber por ser líneas de un
 * documento (regla ya establecida en ZH-LISTING-MIGRATION-ALL-02).
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

  const columns: ZHDataTableColumn<PurchaseLineDto>[] = [
    {
      key: "product",
      header: t("purchases.creditNote.invoiceLines.product", "Producto"),
      render: (line) => (
        <>
          <div className="pcn-lines-table__desc">{line.description}</div>
          {line.snapshotSku && <div className="pcn-lines-table__meta">{line.snapshotSku}</div>}
        </>
      ),
    },
    {
      key: "quantity",
      header: t("purchases.creditNote.invoiceLines.quantity", "Cantidad"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => `${line.quantity} ${line.uomCode}`,
    },
    {
      key: "unitPrice",
      header: t("purchases.creditNote.invoiceLines.unitPrice", "Precio"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => <ZHMoneyValue value={line.unitPrice} currencySymbol="" align="end" />,
    },
    {
      key: "discount",
      header: t("purchases.creditNote.invoiceLines.discount", "Descuento"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => <ZHMoneyValue value={line.discountAmount} currencySymbol="" align="end" />,
    },
    {
      key: "taxableBase",
      header: t("purchases.creditNote.invoiceLines.taxableBase", "Base"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => <ZHMoneyValue value={line.taxableBase} currencySymbol="" align="end" />,
    },
    {
      key: "vat",
      header: t("purchases.creditNote.invoiceLines.vat", "IVA"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => <ZHMoneyValue value={line.vatAmount} currencySymbol="" align="end" />,
    },
    {
      key: "ice",
      header: t("purchases.creditNote.invoiceLines.ice", "ICE"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => <ZHMoneyValue value={line.iceAmount} currencySymbol="" align="end" />,
    },
    {
      key: "irbpnr",
      header: t("purchases.creditNote.invoiceLines.irbpnr", "IRBPNR"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => <ZHMoneyValue value={line.irbpnrAmount} currencySymbol="" align="end" />,
    },
    {
      key: "total",
      header: t("purchases.creditNote.invoiceLines.total", "Total"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (line) => <ZHMoneyValue value={line.taxInclusiveTotal} currencySymbol="" align="end" />,
    },
    {
      key: "warehouse",
      header: t("purchases.creditNote.invoiceLines.warehouse", "Bodega"),
      render: (line) => <span className="pcn-lines-table__meta">{line.snapshotWarehouseCode ?? "—"}</span>,
    },
  ];

  return (
    <ZHDataTable columns={columns} rows={lines} rowKey={(line) => line.id} tableClassName="pcn-lines-table" />
  );
}
