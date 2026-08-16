import type { SalesLineInput } from "../api/salesService";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { normalizeOptionalCode } from "../../../lib/sanitizers";

export function lineGross(l: SalesLineInput): number {
  return l.quantity * l.unitPrice;
}

export function lineDiscountAmt(l: SalesLineInput): number {
  return lineGross(l) * ((l.discountPct ?? 0) / 100);
}

export function lineNet(l: SalesLineInput): number {
  return lineGross(l) - lineDiscountAmt(l);
}

export function calcLineTax(
  l: SalesLineInput,
  vatRates?: Record<string, number>,
  iceRates?: Record<string, number>,
): { vat: number; ice: number } {
  const net = lineNet(l);
  const iceRate = (l.iceCode ? iceRates?.[l.iceCode] : undefined) ?? 0;
  const ice = iceRate > 0 ? (net * iceRate) / 100 : 0;
  // La base imponible del IVA incluye el ICE cuando aplica (normativa SRI Ecuador)
  const taxableBase = net + ice;
  const vatRate = vatRates?.[l.vatCode] ?? 0;
  return { vat: (taxableBase * vatRate) / 100, ice };
}

/** Advertencia preventiva de stock (UX únicamente): solo se activa cuando el frontend ya tiene
 * el dato de disponibilidad (`_stockQty`, snapshot tomado al agregar el ítem o cambiar de
 * bodega) — nunca infiere ni bloquea si ese dato no llegó a cargarse; en ese caso la única
 * fuente de verdad sigue siendo el backend (`AuthorizeSalesUseCases`), cuyo error se muestra
 * tal cual llega. No duplica la regla de stock, solo anticipa el mismo resultado en pantalla. */
export function lineExceedsStock(line: {
  _tracksStock?: boolean;
  _stockQty?: number;
  quantity: number;
}): boolean {
  return (
    !!line._tracksStock &&
    line._stockQty != null &&
    line.quantity > line._stockQty
  );
}

export type MergeCandidateLine = {
  itemId?: string | null;
  unitPrice: number;
  vatCode: string;
  iceCode?: string | null;
  warehouseId?: string | null;
};

/**
 * Único punto de la condición conservadora de fusión "reescanear el mismo producto suma
 * cantidad en vez de crear otra línea": el ítem, precio, impuestos y bodega deben coincidir
 * exactamente, y la línea existente no debe tener ya un descuento manual aplicado. Cualquier
 * diferencia (p. ej. el cajero ya negoció precio/descuento distinto) no fusiona — crea una línea
 * separada para no perder esa intención. Devuelve -1 si no hay ninguna línea fusionable.
 */
export function findMergeableLineIndex<T extends MergeCandidateLine & { discountPct?: number | null }>(
  lines: T[],
  candidate: MergeCandidateLine,
): number {
  return lines.findIndex(
    (l) =>
      l.itemId === candidate.itemId &&
      l.unitPrice === candidate.unitPrice &&
      (l.discountPct ?? 0) === 0 &&
      l.vatCode === candidate.vatCode &&
      normalizeOptionalCode(l.iceCode ?? null) ===
        normalizeOptionalCode(candidate.iceCode ?? null) &&
      (l.warehouseId ?? null) === (candidate.warehouseId ?? null),
  );
}

export type TaxBreakdownEntry = {
  label: string;
  rate: number;
  base: number;
  tax: number;
};

/** Único punto de formato de la etiqueta de IVA por tasa ("IVA 0%" / "IVA 15%") — usado por el
 * resumen de impuestos, el detalle de factura en modo solo-lectura y el badge por línea, para
 * no repetir la misma regla de formato en tres lugares. */
export function formatVatLabel(rate: number): string {
  return rate === 0 ? "IVA 0%" : `IVA ${rate}%`;
}

export function calcSummary(
  lines: SalesLineInput[],
  vatRates?: Record<string, number>,
  iceRates?: Record<string, number>,
) {
  const subtotal = lines.reduce((s, l) => s + lineGross(l), 0);
  const discount = lines.reduce((s, l) => s + lineDiscountAmt(l), 0);
  const netSubtotal = subtotal - discount;

  const byRate = new Map<number, { base: number; tax: number }>();
  let totalIce = 0;

  for (const l of lines) {
    const net = lineNet(l);
    const iceRate = (l.iceCode ? iceRates?.[l.iceCode] : undefined) ?? 0;
    const ice = iceRate > 0 ? (net * iceRate) / 100 : 0;
    totalIce += ice;
    const taxableBase = net + ice;
    const vatRate = vatRates?.[l.vatCode] ?? 0;
    const tax = (taxableBase * vatRate) / 100;
    const entry = byRate.get(vatRate) ?? { base: 0, tax: 0 };
    entry.base += taxableBase;
    entry.tax += tax;
    byRate.set(vatRate, entry);
  }

  const totalAmountDecimals = getDecimalConfig().totalAmount;
  const roundTotal = (v: number) => {
    const factor = 10 ** totalAmountDecimals;
    return Math.round(v * factor) / factor;
  };

  const taxBreakdown: TaxBreakdownEntry[] = Array.from(byRate.entries())
    .sort((a, b) => a[0] - b[0])
    .map(([rate, v]) => ({
      label: formatVatLabel(rate),
      rate,
      base: roundTotal(v.base),
      tax: roundTotal(v.tax),
    }));

  const vat = taxBreakdown.reduce((s, e) => s + e.tax, 0);
  const ice = roundTotal(totalIce);
  const total = roundTotal(netSubtotal + vat + ice);

  return {
    subtotal: roundTotal(subtotal),
    discount: roundTotal(discount),
    netSubtotal: roundTotal(netSubtotal),
    vat,
    ice,
    total,
    taxBreakdown,
  };
}
