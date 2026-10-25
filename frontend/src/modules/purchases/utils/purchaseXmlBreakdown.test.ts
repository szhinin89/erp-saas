import { describe, expect, it } from "vitest";
import type { PurchaseLineDto } from "../api/purchaseService";
import { lineGross, lineNet, resolveLineTaxes } from "./purchaseCalc";
import {
  breakdownBalance,
  canBreakDown,
  hasInvalidBreakdown,
  replaceBreakdownRows,
  restoreXmlAllocation,
} from "./purchaseXmlBreakdown";
import { balanced, manual, promo, xml } from "./purchaseXmlBreakdown.fixture";

describe("purchaseXmlBreakdown", () => {
  it("only offers the split when every documentary tax keeps its origin id", () => {
    expect(canBreakDown(promo())).toBe(true);
    expect(canBreakDown(promo({ xmlSource: { ...xml, taxes: [{ ...xml.taxes[0], purchaseReceptionLineTaxId: null }] } }))).toBe(false);
    expect(canBreakDown(manual)).toBe(false);
  });

  it("reconciles subtotal, discount and every tax base/amount exactly", () => {
    const result = breakdownBalance(promo(), balanced());
    expect(result.valid).toBe(true);
    expect(result.rows.map(r => [r.concept, r.taxCode, r.expected, r.difference])).toEqual([
      ["subtotal", undefined, 12, 0],
      ["discount", undefined, 2, 0],
      ["base", "2", 10, 0],
      ["tax", "2", 1.5, 0],
      ["base", "3", 10, 0],
      ["tax", "3", 0.4, 0],
      ["total", undefined, 11.9, 0],
    ]);
  });

  it("rejects a one-cent tax difference, a single component and a missing product", () => {
    const [a, b] = balanced();
    const offByCent = { ...b, xmlAllocation: { ...b.xmlAllocation!, taxes: [
      { purchaseReceptionLineTaxId: "tax-vat", taxableBase: 3.5, taxAmount: 0.51 },
      b.xmlAllocation!.taxes[1],
    ] } };
    expect(breakdownBalance(promo(), [a, offByCent]).valid).toBe(false);
    expect(breakdownBalance(promo(), [a]).valid).toBe(false);
    expect(breakdownBalance(promo(), [a, { ...b, itemId: null }]).valid).toBe(false);
  });

  it("replaces the promotional row in place and keeps the rest of the purchase untouched", () => {
    const lines = [manual, promo()];
    const next = replaceBreakdownRows(lines, xml.purchaseReceptionLineId, balanced(), 10, 6);
    expect(next).toHaveLength(3);
    expect(next[0]).toBe(manual);
    expect(next.slice(1).map(l => [l._key, l.itemId, l.xmlSupplierCode, l.quantityInBaseUom])).toEqual([
      [10, "item-a", "13563", 2],
      [11, "item-b", "13563", 1],
    ]);
    expect(hasInvalidBreakdown(next)).toBe(false);
  });

  it("keeps the form unchanged when the split does not reconcile", () => {
    const lines = [promo()];
    const unbalanced = balanced().map((l, i) => (i === 0 ? { ...l, xmlAllocation: { ...l.xmlAllocation!, subtotal: 1 } } : l));
    expect(replaceBreakdownRows(lines, xml.purchaseReceptionLineId, unbalanced, 10, 6)).toBe(lines);
  });

  it("components are new lines: no saved id or distributed costs are carried over", () => {
    const components = balanced().map(l => ({ ...l, lineId: "saved-line", freightAllocated: 1 }));
    const next = replaceBreakdownRows([promo()], xml.purchaseReceptionLineId, components, 10, 6);
    expect(next.map(l => [l.lineId, l.freightAllocated])).toEqual([[undefined, undefined], [undefined, undefined]]);
  });

  it("re-splitting replaces the previous components of the same origin", () => {
    const first = replaceBreakdownRows([promo()], xml.purchaseReceptionLineId, balanced(), 10, 6);
    const again = replaceBreakdownRows(first, xml.purchaseReceptionLineId, balanced().reverse(), 20, 6);
    expect(again.map(l => [l._key, l.itemId])).toEqual([[20, "item-b"], [21, "item-a"]]);
  });

  it("flags a tampered group so the form cannot be saved", () => {
    const next = replaceBreakdownRows([promo()], xml.purchaseReceptionLineId, balanced(), 10, 6);
    const tampered = next.map((l, i) => i === 0 ? { ...l, xmlAllocation: { ...l.xmlAllocation!, subtotal: 9 } } : l);
    expect(hasInvalidBreakdown(tampered)).toBe(true);
  });

  it("uses the assigned documentary amounts for totals, never quantity x price", () => {
    const [a] = balanced();
    const rounded = { ...a, unitPrice: 3.33, quantity: 3 };
    expect(lineGross(rounded)).toBe(8);
    expect(lineNet(rounded)).toBe(6.5);
    expect(resolveLineTaxes(rounded)).toEqual({ vat: 0.98, ice: 0.26, irbpnr: 0 });
  });

  it("restores allocations from the saved purchase line on reopen", () => {
    const saved = {
      xmlAllocatedSubtotal: 8, discountAmount: 1.5,
      taxes: [
        { purchaseReceptionLineTaxId: "tax-vat", taxableBase: 6.5, taxAmount: 0.98, source: "XmlAllocation" },
        { purchaseReceptionLineTaxId: "tax-ice", taxableBase: 6.5, taxAmount: 0.26, source: "XmlAllocation" },
      ],
    } as unknown as PurchaseLineDto;
    expect(restoreXmlAllocation(saved)).toEqual({ xmlAllocation: balanced()[0].xmlAllocation });
    expect(restoreXmlAllocation({ ...saved, xmlAllocatedSubtotal: null })).toEqual({});
  });
});
