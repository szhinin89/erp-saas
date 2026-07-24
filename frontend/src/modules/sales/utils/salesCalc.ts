import type { SalesLineInput } from '../api/salesService';
import { getDecimalConfig } from '../../../lib/config/decimal.config';

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
  const ice = iceRate > 0 ? net * iceRate / 100 : 0;
  // La base imponible del IVA incluye el ICE cuando aplica (normativa SRI Ecuador)
  const taxableBase = net + ice;
  const vatRate = vatRates?.[l.vatCode] ?? 0;
  return { vat: taxableBase * vatRate / 100, ice };
}

export type TaxBreakdownEntry = {
  label: string;
  rate: number;
  base: number;
  tax: number;
};

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
    const ice = iceRate > 0 ? net * iceRate / 100 : 0;
    totalIce += ice;
    const taxableBase = net + ice;
    const vatRate = vatRates?.[l.vatCode] ?? 0;
    const tax = taxableBase * vatRate / 100;
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
      label: rate === 0 ? 'IVA 0%' : `IVA ${rate}%`,
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
    vat, ice, total, taxBreakdown,
  };
}
