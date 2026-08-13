import { describe, expect, it } from "vitest";
import type { ItemDetailDto } from "../../../types/items";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import {
  buildPurchaseItemProfile,
  buildPurchaseLineFromItem,
  normalizePurchaseLinePresentation,
  resolvePurchaseItemSelection,
} from "./purchaseItemProfile";

const item: ItemDetailDto = {
  id: "item-1",
  sku: "SKU-1",
  shortName: "Cola",
  description: "Cola botella",
  itemTypeId: "type-1",
  itemTypeName: "Bebida",
  categoryNodeId: null,
  brandId: null,
  defaultUomCode: "UNIT",
  defaultUomAbbrev: "u",
  isForSale: true,
  isFavorite: false,
  isEcommerceActive: false,
  tracksStock: true,
  tracksLot: false,
  tracksSeries: false,
  baseSalePrice: 1.25,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  defaultUomName: "Unidad",
  observations: null,
  taxConfig: {
    saleVatCode: "2",
    saleVatName: "IVA",
    purchaseVatCode: "2",
    purchaseVatName: "IVA",
    exciseTaxCode: "ICE",
    exciseTaxName: "ICE",
  },
  saleConfig: {
    isForSale: true,
    maxDiscountPercent: null,
    isAvailableOnWeb: false,
    isAvailableOnPOS: true,
    isAvailableOnMobile: false,
    isEcommerceActive: false,
    isFavorite: false,
  },
  stockConfig: {
    tracksStock: true,
    tracksLot: false,
    tracksSeries: false,
    allowDecimalQty: false,
    allowDecimalSale: false,
    minStockQty: 3,
    maxStockQty: null,
  },
  variants: [],
  images: [],
  unitConversions: [],
  substitutes: [],
  packagingLevels: [],
  supplierCodes: [],
};

describe("purchaseItemProfile", () => {
  it("construye el mismo perfil para ProductPicker y busqueda global", () => {
    const profile = buildPurchaseItemProfile(item, { vatRates: { "2": 15 } });
    const line = buildPurchaseLineFromItem(item, { key: 7 });

    expect(profile.label).toBe("SKU-1 — Cola");
    expect(profile.purchaseVatCode).toBe("2");
    expect(profile.exciseTaxCode).toBe("ICE");
    expect(profile.vatRate).toBe("15%");
    expect(line).toMatchObject({
      _key: 7,
      itemId: "item-1",
      description: "SKU-1 — Cola",
      quantity: 1,
      unitPrice: 0,
      vatCode: "2",
      discountPct: 0,
      iceCode: "ICE",
    });
  });

  it("preserva la presentacion existente al aplicar un item a una linea XML/TXT", () => {
    const existingLine = {
      _key: 1,
      itemId: undefined,
      description: "Proveedor",
      quantity: 2,
      unitPrice: 9,
      vatCode: "0",
      discountPct: 0,
      purchaseReceptionLineId: "reception-line-1",
      packagingLevelId: "paca-12",
      uomCode: "PACA",
      baseUomCode: "UNIT",
      conversionFactor: 12,
      quantityInBaseUom: 24,
    } satisfies PurchaseLineFormValues;

    const patch = resolvePurchaseItemSelection(
      buildPurchaseItemProfile(item),
      { existingLine },
    );

    expect(patch).toMatchObject({
      itemId: "item-1",
      description: "SKU-1 — Cola",
      packagingLevelId: "paca-12",
      uomCode: "PACA",
      baseUomCode: "UNIT",
      conversionFactor: 12,
      quantityInBaseUom: 24,
    });
    expect(patch).not.toHaveProperty("vatCode");
  });

  it("normaliza presentacion sin inventar valores cuando no viene del backend", () => {
    expect(normalizePurchaseLinePresentation(null)).toEqual({
      packagingLevelId: undefined,
      uomCode: undefined,
      baseUomCode: undefined,
      conversionFactor: undefined,
      quantityInBaseUom: undefined,
    });
  });
});
