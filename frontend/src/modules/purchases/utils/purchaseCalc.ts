import type {
  PurchaseLineInput,
  PurchaseItemContextDto,
  PurchaseLineDto,
} from "../api/purchaseService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
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

/**
 * PURCHASE-INVOICE-TOTALS-SUMMARY-PANEL-01 — agrupa las líneas editables del draft (nunca el
 * snapshot xml*) por tarifa de IVA (`vatCode`), para el panel de solo lectura "Totales". Reusa
 * `lineNet`/`lineDiscountAmt`/`calcLineTax` — misma fuente de verdad que `calcSummary`, sin
 * duplicar la resolución de porcentaje (contexto del ítem > catálogo SRI > 0, nunca inferido).
 */
export type VatBreakdownRow = {
  vatCode: string;
  vatPercent: number;
  taxableBase: number;
  discount: number;
  vat: number;
  ice: number;
  total: number;
};

export function calcVatBreakdown(
  lines: LineWithContext[],
  vatRates?: Record<string, number>,
  iceRates?: Record<string, number>,
): VatBreakdownRow[] {
  const rows = new Map<string, VatBreakdownRow>();
  for (const l of lines) {
    const vatCode = l.vatCode ?? "";
    const vatPercent = l.context?.vatPercent ?? vatRates?.[vatCode] ?? 0;
    const { vat, ice } = calcLineTax(l, vatRates, iceRates);
    const net = lineNet(l);
    const row = rows.get(vatCode) ?? {
      vatCode,
      vatPercent,
      taxableBase: 0,
      discount: 0,
      vat: 0,
      ice: 0,
      total: 0,
    };
    row.taxableBase += net;
    row.discount += lineDiscountAmt(l);
    row.vat += vat;
    row.ice += ice;
    row.total += net + vat + ice;
    rows.set(vatCode, row);
  }
  return Array.from(rows.values()).sort((a, b) => b.vatPercent - a.vatPercent);
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

/**
 * Forma normalizada que consume `simulateCostDistribution`, sea el origen una compra ya
 * persistida (`PurchaseLineDto`, `id` = GUID real de línea) o una compra nueva sin guardar aún
 * (`PurchaseLineFormValues`, `id` = `_key` local del formulario — ver `buildCostDistributionInputFromFormLines`).
 */
export type DistributeCostSourceLine = {
  id: string;
  description: string;
  quantity: number;
  quantityInBaseUom: number;
  unitPrice: number;
  discountAmount: number;
  landedUnitCost: number;
  totalLineCost: number;
};

export function buildCostDistributionInputFromPersistedLines(
  lines: PurchaseLineDto[],
): DistributeCostSourceLine[] {
  return lines.map((l) => ({
    id: l.id,
    description: l.description,
    quantity: l.quantity,
    quantityInBaseUom: l.quantityInBaseUom,
    unitPrice: l.unitPrice,
    discountAmount: l.discountAmount,
    landedUnitCost: l.landedUnitCost,
    totalLineCost: l.totalLineCost,
  }));
}

/**
 * PURCHASE-DISTRIBUTE-COST-BEFORE-SAVE-01 — equivalente a `buildCostDistributionInputFromPersistedLines`
 * para una compra NUEVA sin guardar: no hay `PurchaseInvoiceDetail.Id` real todavía, así que se usa
 * `_key` (identificador estable del formulario) como `id`. `discountAmount`/`landedUnitCost`/
 * `totalLineCost` no existen como campos del formulario — se derivan aquí con la misma fórmula que
 * el backend (`RecalcDiscount`/`RecalcCosts` en `PurchaseInvoiceDetail`), incluyendo cualquier
 * `freightAllocated`/`otherCostsAllocated` ya aplicado en una vuelta previa del modal en esta misma
 * sesión (aditivo, nunca se pierde entre aplicaciones sucesivas antes de guardar).
 */
export function buildCostDistributionInputFromFormLines(
  lines: PurchaseLineFormValues[],
): DistributeCostSourceLine[] {
  return lines.map((l) => {
    const quantity = l.quantity ?? 0;
    const unitPrice = l.unitPrice ?? 0;
    const discountPct = l.discountPct ?? 0;
    const discountAmount = round2((quantity * unitPrice * discountPct) / 100);
    const quantityInBaseUom =
      l.quantityInBaseUom && l.quantityInBaseUom > 0
        ? l.quantityInBaseUom
        : quantity;
    const freightAllocated = l.freightAllocated ?? 0;
    const otherCostsAllocated = l.otherCostsAllocated ?? 0;
    const taxableBase = quantity * unitPrice - discountAmount;
    const totalLineCost = round2(
      taxableBase + freightAllocated + otherCostsAllocated,
    );
    const landedUnitCost =
      quantityInBaseUom > 0 ? round2(totalLineCost / quantityInBaseUom) : 0;
    return {
      id: String(l._key),
      description: l.description,
      quantity,
      quantityInBaseUom,
      unitPrice,
      discountAmount,
      landedUnitCost,
      totalLineCost,
    };
  });
}

export function simulateCostDistribution(
  lines: DistributeCostSourceLine[],
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
