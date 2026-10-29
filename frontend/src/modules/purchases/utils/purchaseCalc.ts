import Decimal from "decimal.js";
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
  xmlSource?: PurchaseLineFormValues["xmlSource"];
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
  return l.xmlAllocation?.subtotal ?? l.quantity * l.unitPrice;
}

export function lineDiscountAmt(l: PurchaseLineInput): number {
  return l.xmlAllocation?.discount ?? lineGross(l) * ((l.discountPct ?? 0) / 100);
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

/** Whether operational quantity/price/discount still match the XML, for source comparisons. */
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
 * Linked XML amounts remain fiscal evidence when operational values change. Confirmation
 * reconciles their header totals by category/rate. Manual lines use the normal tax calculator.
 */
export function resolveLineTaxes(
  l: LineWithContext,
  vatRates?: Record<string, number>,
  iceRates?: Record<string, number>,
): { vat: number; ice: number; irbpnr: number } {
  if (l.xmlAllocation) {
    const sum = (code: string) => l.xmlAllocation!.taxes.reduce((total, tax) =>
      total + (l.xmlSource?.taxes.find(t => t.purchaseReceptionLineTaxId === tax.purchaseReceptionLineTaxId)?.taxCode === code ? tax.taxAmount : 0), 0);
    return { vat: sum("2"), ice: sum("3"), irbpnr: sum("5") };
  }
  const irbpnr = l.xmlIrbpnrAmount ?? 0;
  const hasFiscalSnapshot = !!l.purchaseReceptionLineId;
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
    hasFiscalSnapshot && l.xmlIceAmount != null
      ? new FiscalDecimal(l.xmlIceAmount)
      : icePct > 0
        ? fiscalRound(base.times(icePct).div(100), 2)
        : new FiscalDecimal(l.xmlIceAmount ?? 0);

  const vatPct = vatRates?.[l.vatCode] ?? l.context?.vatPercent ?? 0;
  const vat =
    hasFiscalSnapshot && l.xmlTaxValue != null
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
  const total = netSubtotal + vat + ice + irbpnr;
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
