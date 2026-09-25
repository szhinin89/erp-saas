import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";

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

  return (
    <section className="exp-doc-totals" aria-label="Totales del gasto">
      <div className="exp-doc-total-row">
        <span>Subtotal</span>
        <ZHMoneyValue value={totals.subtotal} precision="money" />
      </div>
      <div className="exp-doc-total-row">
        <span>Descuento</span>
        <ZHMoneyValue
          value={totals.totalDiscount}
          precision="money"
          emphasis="muted"
        />
      </div>
      <div className="exp-doc-total-row">
        <span>IVA</span>
        <ZHMoneyValue value={totals.totalTax} precision="tax" />
      </div>
      <div className="exp-doc-total-row exp-doc-total-row--grand">
        <span>Total</span>
        <ZHMoneyValue
          value={totals.grandTotal}
          precision="money"
          emphasis="grand"
        />
      </div>
    </section>
  );
}
