import type {
  PurchaseLineInput,
  PurchaseItemContextDto,
  PurchaseLineDto,
} from "../api/purchaseService";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { toLocalIsoDate } from "../../../lib/formatters/dateFormatters";

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
  const icePct =
    l.context?.icePercent ??
    (l.iceCode ? iceRates?.[l.iceCode] : undefined) ??
    0;
  return { vat: (net * vatPct) / 100, ice: (net * icePct) / 100 };
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
  const vat = lines.reduce(
    (s, l) => s + calcLineTax(l, vatRates, iceRates).vat,
    0,
  );
  const ice = lines.reduce(
    (s, l) => s + calcLineTax(l, vatRates, iceRates).ice,
    0,
  );
  const total = netSubtotal + vat + ice + freightCost + otherCosts;
  return { subtotal, discount, netSubtotal, vat, ice, total };
}

type ScheduleRow = {
  number: number;
  dueDate: string;
  amount: number;
  notes: string;
};

export function roundToTotalAmount(value: number): number {
  const factor = 10 ** getDecimalConfig().totalAmount;
  return Math.round(value * factor) / factor;
}

function round2(value: number): number {
  return Math.round(value * 100) / 100;
}

/**
 * PURCHASE-FREIGHT-DISTRIBUTION-MODAL-01 — previsualización del modal "Distribuir flete/gasto".
 * Espeja EXACTAMENTE el algoritmo de PurchaseInvoice.DistributeAdditionalCost (backend): prorrateo
 * proporcional por base imponible (subtotal - descuento) entre las líneas incluidas, redondeo a
 * 2 decimales, la última línea incluida absorbe el residuo. Es solo una simulación local — el
 * valor real se aplica y persiste vía purchaseService.distributeCost (misma fórmula server-side).
 */
export type DistributeCostPreviewLine = {
  lineId: string;
  description: string;
  quantity: number;
  quantityInBaseUom: number;
  currentUnitCost: number;
  subtotalBase: number;
  included: boolean;
  participationPct: number;
  allocatedAmount: number;
  allocatedPerUnit: number;
  newUnitCost: number;
  newLineTotal: number;
};

export function simulateCostDistribution(
  lines: Pick<
    PurchaseLineDto,
    | "id"
    | "description"
    | "quantity"
    | "quantityInBaseUom"
    | "unitPrice"
    | "discountAmount"
    | "landedUnitCost"
    | "totalLineCost"
  >[],
  includedIds: Set<string>,
  amountToDistribute: number,
): DistributeCostPreviewLine[] {
  const included = lines.filter((l) => includedIds.has(l.id));
  const baseTotal = included.reduce(
    (s, l) => s + (l.quantity * l.unitPrice - l.discountAmount),
    0,
  );
  const canDistribute = amountToDistribute > 0 && baseTotal > 0;
  let allocated = 0;

  return lines.map((l) => {
    const subtotalBase = l.quantity * l.unitPrice - l.discountAmount;
    const isIncluded = includedIds.has(l.id);

    if (!isIncluded || !canDistribute) {
      return {
        lineId: l.id,
        description: l.description,
        quantity: l.quantity,
        quantityInBaseUom: l.quantityInBaseUom,
        currentUnitCost: l.landedUnitCost,
        subtotalBase,
        included: isIncluded,
        participationPct: 0,
        allocatedAmount: 0,
        allocatedPerUnit: 0,
        newUnitCost: l.landedUnitCost,
        newLineTotal: l.totalLineCost,
      };
    }

    const isLastIncluded = included.at(-1)?.id === l.id;
    const share = isLastIncluded
      ? round2(amountToDistribute - allocated)
      : round2((amountToDistribute * subtotalBase) / baseTotal);
    if (!isLastIncluded) allocated += share;

    const perUnit = l.quantityInBaseUom > 0 ? share / l.quantityInBaseUom : 0;

    return {
      lineId: l.id,
      description: l.description,
      quantity: l.quantity,
      quantityInBaseUom: l.quantityInBaseUom,
      currentUnitCost: l.landedUnitCost,
      subtotalBase,
      included: true,
      participationPct: (subtotalBase / baseTotal) * 100,
      allocatedAmount: share,
      allocatedPerUnit: perUnit,
      newUnitCost: round2(l.landedUnitCost + perUnit),
      newLineTotal: round2(l.totalLineCost + share),
    };
  });
}

export function generateScheduleRows(
  count: number,
  daysBetween: number,
  total: number,
  date: string,
): ScheduleRow[] {
  if (count < 1 || !date) return [];
  const factor = 10 ** getDecimalConfig().totalAmount;
  const amt = total > 0 ? Math.round((total * factor) / count) / factor : 0;
  let accumulated = 0;
  const rows: ScheduleRow[] = [];
  const d = new Date(date + "T00:00:00");
  for (let i = 1; i <= count; i++) {
    const due = new Date(d);
    due.setDate(due.getDate() + daysBetween * i);
    const a =
      i === count && total > 0
        ? Math.round((total - accumulated) * factor) / factor
        : amt;
    rows.push({
      number: i,
      dueDate: toLocalIsoDate(due),
      amount: a,
      notes: "",
    });
    accumulated += a;
  }
  return rows;
}
