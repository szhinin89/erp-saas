/**
 * Fórmula única de margen bruto — misma que usa el Pricing Engine del backend
 * (`GetPurchaseItemContextQueryHandler`: `costMargin = pvp - averageCost`, `costMarginPct =
 * (costMargin / pvp) * 100` cuando `pvp > 0`) y que ya consumía la simulación de rentabilidad
 * de líneas de Compras (`modules/purchases/utils/purchaseLinePresentation.ts`). Margen sobre
 * PRECIO, nunca sobre costo — no crear una fórmula paralela en ningún otro lugar del frontend.
 */
export function calcMarginAmount(cost: number, price: number): number {
  return price - cost;
}

export function calcMarginPercent(cost: number, price: number): number {
  return price > 0 ? (calcMarginAmount(cost, price) / price) * 100 : 0;
}
