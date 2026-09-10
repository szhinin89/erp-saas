import type { PurchaseLineDto } from "../api/purchaseService";

/** Historical invoice preview; the server resolves and validates it again. */
export function purchaseReturnPreview(line: PurchaseLineDto | undefined, quantity: number) {
  const fraction = line && line.quantity > 0 ? quantity / line.quantity : 0;
  const prorate = (amount: number) => Math.round((fraction * amount + Number.EPSILON) * 100) / 100;
  const base = prorate(line ? line.quantity * line.unitPrice - line.discountAmount : 0);
  const vat = prorate(line?.vatAmount ?? 0);
  const ice = prorate(line?.iceAmount ?? 0);
  const irbpnr = prorate(line?.irbpnrAmount ?? 0);
  return { base, vat, ice, irbpnr, total: base + vat + ice + irbpnr };
}
