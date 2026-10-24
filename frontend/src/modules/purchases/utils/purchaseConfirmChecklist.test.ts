import { describe, expect, it } from "vitest";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import { lineCardContext } from "../pages/purchaseLineCard.fixture";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import { buildConfirmChecklist, buildConfirmLineWarnings } from "./purchaseConfirmChecklist";
import type { PurchaseLineReadiness } from "./purchaseLineReadiness";

const t = (key: string, params?: unknown) =>
  params && typeof params === "object" && "count" in params ? `${key}:${String((params as { count: number }).count)}`
    : params && typeof params === "object" && "amount" in params ? `${key}:${String((params as { amount: string }).amount)}`
      : key;
const VAT = { "4": 15 };

function line(overrides: Partial<PurchaseLineFormValues> = {}): PurchaseLineFormValues {
  return {
    _key: 1, itemId: "item-1", description: "Producto", quantity: 10, unitPrice: 1, vatCode: "4", discountPct: 0,
    warehouseId: "wh-1", packagingLevelId: "box", purchaseReceptionLineId: "rl-1", xmlTotalLine: 11.5,
    context: { participatesInInventory: true } as PurchaseLineFormValues["context"],
    ...overrides,
  };
}
const build = (lines: PurchaseLineFormValues[]) => buildConfirmChecklist(lines, [], VAT, {}, t, 2);

describe("buildConfirmChecklist (P2)", () => {
  it("zero issues: every passed check is summarized in one line, without per-check success messages", () => {
    const summary = build([line(), line({ _key: 2, purchaseReceptionLineId: "rl-2" })]);
    expect(summary.issues).toEqual([]);
    expect(summary.blockerCount).toBe(0);
    expect(summary.passed).toEqual([
      "purchases.confirm.checklist.linkedItems (2)",
      "purchases.confirm.checklist.linkedPresentations",
      "purchases.confirm.checklist.recognizedTaxes (2)",
      expect.stringMatching(/^purchases\.confirm\.checklist\.totalDifference \$0\.00$/),
    ]);
  });

  it("one issue: a linked stock line without presentation is a single presentation blocker", () => {
    const summary = build([line({ packagingLevelId: undefined })]);
    expect(summary.blockerCount).toBe(1);
    expect(summary.issues).toEqual([{
      key: "presentations", tone: "danger", label: "purchases.confirm.checklist.linkedPresentations",
      detail: "purchases.confirm.checklist.linkedPresentationsMissing:1",
    }]);
    expect(summary.passed).toHaveLength(3);
  });

  it("several issues: blockers first, then warnings; the same lines are never reported twice", () => {
    const summary = build([
      line({ itemId: undefined, packagingLevelId: undefined, context: undefined }),
      line({ _key: 2, purchaseReceptionLineId: "rl-2", packagingLevelId: undefined, context: undefined, xmlTotalLine: 20 }),
    ]);
    expect(summary.issues.map(i => [i.key, i.tone])).toEqual([
      ["linked-items", "danger"],
      ["presentations", "warning"],
      ["total-difference", "warning"],
    ]);
    expect(summary.issues[1].detail).toBe("purchases.confirm.checklist.linesWithoutPresentationUnknown:1");
    expect(summary.blockerCount).toBe(1);
    expect(summary.passed).toEqual(["purchases.confirm.checklist.recognizedTaxes (2)"]);
  });

  it("missing XML totals stay a warning, not a passed check", () => {
    const summary = build([line({ xmlTotalLine: undefined })]);
    expect(summary.issues).toEqual([expect.objectContaining({
      key: "total-difference", tone: "warning", detail: "purchases.confirm.checklist.totalDifferenceUnavailable",
    })]);
  });

  it("manual purchases have no XML checks", () => {
    expect(build([line({ purchaseReceptionLineId: undefined })])).toEqual({ hasXmlPurchase: false, blockerCount: 0, issues: [], passed: [] });
  });
});

// PURCHASE-CONFIRM-LINE-WARNINGS-01 — advertencias no bloqueantes de línea en el modal de confirmación.
describe("buildConfirmLineWarnings", () => {
  const ready = { status: "READY", label: "Lista", detail: "", tone: "success", blocking: false } as PurchaseLineReadiness;
  const salePriceWarning = { status: "MISSING_SALE_PRICE_FOR_MARGIN", label: "Sin precio de venta", detail: "", tone: "warning" } as NonNullable<PurchaseLineReadiness["warning"]>;
  const warehouseBlocker = { status: "MISSING_WAREHOUSE", label: "Falta bodega", detail: "", tone: "warning", blocking: true } as PurchaseLineReadiness;
  const tw = ((key: string, params?: unknown) => typeof params === "string" ? params
    : params && typeof params === "object" && "count" in params ? `L${String((params as { count: number }).count)}` : key) as Parameters<typeof buildConfirmLineWarnings>[5];
  const matching = (o: Partial<PurchaseLineFormValues> = {}) => line({ unitPrice: 0.5, xmlQuantity: 10, xmlUnitPrice: 0.5, packagingLevelId: "unit", description: "Leche",
    context: { ...lineCardContext, shortName: "", hasIce: false, icePercent: 0, exciseTaxCode: null }, ...o });
  const warnings = (lines: PurchaseLineFormValues[], readiness: Record<number, PurchaseLineReadiness>) =>
    buildConfirmLineWarnings(lines, readiness, TEST_PRECISION_POLICY, VAT, {}, tw);

  it("no warnings: lines matching the supplier document report nothing", () => {
    expect(warnings([matching()], { 1: ready })).toEqual([]);
  });

  it("one warning: the edited quantity/cost is reported with its line and reason", () => {
    expect(warnings([matching({ quantity: 12 })], { 1: ready })).toEqual([
      { key: "1-0", line: "L1 · Leche", reason: "Cantidad o costo distintos al documento del proveedor" },
    ]);
  });

  it("commercial warnings are not listed per line; operational ones still are", () => {
    const result = warnings(
      [matching({ quantity: 12 }), matching({ _key: 2, description: "Queso", purchaseReceptionLineId: "rl-2" })],
      { 1: { ...ready, warning: salePriceWarning }, 2: { ...ready, warning: salePriceWarning } },
    );
    expect(result.map(w => `${w.line}: ${w.reason}`)).toEqual([
      "L1 · Leche: Cantidad o costo distintos al documento del proveedor",
    ]);
  });

  it("a real blocker is not repeated as a warning; warnings on the same line still show", () => {
    const result = warnings([matching({ quantity: 12, warehouseId: "" })], { 1: warehouseBlocker });
    expect(result.map(w => w.reason)).toEqual(["Cantidad o costo distintos al documento del proveedor"]);
  });
});
