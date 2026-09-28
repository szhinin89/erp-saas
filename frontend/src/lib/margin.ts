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

/**
 * Inversa exacta de {@link calcMarginPercent}: precio que deja `marginPct` de margen sobre PRECIO
 * para `cost`. Sin redondeo (lo decide el llamador con la precisión de su campo). `null` si el
 * margen no es alcanzable (>= 100 %) o el costo no es positivo.
 */
export function calcPriceForMargin(cost: number, marginPct: number): number | null {
  if (!Number.isFinite(cost) || !Number.isFinite(marginPct) || !(cost > 0) || !(marginPct < 100)) return null;
  const price = cost / (1 - marginPct / 100);
  return Number.isFinite(price) ? price : null;
}
