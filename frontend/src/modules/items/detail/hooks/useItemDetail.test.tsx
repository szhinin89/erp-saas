// @vitest-environment jsdom
import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useItemDetailPage } from "./useItemDetail";
import { itemService } from "../../api/itemService";
import type { ItemDetailDto } from "../../../../types/items";

vi.mock("../../api/itemService", () => ({
  itemService: {
    getById: vi.fn(),
    replacePackagingLevels: vi.fn(),
  },
}));

const baseItem: ItemDetailDto = {
  id: "item-1",
  sku: "SKU-1",
  shortName: "Item",
  description: "Item",
  itemTypeId: "type-1",
  itemTypeName: "Producto",
  categoryNodeId: null,
  brandId: null,
  defaultUomCode: "UNIDAD",
  defaultUomAbbrev: "UND",
  defaultUomName: "Unidad",
  isForSale: true,
  isFavorite: false,
  isEcommerceActive: false,
  tracksStock: true,
  tracksLot: false,
  tracksSeries: false,
  baseSalePrice: null,
  isActive: true,
  createdAt: "2026-08-11T00:00:00Z",
  updatedAt: null,
  observations: null,
  taxConfig: {
    saleVatCode: null,
    saleVatName: null,
    purchaseVatCode: null,
    purchaseVatName: null,
    exciseTaxCode: null,
    exciseTaxName: null,
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
    minStockQty: null,
    maxStockQty: null,
  },
  variants: [],
  images: [],
  unitConversions: [],
  substitutes: [],
  packagingLevels: [
    {
      id: "paca-12",
      name: "PACA",
      level: 2,
      baseQuantity: 1,
      uomCode: "PACA",
      uomAbbrev: "PACA",
      barcode: null,
      weight: null,
      isBaseUnit: false,
      isPurchaseDefault: true,
      isSaleDefault: false,
      isActive: true,
    },
  ],
  supplierCodes: [
    {
      id: "supplier-code-1",
      supplierId: "supplier-1",
      supplierDisplayName: "Cervecería Nacional",
      supplierIdentification: "0999999999001",
      packagingLevelId: "paca-12",
      code: "3172",
      isPrimary: true,
      isActive: true,
    },
  ],
};

describe("useItemDetailPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("refresca el ítem después de guardar empaques y mantiene supplierCodes con packaging actualizado", async () => {
    const refreshedItem: ItemDetailDto = {
      ...baseItem,
      packagingLevels: [
        {
          ...baseItem.packagingLevels[0],
          baseQuantity: 12,
        },
      ],
    };
    vi.mocked(itemService.getById)
      .mockResolvedValueOnce(baseItem)
      .mockResolvedValueOnce(refreshedItem);
    vi.mocked(itemService.replacePackagingLevels).mockResolvedValue(refreshedItem);

    const { result } = renderHook(() => useItemDetailPage("item-1"));

    await waitFor(() => expect(result.current.item).toEqual(baseItem));

    await act(async () => {
      await result.current.replacePackagingLevels([
        {
          id: "paca-12",
          name: "PACA",
          level: 2,
          baseQuantity: 12,
          uomCode: "PACA",
          isBaseUnit: false,
          isPurchaseDefault: true,
          isSaleDefault: false,
        },
      ]);
    });

    expect(itemService.replacePackagingLevels).toHaveBeenCalledWith("item-1", [
      expect.objectContaining({ id: "paca-12", baseQuantity: 12 }),
    ]);
    expect(itemService.getById).toHaveBeenCalledTimes(2);
    expect(result.current.item?.packagingLevels[0].baseQuantity).toBe(12);
    expect(result.current.item?.supplierCodes[0].packagingLevelId).toBe("paca-12");
  });
});
