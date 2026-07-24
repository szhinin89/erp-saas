import type { PurchaseLineInput, PurchaseItemContextDto } from '../api/purchaseService';
import { getDecimalConfig } from '../../../lib/config/decimal.config';
import { toLocalIsoDate } from '../../../lib/formatters/dateFormatters';

type LineWithContext = PurchaseLineInput & { context?: PurchaseItemContextDto };

export function lineGross(l: PurchaseLineInput): number {
  return l.quantity * l.unitPrice;
}

export function lineDiscountAmt(l: PurchaseLineInput): number {
  return lineGross(l) * ((l.discountPct ?? 0) / 100);
}

export function lineNet(l: PurchaseLineInput): number {
  return lineGross(l) - lineDiscountAmt(l);
}

export function calcLineTax(
  l: LineWithContext,
  vatRates?: Record<string, number>,
  iceRates?: Record<string, number>,
): { vat: number; ice: number } {
  const net = lineNet(l);
  // Fuente única de verdad: el contexto ya resuelto del ítem (vatPercent/icePercent),
  // y si aún no cargó, el catálogo SRI por código — nunca se infiere el porcentaje.
  const vatPct = l.context?.vatPercent ?? vatRates?.[l.vatCode] ?? 0;
  const icePct = l.context?.icePercent ?? (l.iceCode ? iceRates?.[l.iceCode] : undefined) ?? 0;
  return { vat: net * vatPct / 100, ice: net * icePct / 100 };
}

export function calcSummary(
  lines: LineWithContext[],
  freightCost: number,
  otherCosts: number,
  vatRates?: Record<string, number>,
  iceRates?: Record<string, number>,
) {
  const subtotal = lines.reduce((s, l) => s + lineGross(l), 0);
  const discount = lines.reduce((s, l) => s + lineDiscountAmt(l), 0);
  const netSubtotal = subtotal - discount;
  const vat = lines.reduce((s, l) => s + calcLineTax(l, vatRates, iceRates).vat, 0);
  const ice = lines.reduce((s, l) => s + calcLineTax(l, vatRates, iceRates).ice, 0);
  const total = netSubtotal + vat + ice + freightCost + otherCosts;
  return { subtotal, discount, netSubtotal, vat, ice, total };
}

type ScheduleRow = { number: number; dueDate: string; amount: number; notes: string };

export function roundToTotalAmount(value: number): number {
  const factor = 10 ** getDecimalConfig().totalAmount;
  return Math.round(value * factor) / factor;
}

export function generateScheduleRows(
  count: number, daysBetween: number, total: number, date: string,
): ScheduleRow[] {
  if (count < 1 || !date) return [];
  const factor = 10 ** getDecimalConfig().totalAmount;
  const amt = total > 0 ? Math.round((total * factor) / count) / factor : 0;
  let accumulated = 0;
  const rows: ScheduleRow[] = [];
  const d = new Date(date + 'T00:00:00');
  for (let i = 1; i <= count; i++) {
    const due = new Date(d);
    due.setDate(due.getDate() + daysBetween * i);
    const a = i === count && total > 0 ? Math.round((total - accumulated) * factor) / factor : amt;
    rows.push({ number: i, dueDate: toLocalIsoDate(due), amount: a, notes: '' });
    accumulated += a;
  }
  return rows;
}
