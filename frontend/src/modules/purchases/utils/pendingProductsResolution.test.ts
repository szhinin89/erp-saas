import { describe, expect, it } from "vitest";
import { calcMarginPercent, calcPriceForMargin } from "../../../lib/margin";
import { pendingProductsFormSchema } from "../schemas/pendingProductsSchema";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import type { PurchaseLineReadiness } from "./purchaseLineReadiness";
import { applyBulkAssignment, applyMarginToSelected, applyResolvedReceptionLines, buildInitialRow,
  buildResolveRequest, groupRowErrors, isRealBarcode, netUnitCost, provisionalUnitBarcode, rowSalePrice,
  selectPendingProductSources, summarizeXmlLines, type PendingProductSource,
  type SalePricing } from "./pendingProductsResolution";
import type { ResolvedReceptionLine } from "../api/purchaseReceptionService";

const UNIT_EAN = "7861234567898";
const BOX_EAN = "7860000000010";
const source: PendingProductSource = {
  rowId: "a", lineKey: 1, supplierCode: "XML/A", supplierAuxCode: UNIT_EAN,
  xmlDescription: " ORIGINAL XML ", quantity: 2, unitPrice: 120, discountPct: 10,
  vatCode: "0", iceCode: "", matchStatus: "PENDING",
};
const row = () => ({ ...buildInitialRow(source, ["EAN13", "Internal"]),
  itemTypeId: "type", brandId: "brand", categoryNodeId: "category", defaultUomCode: "19", salePriceGross: 15 });
// Catálogo SRI de IVA (código → %) y precisión de precios de la empresa.
const pricing = (priceDecimals = 2): SalePricing => ({ vatPercentByCode: new Map([["0", 0], ["4", 15]]), priceDecimals });
const line = (patch: Partial<PurchaseLineFormValues> = {}): PurchaseLineFormValues => ({
  _key: 1, description: "XML", quantity: 3, unitPrice: 5, discountPct: 0, vatCode: "0", ...patch,
});

describe("pending products schema", () => {
  it("accepts create, link and another presentation without requiring new-item fields on links", () => {
    expect(pendingProductsFormSchema.safeParse({ rows: [row(),
      { ...buildInitialRow({ ...source, rowId: "b" }, []), mode: "link", linkItemId: "existing" },
      { ...buildInitialRow({ ...source, rowId: "c" }, []), mode: "presentation", parentRowId: "a",
        presentationFactor: 12, presentationName: "Caja", presentationUomCode: "02", barcode: BOX_EAN },
    ] }).success).toBe(true);
  });
  it.each(["sku", "shortName", "description", "itemTypeId", "brandId", "categoryNodeId", "defaultUomCode", "purchaseVatCode", "saleVatCode"])("requires %s on selected creates", (field) => {
    expect(pendingProductsFormSchema.safeParse({ rows: [{ ...row(), [field]: "" }] }).success).toBe(false);
  });
  it("makes the barcode optional and requires a type only for a unit barcode", () => {
    const parse = (patch: object) => pendingProductsFormSchema.safeParse({ rows: [{ ...row(), ...patch }] }).success;
    expect(parse({ barcode: "", barcodeType: "" })).toBe(true);
    expect(parse({ barcodeType: "" })).toBe(false);
    expect(parse({ barcode: BOX_EAN, barcodeType: "", presentationFactor: 12, presentationName: "Caja", presentationUomCode: "02" })).toBe(true);
  });
  it("allows the same barcode on lines of one presentation but not a code with two barcodes", () => {
    const box = { ...row(), rowId: "b", mode: "presentation" as const, parentRowId: "a", presentationFactor: 12,
      presentationName: "Caja", presentationUomCode: "02", barcode: BOX_EAN };
    expect(pendingProductsFormSchema.safeParse({ rows: [row(), box, { ...box, rowId: "c" }] }).success).toBe(true);
    const result = pendingProductsFormSchema.safeParse({ rows: [row(), box, { ...box, rowId: "c", barcode: "7860000000027" }] });
    expect(result.success).toBe(false);
    if (!result.success) expect(result.error.issues.map(i => i.path.join("."))).toEqual(["rows.1.barcode", "rows.2.barcode"]);
  });
  it.each([0, -1, Infinity, NaN])("rejects invalid price and factor %s", (value) => {
    expect(pendingProductsFormSchema.safeParse({ rows: [{ ...row(), salePriceGross: value }] }).success).toBe(false);
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
  it("prefills only a real barcode, never a supplier code, and never an implicit internal type", () => {
    expect(isRealBarcode(UNIT_EAN)).toBe(true);
    expect(isRealBarcode("12345670")).toBe(true);
    expect(isRealBarcode("1234567890123")).toBe(false);
    expect(isRealBarcode("XML/A")).toBe(false);
    expect(buildInitialRow(source, ["EAN13"])).toMatchObject({ barcode: UNIT_EAN, barcodeType: "EAN13" });
    expect(buildInitialRow({ ...source, supplierAuxCode: "", supplierCode: UNIT_EAN }, ["EAN13"]).barcode).toBe(UNIT_EAN);
    // FASE 5: un código auxiliar que cumple las validaciones de Items se propone como código de
    // barras de la unidad, pero sin tipo implícito (no es GTIN).
    expect(buildInitialRow({ ...source, supplierAuxCode: "AUX-9" }, ["EAN13", "Internal"]))
      .toMatchObject({ barcode: "AUX-9", barcodeType: "" });
  });
  it("shows the supplier code as provisional unit barcode only when the unit has no real one", () => {
    const sources = new Map([["a", { ...source, supplierCode: "SUP-U" }], ["b", { ...source, rowId: "b", supplierCode: "SUP-BOX" }]]);
    const box = { ...row(), rowId: "b", mode: "presentation" as const, parentRowId: "a", presentationFactor: 12, barcode: BOX_EAN };
    expect(provisionalUnitBarcode([row(), box], "a", sources)).toBeNull();
    expect(provisionalUnitBarcode([{ ...row(), barcode: "" }, box], "a", sources)).toBe("SUP-U");
    // Bought only by box: the box supplier code, unless it is the box barcode itself.
    const boxOnly = { ...row(), barcode: BOX_EAN, presentationFactor: 12 };
    expect(provisionalUnitBarcode([boxOnly], "a", sources)).toBe("SUP-U");
    expect(provisionalUnitBarcode([boxOnly], "a", new Map([["a", { ...source, supplierCode: BOX_EAN }]]))).toBeNull();
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
    const result = applyMarginToSelected([{ ...row(), presentationFactor: 12 }], new Map([["a", source]]), 25, pricing());
    expect(result[0].salePriceGross).toBe(12);
    expect(calcMarginPercent(9, 12)).toBe(25);
    expect(calcPriceForMargin(9, 25)).toBe(12);
  });
  it.each([100, 120, NaN, Infinity, -Infinity])("ignores unreachable/nonfinite margins %s", margin => {
    expect(calcPriceForMargin(9, margin)).toBeNull();
    expect(applyMarginToSelected([row()], new Map([["a", source]]), margin, pricing())[0].salePriceGross).toBe(15);
  });
  it("does not price existing links, presentations or unselected rows", () => {
    const rows = [{ ...row(), selected: false }, { ...row(), mode: "link" as const }, { ...row(), mode: "presentation" as const }];
    expect(applyMarginToSelected(rows, new Map([["a", source]]), 25, pricing())).toEqual(rows);
    expect(calcPriceForMargin(Infinity, 25)).toBeNull();
    expect(calcPriceForMargin(Number.MAX_VALUE, 99)).toBeNull();
    expect(calcPriceForMargin(0, 25)).toBeNull();
  });
});

describe("PVP final con IVA → BaseSalePrice sin IVA (FASE 3)", () => {
  const vatRow = (gross: number) => ({ ...row(), saleVatCode: "4", salePriceGross: gross });
  it("derives BaseSalePrice without VAT using the company price precision, not a fixed one", () => {
    expect(rowSalePrice(vatRow(1.19), source, pricing(4))?.net).toBe(1.0348);
    expect(rowSalePrice(vatRow(1.19), source, pricing(2))?.net).toBe(1.03);
    expect(buildResolveRequest([vatRow(1.19)], pricing(4)).newItems[0].baseSalePrice).toBe(1.0348);
    expect(buildResolveRequest([vatRow(1.19)], pricing(6)).newItems[0].baseSalePrice).toBe(1.034783);
  });
  it("recomputes the effective price from the persistible net and warns when it differs", () => {
    expect(rowSalePrice(vatRow(1.19), source, pricing(2))).toMatchObject({ effectiveGross: 1.18, differs: true });
    expect(rowSalePrice(vatRow(1.19), source, pricing(4))).toMatchObject({ effectiveGross: 1.19, differs: false });
    expect(rowSalePrice(vatRow(1.15), source, pricing(2))).toMatchObject({ net: 1, vatAmount: 0.15, effectiveGross: 1.15, differs: false });
  });
  it("reports profit, margin on price and markup on cost against the net cost per base unit", () => {
    // Caja x12 a 120 −10 % → 9 por unidad base; PVP 13.80 con IVA 15 % → 12 sin IVA.
    const breakdown = rowSalePrice({ ...vatRow(13.8), presentationFactor: 12 }, source, pricing(2));
    expect(breakdown).toMatchObject({ net: 12, profit: 3, marginPct: 25 });
    expect(breakdown?.markupPct).toBeCloseTo(33.3333, 4);
    expect(rowSalePrice(vatRow(13.8), undefined, pricing(2))).toMatchObject({ profit: null, marginPct: null, markupPct: null });
  });
  it("treats a product without VAT as final price = BaseSalePrice", () => {
    expect(rowSalePrice(row(), source, pricing(2))).toMatchObject({ net: 15, vatAmount: 0, effectiveGross: 15, differs: false });
    expect(buildResolveRequest([row()], pricing(2)).newItems[0].baseSalePrice).toBe(15);
  });
  it("needs a known sales VAT to price; never sends an invented price", () => {
    expect(rowSalePrice({ ...row(), saleVatCode: "" }, source, pricing())).toBeNull();
    expect(buildResolveRequest([{ ...row(), saleVatCode: "X" }], pricing()).newItems[0].baseSalePrice).toBe(0);
  });
  it("bulk margin ends as a final price with VAT per row, through the rounding extension point", () => {
    const rows = [{ ...vatRow(0), presentationFactor: 12 }, { ...row(), rowId: "b", presentationFactor: 12, saleVatCode: "" }];
    const sources = new Map([["a", source], ["b", { ...source, rowId: "b" }]]);
    const result = applyMarginToSelected(rows, sources, 25, pricing(2));
    expect(result[0].salePriceGross).toBe(13.8);
    expect(result[1]).toBe(rows[1]);
    expect(applyMarginToSelected(rows, sources, 25, pricing(2), () => 13.99)[0].salePriceGross).toBe(13.99);
  });
});

describe("resolved/corrected reception lines on the purchase form", () => {
  const resolved = (patch: Partial<ResolvedReceptionLine> = {}): ResolvedReceptionLine => ({
    purchaseReceptionLineId: "r1", itemId: "C", itemSku: "C-1", itemName: "Correcto", matchStatus: "MANUALLY_MATCHED",
    packagingLevelId: "box", uomCode: "CJ", baseUomCode: "UND", conversionFactor: 12, equivalenceLearned: true, ...patch,
  });
  it("relinks the corrected line to the new product and base quantity and reloads its context", () => {
    const lines = [
      line({ _key: 1, purchaseReceptionLineId: "r1", itemId: "B", quantity: 2, context: { shortName: "B" } as PurchaseLineFormValues["context"] }),
      line({ _key: 2, purchaseReceptionLineId: "r2", itemId: "B", quantity: 1 }),
    ];
    const [corrected, untouched] = applyResolvedReceptionLines(lines, [resolved()]);
    expect(corrected).toMatchObject({ itemId: "C", packagingLevelId: "box", uomCode: "CJ", conversionFactor: 12, quantityInBaseUom: 24, context: undefined });
    expect(untouched).toBe(lines[1]);
  });
  it("keeps the loaded context when only the presentation changes", () => {
    const context = { shortName: "C" } as PurchaseLineFormValues["context"];
    const [result] = applyResolvedReceptionLines([line({ purchaseReceptionLineId: "r1", itemId: "C", context })], [resolved()]);
    expect(result.context).toBe(context);
  });
});

describe("request, row errors and XML summary", () => {
  it("builds a mixed batch with one item and unit/box references, excludes unselected rows", () => {
    const request = buildResolveRequest([row(), { ...row(), rowId: "b", mode: "presentation", parentRowId: "a", presentationFactor: 12, presentationName: " Caja ", presentationUomCode: "02", barcode: ` ${BOX_EAN} ` },
      { ...row(), rowId: "c", mode: "link", linkItemId: "item", linkPackagingLevelId: "level" }, { ...row(), rowId: "d", selected: false }], pricing());
    expect(request.newItems).toHaveLength(1);
    expect(request.newItems[0]).toMatchObject({ key: "a", baseSalePrice: 15, exciseTaxCode: null });
    expect(request.newItems[0]).not.toHaveProperty("barcode");
    expect(request.lines).toEqual([
      { purchaseReceptionLineId: "a", newItemKey: "a", presentationFactor: 1, presentationName: null, presentationUomCode: null, barcode: UNIT_EAN, barcodeType: "EAN13" },
      { purchaseReceptionLineId: "b", newItemKey: "a", presentationFactor: 12, presentationName: "Caja", presentationUomCode: "02", barcode: BOX_EAN, barcodeType: null },
      { purchaseReceptionLineId: "c", itemId: "item", packagingLevelId: "level" },
    ]);
  });
  it("sends no barcode when the line has none", () => {
    expect(buildResolveRequest([{ ...row(), barcode: "", barcodeType: "" }], pricing()).lines[0])
      .toMatchObject({ barcode: null, barcodeType: null });
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

describe("FASE 5 — código auxiliar del XML: código de barras, nunca SKU", () => {
  // Caso real: código principal 8433, código auxiliar 001013300320845306 (18 dígitos, no GTIN).
  const xml: PendingProductSource = { ...source, supplierCode: "8433", supplierAuxCode: "001013300320845306" };
  const ready = (r: ReturnType<typeof buildInitialRow>) => ({
    ...r, itemTypeId: "type", brandId: "brand", categoryNodeId: "category", defaultUomCode: "19", salePriceGross: 15,
  });

  it("1. auxiliar válido → se propone como código de barras de la unidad, no como SKU", () => {
    const initial = buildInitialRow(xml, ["EAN13", "Code128", "Internal"]);

    expect(initial.barcode).toBe("001013300320845306");
    expect(initial.barcodeType).toBe("");
    expect(initial.sku).toBe("8433");
    expect(initial.sku).not.toContain("001013300320845306");

    const request = buildResolveRequest([{ ...ready(initial), barcodeType: "Code128" }], pricing());
    expect(request.newItems[0].sku).toBe("8433");
    expect(request.lines[0]).toMatchObject({ barcode: "001013300320845306", barcodeType: "Code128" });
  });

  it("1b. auxiliar GTIN real → código de barras con su tipo EAN sugerido", () => {
    expect(buildInitialRow({ ...xml, supplierAuxCode: UNIT_EAN }, ["EAN13"])).toMatchObject({
      barcode: UNIT_EAN, barcodeType: "EAN13", sku: "8433",
    });
  });

  it("2. auxiliar no válido → no contamina SKU ni código de barras (queda como evidencia XML)", () => {
    const tooLong = "9".repeat(101);
    const initial = buildInitialRow({ ...xml, supplierAuxCode: tooLong }, ["EAN13"]);

    expect(initial).toMatchObject({ barcode: "", barcodeType: "", sku: "8433" });
    // La evidencia XML no se toca.
    expect({ ...xml, supplierAuxCode: tooLong }.supplierAuxCode).toBe(tooLong);
    // Sin auxiliar válido, un código principal GTIN real sigue siendo el código de barras (FASE 3).
    expect(buildInitialRow({ ...xml, supplierAuxCode: tooLong, supplierCode: UNIT_EAN }, ["EAN13"]).barcode)
      .toBe(UNIT_EAN);
  });

  it("3. el código del proveedor sigue siendo la relación aprendida (línea de recepción → producto nuevo)", () => {
    const initial = ready(buildInitialRow(xml, ["Code128"]));
    const request = buildResolveRequest([{ ...initial, barcodeType: "Code128" }], pricing());

    // El backend aprende Proveedor + código principal desde la línea de recepción indicada.
    expect(request.lines[0]).toMatchObject({ purchaseReceptionLineId: xml.rowId, newItemKey: xml.rowId });
    expect(request.newItems[0]).toMatchObject({ key: xml.rowId, sku: "8433" });
    // Sin código de barras de la unidad, el código del proveedor sigue siendo el Interno provisional.
    const sources = new Map([[xml.rowId, xml]]);
    expect(provisionalUnitBarcode([{ ...initial, barcode: "" }], xml.rowId, sources)).toBe("8433");
  });

  it("4. creación sin código auxiliar sigue funcionando", () => {
    const initial = ready(buildInitialRow({ ...xml, supplierAuxCode: "" }, ["EAN13"]));

    expect(initial).toMatchObject({ barcode: "", barcodeType: "", sku: "8433" });
    expect(pendingProductsFormSchema.safeParse({ rows: [initial] }).success).toBe(true);
    const request = buildResolveRequest([initial], pricing());
    expect(request.newItems[0].sku).toBe("8433");
    expect(request.lines[0]).toMatchObject({ barcode: null, barcodeType: null });
  });
});

describe("FASE 5 — unidad de medida duplicada entre presentaciones del producto nuevo", () => {
  // Caso real DINADEC: 8433, unidad base = Unidad (19), SIXPACK X6 con unidad de presentación Unidad.
  const sixpack = (presentationUomCode: string) => ({
    ...buildInitialRow({ ...source, supplierCode: "8433", supplierAuxCode: "001013300320845306" }, []),
    itemTypeId: "type", brandId: "brand", categoryNodeId: "category", defaultUomCode: "19", salePriceGross: 15,
    presentationFactor: 6, presentationName: "SIXPACK X6", presentationUomCode, barcodeType: "",
  });
  const uomErrors = (rows: ReturnType<typeof sixpack>[]) => {
    const parsed = pendingProductsFormSchema.safeParse({ rows });
    return parsed.success ? [] : parsed.error.issues.filter((i) => i.path.at(-1) === "presentationUomCode");
  };

  it("bloquea SIXPACK X6 con la misma unidad que la unidad base y pide otra (Paquete/Caja)", () => {
    const errors = uomErrors([sixpack("19")]);

    expect(errors).toHaveLength(1);
    expect(errors[0].path).toEqual(["rows", 0, "presentationUomCode"]);
    expect(errors[0].message).toContain('"SIXPACK X6" no puede usar la misma unidad que la unidad base');
    expect(errors[0].message).toContain("Paquete o Caja");
  });

  it("acepta la presentación con su propia unidad (Paquete)", () => {
    expect(pendingProductsFormSchema.safeParse({ rows: [sixpack("14")] }).success).toBe(true);
  });

  it("también bloquea dos presentaciones del mismo producto nuevo con la misma unidad", () => {
    const base = { ...sixpack("14"), rowId: "a" };
    const box = { ...sixpack("14"), rowId: "b", mode: "presentation" as const, parentRowId: "a",
      presentationFactor: 12, presentationName: "CAJA X12", barcode: "" };
    const errors = uomErrors([base, box]);

    expect(errors.length).toBeGreaterThan(0);
    expect(errors[0].message).toContain("Otra presentación de este producto ya usa esa unidad");
  });
});
