import { Badge } from "../../../components/PageShell";
import type { ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import type { useI18n } from "../../../i18n/i18n";
import type { CashSessionCollectionDetailDto } from "../api/cajaService";

export function collectionDetailColumns(
  t: ReturnType<typeof useI18n>["t"],
): ZHDataTableColumn<CashSessionCollectionDetailDto>[] {
  return [
    { key: "authorizedAt", header: t("caja.movements.table.date"), render: (d) => formatDateTime(d.authorizedAt) },
    { key: "invoiceNumber", header: t("caja.session.invoice"), render: (d) => d.invoiceNumber },
    { key: "customer", header: t("caja.session.customer"), render: (d) => d.customerName },
    {
      key: "invoiceTotal",
      header: t("caja.session.invoiceTotal"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (d) => <ZHMoneyValue value={d.invoiceTotal} precision="money" />,
    },
    {
      key: "amount",
      header: t("caja.session.methodAmount"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (d) => <ZHMoneyValue value={d.amount} precision="money" />,
    },
    {
      key: "mixed",
      header: t("caja.session.paymentScope"),
      align: "center",
      render: (d) => (
        <Badge
          variant={d.isMixedPayment ? "warning" : "neutral"}
          label={d.isMixedPayment ? t("caja.session.mixed") : t("caja.session.full")}
        />
      ),
    },
    { key: "reference", header: t("caja.session.referenceReceipt"), render: (d) => d.reference ?? "—" },
    {
      key: "destination",
      header: t("caja.session.destination"),
      render: (d) => {
        if (!d.destinationBankName) return "—";
        return (
          <>
            {d.destinationBankName}
            {d.destinationAccountMasked ? ` — ${d.destinationAccountMasked}` : ""}
            {d.transferDate ? (
              <span className="cj-collection-transfer-date"> ({formatDate(d.transferDate)})</span>
            ) : null}
          </>
        );
      },
    },
  ];
}
