import { xmlEvidenceFromDraftLine } from "./purchaseXmlEvidence";
import { roundToDecimals } from "../../../lib/sanitizers";
import Decimal from "decimal.js";
import type { PurchaseLineDto } from "../api/purchaseService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";

type Line = PurchaseLineFormValues;
const sum = (values: number[]) => values.reduce((s, n) => s.plus(n), new Decimal(0));

export function restoreXmlAllocation(line: PurchaseLineDto): Partial<Line> {
  if (line.xmlAllocatedSubtotal == null) return {};
  return { xmlAllocation: { subtotal: line.xmlAllocatedSubtotal, discount: line.discountAmount,
    taxes: line.taxes.filter(t => t.source === "XmlAllocation").map(t => ({
      purchaseReceptionLineTaxId: t.purchaseReceptionLineTaxId ?? "",
      taxableBase: t.taxableBase, taxAmount: t.taxAmount,
    })) } };
}

export interface BreakdownBalanceRow {
  key: string;
  concept: "subtotal" | "discount" | "base" | "tax" | "total";
  taxCode?: string;
  taxRateCode?: string;
  expected: number;
  assigned: number;
  difference: number;
}

/** XML `lineSubtotal` is already net of discount; allocations are captured gross. */
export function grossXmlSubtotal(xml: NonNullable<Line["xmlSource"]>): number {
  return new Decimal(xml.lineSubtotal).plus(xml.discount).toNumber();
}

/** Only lines with every documentary tax identified can be split; already split rows stay editable. */
export function canBreakDown(line: Line): boolean {
  return !!line.purchaseReceptionLineId && !!line.xmlSource && line.xmlSource.taxes.length > 0
    && line.xmlSource.taxes.every(tx => !!tx.purchaseReceptionLineTaxId);
}

/** Starting allocation for a new component: every original tax present, all amounts explicit (0). */
export function emptyXmlAllocation(xml: NonNullable<Line["xmlSource"]>): NonNullable<Line["xmlAllocation"]> {
  return { subtotal: 0, discount: 0, taxes: xml.taxes.map(tx => ({
    purchaseReceptionLineTaxId: tx.purchaseReceptionLineTaxId ?? "", taxableBase: 0, taxAmount: 0,
  })) };
}

export function breakdownBalance(source: Line, components: Line[]) {
  const xml = source.xmlSource;
  const rows: BreakdownBalanceRow[] = [];
  const add = (row: Omit<BreakdownBalanceRow, "assigned" | "difference">, values: number[]) => {
    const assigned = sum(values);
    rows.push({ ...row, assigned: assigned.toNumber(),
      difference: new Decimal(row.expected).minus(assigned).toNumber() });
  };
  if (!xml) return { valid: false, rows };
  add({ key: "subtotal", concept: "subtotal", expected: grossXmlSubtotal(xml) }, components.map(l => l.xmlAllocation?.subtotal ?? 0));
  add({ key: "discount", concept: "discount", expected: xml.discount }, components.map(l => l.xmlAllocation?.discount ?? 0));
  for (const tax of xml.taxes) {
    const assigned = components.flatMap(l => l.xmlAllocation?.taxes.filter(t => t.purchaseReceptionLineTaxId === tax.purchaseReceptionLineTaxId) ?? []);
    const origin = { taxCode: tax.taxCode, taxRateCode: tax.taxRateCode };
    add({ key: `${tax.purchaseReceptionLineTaxId}:base`, concept: "base", expected: tax.taxableBase, ...origin }, assigned.map(t => t.taxableBase));
    add({ key: `${tax.purchaseReceptionLineTaxId}:tax`, concept: "tax", expected: tax.taxAmount, ...origin }, assigned.map(t => t.taxAmount));
  }
  add({ key: "total", concept: "total", expected: xml.totalLine }, components.map(l => sum([
    l.xmlAllocation?.subtotal ?? 0, -(l.xmlAllocation?.discount ?? 0),
    ...(l.xmlAllocation?.taxes.map(t => t.taxAmount) ?? []),
  ]).toNumber()));
  const valid = components.length >= 2 && xml.taxes.length > 0
    && xml.taxes.every(t => !!t.purchaseReceptionLineTaxId)
    && components.every(l => !!l.itemId && l.quantity > 0 && l.unitPrice >= 0
      && (l.conversionFactor ?? 1) > 0
      && l.purchaseReceptionLineId === source.purchaseReceptionLineId
      && !!l.xmlAllocation && l.xmlAllocation.subtotal >= l.xmlAllocation.discount && l.xmlAllocation.discount >= 0
      && l.xmlAllocation.taxes.length > 0
      && new Set(l.xmlAllocation.taxes.map(t => t.purchaseReceptionLineTaxId)).size === l.xmlAllocation.taxes.length
      && l.xmlAllocation.taxes.every(t => t.taxAmount >= 0 && t.taxableBase >= 0
        && xml.taxes.some(o => o.purchaseReceptionLineTaxId === t.purchaseReceptionLineTaxId)))
    && rows.every(r => r.difference === 0);
  return { valid, rows };
}

/** Replace the whole origin group at its existing position; never keep the promotion row. */
export function replaceBreakdownRows(lines: Line[], sourceId: string, components: Line[], nextKey: number,
  quantityDecimals: number): Line[] {
  const first = lines.findIndex(l => l.purchaseReceptionLineId === sourceId);
  if (first < 0) return lines;
  const source = lines[first];
  if (!breakdownBalance(source, components).valid) return lines;
  const cost = (field: "freightAllocated" | "otherCostsAllocated") =>
    sum(lines.filter(l => l.purchaseReceptionLineId === sourceId).map(l => l[field] ?? 0))
      .eq(sum(components.map(l => l[field] ?? 0)));
  if (!cost("freightAllocated") || !cost("otherCostsAllocated")) return lines;
  const replacement = components.map((c, i) => ({ ...xmlEvidenceFromDraftLine(source.xmlSource!), ...c, _key: nextKey + i,
    xmlSource: source.xmlSource, quantityInBaseUom: roundToDecimals(c.quantity * (c.conversionFactor ?? 1), quantityDecimals) }));
  return lines.flatMap((l, index) => {
    if (index === first) return replacement;
    return l.purchaseReceptionLineId === sourceId ? [] : [l];
  });
}

export function hasInvalidBreakdown(lines: Line[]): boolean {
  const ids = new Set(lines.filter(l => l.xmlAllocation).map(l => l.purchaseReceptionLineId));
  return [...ids].some(id => {
    const group = lines.filter(l => l.purchaseReceptionLineId === id);
    return !breakdownBalance(group[0], group).valid;
  });
}
