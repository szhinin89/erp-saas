import { useId, useState } from "react";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { useI18n } from "../../../i18n/i18n";

export interface ExpenseDocumentTotalsValue {
  subtotal: number;
  totalDiscount: number;
  totalTax: number;
  grandTotal: number;
}

/**
 * Resumen de importes del gasto. Solo presenta los totales que ya calcula la página (único estado
 * de totales): en escritorio muestra las cuatro filas; en móvil (≤900px, ver expense-documents.css)
 * deja siempre visible el Total y despliega el desglose con el botón "Ver desglose". El estado
 * abierto/cerrado es solo de presentación.
 */
export function ExpenseDocumentTotals({
  totals,
}: {
  totals: ExpenseDocumentTotalsValue;
}) {
  const { t } = useI18n();
  const [detailOpen, setDetailOpen] = useState(false);
  const detailId = useId();
  return (
    <section className="exp-doc-totals" aria-label={t("expenses.form.totals.aria")}>
      <div
        id={detailId}
        className={`exp-doc-totals__rows${detailOpen ? " exp-doc-totals__rows--open" : ""}`}
      >
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
      </div>
      <div className="exp-doc-total-row exp-doc-total-row--grand">
        <span>{t("common.total")}</span>
        <ZHMoneyValue
          value={totals.grandTotal}
          precision="money"
          emphasis="grand"
        />
        <ZHBtn
          type="button"
          variant="ghost"
          size="xs"
          className="exp-doc-totals__toggle"
          expanded={detailOpen}
          aria-controls={detailId}
          onClick={() => setDetailOpen((open) => !open)}
        >
          {detailOpen ? t("expenses.form.totals.hideDetail") : t("expenses.form.totals.showDetail")}
        </ZHBtn>
      </div>
    </section>
  );
}
