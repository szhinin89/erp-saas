import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { getDecimalConfig } from "../../../lib/config/decimal.config";

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
  const decimals = getDecimalConfig().totalAmount;

  return (
    <section className="exp-doc-totals" aria-label="Totales del gasto">
      <div className="exp-doc-total-row">
        <span>Subtotal</span>
        <ZHMoneyValue value={totals.subtotal} decimals={decimals} />
      </div>
      <div className="exp-doc-total-row">
        <span>Descuento</span>
        <ZHMoneyValue
          value={totals.totalDiscount}
          decimals={decimals}
          emphasis="muted"
        />
      </div>
      <div className="exp-doc-total-row">
        <span>IVA</span>
        <ZHMoneyValue value={totals.totalTax} decimals={decimals} />
      </div>
      <div className="exp-doc-total-row exp-doc-total-row--grand">
        <span>Total</span>
        <ZHMoneyValue
          value={totals.grandTotal}
          decimals={decimals}
          emphasis="grand"
        />
      </div>
    </section>
  );
}
