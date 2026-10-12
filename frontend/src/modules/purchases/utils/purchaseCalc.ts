import Decimal from "decimal.js";
import { roundToDecimals } from "../../../lib/sanitizers";
import type {
  PurchaseLineInput,
  PurchaseItemContextDto,
  PurchaseLineDto,
} from "../api/purchaseService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import { getPrecisionPolicy } from "../../../lib/config/precisionPolicy.config";
import { addDaysIso } from "../../../lib/formatters/dateFormatters";

type LineWithContext = PurchaseLineInput & {
  context?: PurchaseItemContextDto;
  // Montos documentales (XML) de la línea — solo en líneas de Recepción Electrónica.
  xmlQuantity?: number;
  xmlUnitPrice?: number;
  xmlDiscountPct?: number;
  xmlTaxableBase?: number;
  xmlTaxValue?: number;
  xmlIceAmount?: number;
  xmlIrbpnrAmount?: number;
};

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

const FiscalDecimal = Decimal.clone({ precision: 64 });

function fiscalRound(value: Decimal, decimals: number): Decimal {
  return value.toDecimalPlaces(decimals, Decimal.ROUND_HALF_UP);
}

/**
 * PURCHASES-HARDENING-P0 (B4-B6) — misma condición que el backend
 * (`PurchaseLineNormalizer.MatchesXmlDocument`): la línea conserva cantidad, precio y % descuento
 * de la línea de recepción. Solo entonces los montos IVA/ICE del XML siguen siendo válidos.
 */
export function matchesXmlDocument(l: LineWithContext): boolean {
  return (
    l.xmlQuantity != null &&
    l.xmlUnitPrice != null &&
    l.quantity === l.xmlQuantity &&
    l.unitPrice === l.xmlUnitPrice &&
    (l.discountPct ?? 0) === (l.xmlDiscountPct ?? 0)
  );
}

/**
 * PURCHASES-HARDENING-P0 (B4-B6) — impuestos de la línea tal como los persiste el backend
 * (`PurchaseInvoiceDetail.ApplyTaxes` → `RecalcTaxes` → `SriTaxCalculator.Compute`):
 * - línea XML sin editar: IVA/ICE = montos documentales del XML;
 * - si no: base = round2(subtotal − round6(descuento)), ICE porcentual = round2(base × tarifa),
 *   IVA = round2((base + ICE) × tarifa) — el IVA se calcula sobre base + ICE (regla SRI);
 * - ICE específico (sin tarifa porcentual) e IRBPNR: monto fijo del XML, como en backend.
 * Tarifa por código de la línea (catálogo SRI, igual que el backend); el contexto del ítem es solo
 * respaldo mientras el catálogo no carga.
 */
export function resolveLineTaxes(
  l: LineWithContext,
  vatRates?: Record<string, number>,
  iceRates?: Record<string, number>,
): { vat: number; ice: number; irbpnr: number } {
  const irbpnr = l.xmlIrbpnrAmount ?? 0;
  const documentMatches = matchesXmlDocument(l);
  const gross = new FiscalDecimal(l.quantity ?? 0).times(l.unitPrice ?? 0);
  const discountPct = l.discountPct ?? 0;
  const discount = fiscalRound(
    discountPct > 0 ? gross.times(discountPct).div(100) : new FiscalDecimal(0),
    6,
  );
  const base = fiscalRound(gross.minus(discount), 2);

  const icePct =
    (l.iceCode ? iceRates?.[l.iceCode] : undefined) ?? l.context?.icePercent ?? 0;
  const ice =
    documentMatches && l.xmlIceAmount != null
      ? new FiscalDecimal(l.xmlIceAmount)
      : icePct > 0
        ? fiscalRound(base.times(icePct).div(100), 2)
        : new FiscalDecimal(l.xmlIceAmount ?? 0);

  const vatPct = vatRates?.[l.vatCode] ?? l.context?.vatPercent ?? 0;
  const vat =
    documentMatches && l.xmlTaxValue != null
      ? l.xmlTaxValue
      : vatPct > 0
        ? fiscalRound(base.plus(ice).times(vatPct).div(100), 2).toNumber()
        : 0;

  return { vat, ice: ice.toNumber(), irbpnr };
}

/**
 * PURCHASES-HARDENING-P0 (B4-B6) — montos documentales (Source=Xml) de una línea ya persistida, para
 * que un borrador reabierto siga resolviendo IVA/ICE/IRBPNR igual que el backend. Sin impuestos XML
 * (línea manual) no devuelve nada: la línea se calcula por tarifa como siempre.
 */
export function documentalTaxSnapshot(
  line: Pick<PurchaseLineDto, "taxes" | "quantity" | "unitPrice" | "discountPct">,
): Pick<
  LineWithContext,
  | "xmlQuantity"
  | "xmlUnitPrice"
  | "xmlDiscountPct"
  | "xmlTaxableBase"
  | "xmlTaxValue"
  | "xmlIceAmount"
  | "xmlIrbpnrAmount"
> {
  const xml = (line.taxes ?? []).filter((tx) => tx.source === "Xml");
  if (xml.length === 0) return {};
  const vat = xml.find((tx) => tx.taxCode === "2");
  // IVA con Source=Xml solo persiste si la línea seguía igual al comprobante al guardarse
  // (backend MatchesXmlDocument): sus valores persistidos SON los del XML.
  const documentValues = vat
    ? { xmlQuantity: line.quantity, xmlUnitPrice: line.unitPrice, xmlDiscountPct: line.discountPct }
    : {};
  const ice = xml.filter((tx) => tx.taxCode === "3");
  const irbpnr = xml.filter((tx) => tx.taxCode === "5");
  const sum = (rows: typeof xml) =>
    rows.length > 0 ? rows.reduce((s, tx) => s + tx.taxAmount, 0) : undefined;
  return {
    ...documentValues,
    xmlTaxableBase: vat?.taxableBase,
    xmlTaxValue: vat?.taxAmount,
    xmlIceAmount: sum(ice),
    xmlIrbpnrAmount: sum(irbpnr),
  };
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
  const taxes = lines.map((l) => resolveLineTaxes(l, vatRates, iceRates));
  const vat = taxes.reduce((s, tx) => s + tx.vat, 0);
  const ice = taxes.reduce((s, tx) => s + tx.ice, 0);
  // IRBPNR forma parte del GrandTotal en backend (TaxInclusiveTotal, FLOW-READY-02F.2).
  const irbpnr = taxes.reduce((s, tx) => s + tx.irbpnr, 0);
  const total = netSubtotal + vat + ice + irbpnr + freightCost + otherCosts;
  return { subtotal, discount, netSubtotal, vat, ice, irbpnr, total };
}

/**
 * PURCHASE-INVOICE-TOTALS-SUMMARY-PANEL-01 — agrupa las líneas editables del draft por tarifa de
 * IVA (`vatCode`), para el panel de solo lectura "Totales". Reusa `lineNet`/`lineDiscountAmt`/
 * `resolveLineTaxes` — misma fuente de verdad que `calcSummary` (monto documental XML mientras la
 * base no cambie; si no, contexto del ítem > catálogo SRI > 0, nunca inferido).
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
    const { vat, ice } = resolveLineTaxes(l, vatRates, iceRates);
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
  const factor = 10 ** getPrecisionPolicy().moneyDecimals;
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
      quantityInBaseUom > 0 ? roundToDecimals(totalLineCost / quantityInBaseUom, getPrecisionPolicy().unitCostDecimals) : 0;
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
      newUnitCost: roundToDecimals(l.landedUnitCost + perUnit, getPrecisionPolicy().unitCostDecimals),
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
  const factor = 10 ** getPrecisionPolicy().moneyDecimals;
  const amt = total > 0 ? Math.round((total * factor) / count) / factor : 0;
  let accumulated = 0;
  const rows: ScheduleRow[] = [];
  for (let i = 1; i <= count; i++) {
    // Fecha de negocio: aritmética de calendario pura, sin Date/zona del navegador.
    const due = addDaysIso(date, daysBetween * i);
    const a =
      i === count && total > 0
        ? Math.round((total - accumulated) * factor) / factor
        : amt;
    rows.push({
      number: i,
      dueDate: due,
      amount: a,
      notes: "",
    });
    accumulated += a;
  }
  return rows;
}
