import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { useI18n } from "../../../i18n/i18n";

export interface ExpenseDocumentTotalsValue {
  subtotal: number;
  totalDiscount: number;
  totalTax: number;
  grandTotal: number;
}

export function ExpenseDocumentTotals({
  totals,
}: {
  totals: ExpenseDocumentTotalsValue;
}) {
  const { t } = useI18n();
  return (
    <section className="exp-doc-totals" aria-label={t("expenses.form.totals.aria")}>
      <div className="exp-doc-total-row">
        <span>{t("expenses.form.totals.subtotal")}</span>
        <ZHMoneyValue value={totals.subtotal} precision="money" />
      </div>
      <div className="exp-doc-total-row">
        <span>{t("expenses.form.totals.discount")}</span>
        <ZHMoneyValue
          value={totals.totalDiscount}
          precision="money"
          emphasis="muted"
        />
      </div>
      <div className="exp-doc-total-row">
        <span>{t("expenses.form.totals.vat")}</span>
        <ZHMoneyValue value={totals.totalTax} precision="tax" />
      </div>
      <div className="exp-doc-total-row exp-doc-total-row--grand">
        <span>{t("common.total")}</span>
        <ZHMoneyValue
          value={totals.grandTotal}
          precision="money"
          emphasis="grand"
        />
      </div>
    </section>
  );
}
