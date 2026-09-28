import { describe, expect, it } from "vitest";
import { calcMarginPercent, calcPriceForMargin } from "../../../lib/margin";
import { pendingProductsFormSchema } from "../schemas/pendingProductsSchema";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import type { PurchaseLineReadiness } from "./purchaseLineReadiness";
import { applyBulkAssignment, applyMarginToSelected, buildInitialRow, buildResolveRequest,
  groupRowErrors, netUnitCost, selectPendingProductSources, summarizeXmlLines,
  type PendingProductSource } from "./pendingProductsResolution";

const source: PendingProductSource = {
  rowId: "a", lineKey: 1, supplierCode: "XML/A", supplierAuxCode: "1234567890123",
  xmlDescription: " ORIGINAL XML ", quantity: 2, unitPrice: 120, discountPct: 10,
  vatCode: "0", iceCode: "", matchStatus: "PENDING",
};
const row = () => ({ ...buildInitialRow(source, ["EAN13", "Internal"]),
  itemTypeId: "type", brandId: "brand", categoryNodeId: "category", defaultUomCode: "19", salePrice: 15 });
const line = (patch: Partial<PurchaseLineFormValues> = {}): PurchaseLineFormValues => ({
  _key: 1, description: "XML", quantity: 3, unitPrice: 5, discountPct: 0, vatCode: "0", ...patch,
});

describe("pending products schema", () => {
  it("accepts create, link and another presentation without requiring new-item fields on links", () => {
    expect(pendingProductsFormSchema.safeParse({ rows: [row(),
      { ...buildInitialRow({ ...source, rowId: "b" }, []), mode: "link", linkItemId: "existing" },
      { ...buildInitialRow({ ...source, rowId: "c" }, []), mode: "presentation", parentRowId: "a",
        presentationFactor: 12, presentationName: "Caja", presentationUomCode: "02" },
    ] }).success).toBe(true);
  });
  it.each(["sku", "shortName", "description", "itemTypeId", "brandId", "categoryNodeId", "defaultUomCode", "barcode", "barcodeType", "purchaseVatCode", "saleVatCode"])("requires %s on selected creates", (field) => {
    expect(pendingProductsFormSchema.safeParse({ rows: [{ ...row(), [field]: "" }] }).success).toBe(false);
  });
  it.each([0, -1, Infinity, NaN])("rejects invalid price and factor %s", (value) => {
    expect(pendingProductsFormSchema.safeParse({ rows: [{ ...row(), salePrice: value }] }).success).toBe(false);
    expect(pendingProductsFormSchema.safeParse({ rows: [{ ...row(), presentationFactor: value }] }).success).toBe(false);
  });
  it("reports duplicate SKUs/barcodes on both rows and missing selected parents", () => {
    const result = pendingProductsFormSchema.safeParse({ rows: [row(), { ...row(), rowId: "b", sku: row().sku.toLowerCase() },
      { ...row(), rowId: "c", mode: "presentation", parentRowId: "absent" }] });
    expect(result.success).toBe(false);
    if (!result.success) expect(result.error.issues.map(i => i.path.join("."))).toEqual(expect.arrayContaining([
      "rows.0.sku", "rows.1.sku", "rows.0.barcode", "rows.1.barcode", "rows.2.parentRowId",
    ]));
  });
  it("ignores incomplete unselected rows and requires box details", () => {
    expect(pendingProductsFormSchema.safeParse({ rows: [{ ...buildInitialRow(source, []), selected: false }] }).success).toBe(true);
    expect(pendingProductsFormSchema.safeParse({ rows: [{ ...row(), presentationFactor: 12 }] }).success).toBe(false);
  });
});

describe("XML sources and initial values", () => {
  it("selects only unmatched reception lines and prefers immutable XML quantity/price including zero", () => {
    const sources = selectPendingProductSources([line(), line({ itemId: "known", purchaseReceptionLineId: "known" }),
      line({ purchaseReceptionLineId: "a", xmlQuantity: 0, xmlUnitPrice: 0, xmlSupplierCode: "ABC" })]);
    expect(sources).toHaveLength(1);
    expect(sources[0]).toMatchObject({ rowId: "a", quantity: 0, unitPrice: 0, supplierCode: "ABC" });
  });
  it("prefills XML taxes, editable ERP names and barcode type without mutating XML", () => {
    const initial = buildInitialRow(source, ["EAN13"]);
    expect(initial).toMatchObject({ shortName: "ORIGINAL XML", description: "ORIGINAL XML", barcodeType: "EAN13", purchaseVatCode: "0", saleVatCode: "0" });
    initial.description = "ERP";
    expect(source.xmlDescription).toBe(" ORIGINAL XML ");
    expect(buildInitialRow({ ...source, supplierAuxCode: "" }, []).sku).toBe("XML-A");
    expect(buildInitialRow(source, []).barcodeType).toBe("");
  });
  it("uses the original XML description and discount after editing the purchase fields", () => {
    const [result] = selectPendingProductSources([line({ purchaseReceptionLineId: "a", description: "ERP edited",
      xmlDescription: "XML original", quantity: 7, unitPrice: 99, discountPct: 50,
      xmlQuantity: 2, xmlUnitPrice: 120, xmlDiscount: 24 })]);
    expect(result).toMatchObject({ xmlDescription: "XML original", quantity: 2, unitPrice: 120, discountPct: 10 });
    expect(netUnitCost(result, 12)).toBe(9);
  });
});

describe("bulk assignment and margin SSOT", () => {
  it("changes only selected rows and ignores blank assignments without mutating input", () => {
    const original = [row(), { ...row(), selected: false }];
    const result = applyBulkAssignment(original, { brandId: "new", saleVatCode: "0", categoryNodeId: "" });
    expect(result[0]).toMatchObject({ brandId: "new", categoryNodeId: "category", saleVatCode: "0" });
    expect(result[1]).toBe(original[1]);
    expect(original[0].brandId).toBe("brand");
  });
  it("uses net cost per base unit and margin on price, rounding only the result", () => {
    expect(netUnitCost(source, 12)).toBe(9);
    const result = applyMarginToSelected([{ ...row(), presentationFactor: 12 }], new Map([["a", source]]), 25, 2);
    expect(result[0].salePrice).toBe(12);
    expect(calcMarginPercent(9, 12)).toBe(25);
    expect(calcPriceForMargin(9, 25)).toBe(12);
  });
  it.each([100, 120, NaN, Infinity, -Infinity])("ignores unreachable/nonfinite margins %s", margin => {
    expect(calcPriceForMargin(9, margin)).toBeNull();
    expect(applyMarginToSelected([row()], new Map([["a", source]]), margin, 2)[0].salePrice).toBe(15);
  });
  it("does not price existing links, presentations or unselected rows", () => {
    const rows = [{ ...row(), selected: false }, { ...row(), mode: "link" as const }, { ...row(), mode: "presentation" as const }];
    expect(applyMarginToSelected(rows, new Map([["a", source]]), 25, 2)).toEqual(rows);
    expect(calcPriceForMargin(Infinity, 25)).toBeNull();
    expect(calcPriceForMargin(Number.MAX_VALUE, 99)).toBeNull();
    expect(calcPriceForMargin(0, 25)).toBeNull();
  });
});

describe("request, row errors and XML summary", () => {
  it("builds a mixed batch with one item and unit/box references, excludes unselected rows", () => {
    const request = buildResolveRequest([row(), { ...row(), rowId: "b", mode: "presentation", parentRowId: "a", presentationFactor: 12, presentationName: " Caja ", presentationUomCode: "02" },
      { ...row(), rowId: "c", mode: "link", linkItemId: "item", linkPackagingLevelId: "level" }, { ...row(), rowId: "d", selected: false }]);
    expect(request.newItems).toHaveLength(1);
    expect(request.newItems[0]).toMatchObject({ key: "a", baseSalePrice: 15, exciseTaxCode: null });
    expect(request.lines).toEqual([
      { purchaseReceptionLineId: "a", newItemKey: "a", presentationFactor: 1, presentationName: null, presentationUomCode: null },
      { purchaseReceptionLineId: "b", newItemKey: "a", presentationFactor: 12, presentationName: "Caja", presentationUomCode: "02" },
      { purchaseReceptionLineId: "c", itemId: "item", packagingLevelId: "level" },
    ]);
  });
  it("groups all row messages and keeps general errors", () => {
    const result = groupRowErrors([
      { purchaseReceptionLineId: "a", newItemKey: "b", message: "line" },
      { purchaseReceptionLineId: null, newItemKey: "a", message: "item" },
      { purchaseReceptionLineId: null, newItemKey: null, message: "general" },
    ]);
    expect(result.byRow.get("a")).toEqual(["line", "item"]);
    expect(result.general).toEqual(["general"]);
  });
  it("counts only XML lines, including nonblocking warnings", () => {
    const ready: PurchaseLineReadiness = { status: "READY", label: "", detail: "", tone: "success", blocking: false, primaryAction: "NONE" };
    expect(summarizeXmlLines([line(), line({ purchaseReceptionLineId: "a" }),
      line({ _key: 2, purchaseReceptionLineId: "b", itemId: "item" }),
      line({ _key: 3, purchaseReceptionLineId: "c", itemId: "item" })], {
        2: ready, 3: { ...ready, warning: { status: "MISSING_SALE_PRICE_FOR_MARGIN", label: "", detail: "", tone: "warning" } },
      })).toEqual({ total: 3, resolved: 1, pending: 1, warnings: 1 });
  });
});
