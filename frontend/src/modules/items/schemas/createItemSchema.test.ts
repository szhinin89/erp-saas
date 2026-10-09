import { describe, expect, it } from "vitest";
import { createItemSchema, defaultCreateItemValues, updateItemSchema } from "./createItemSchema";

const valid = {
  ...defaultCreateItemValues,
  sku: "VAT-TEST", shortName: "IVA", description: "IVA",
  itemTypeId: "10000000-0000-4000-8000-000000000001",
  categoryNodeId: "10000000-0000-4000-8000-000000000002",
  brandId: "10000000-0000-4000-8000-000000000003",
  defaultUomCode: "19",
  barcodes: [{ code: "VAT-TEST", barcodeType: "Internal", isPrimary: true }],
  taxConfig: { saleVatCode: "6", purchaseVatCode: "7", exciseTaxCode: null },
  baseSalePrice: 10,
};

describe("IVA explícito e independiente en Item", () => {
  for (const schema of [createItemSchema, updateItemSchema]) {
    for (const field of ["saleVatCode", "purchaseVatCode"] as const) {
      it.each([null, undefined, "", "   "])(`rechaza ${field}=%s`, (value) => {
        expect(schema.safeParse({ ...valid, taxConfig: { ...valid.taxConfig, [field]: value } }).success).toBe(false);
      });
    }
    it("conserva códigos distintos sin copiar ni asignar defaults", () => {
      expect(schema.parse(valid).taxConfig).toEqual(valid.taxConfig);
      expect(defaultCreateItemValues.taxConfig.saleVatCode).toBe("");
      expect(defaultCreateItemValues.taxConfig.purchaseVatCode).toBe("");
    });
  }
});
