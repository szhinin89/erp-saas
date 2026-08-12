// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import {
  BarcodePrincipalSummary,
  SupplierCodesPrincipalSummary,
} from "./PrincipalCodesSummary";
import type { ItemDetailDto } from "../../../../types/items";

afterEach(() => cleanup());

const t = (_key: string, fallback?: string) => fallback ?? _key;

const baseItem: ItemDetailDto = {
  id: "item-1",
  sku: "FAN-1350",
  shortName: "FANTA",
  description: "FANTA HARMONY NRJ 1350 PET",
  observations: null,
  itemTypeId: "physical",
  itemTypeName: "Fisico",
  categoryNodeId: null,
  brandId: null,
  defaultUomCode: "UN",
  defaultUomAbbrev: "UN",
  defaultUomName: "Unidad",
  isForSale: true,
  isFavorite: false,
  isEcommerceActive: false,
  tracksStock: true,
  tracksLot: false,
  tracksSeries: false,
  baseSalePrice: 1,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
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
    isAvailableOnPOS: false,
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
  variants: [
    {
      id: "variant-1",
      sku: "FAN-1350",
      name: "Default",
      isDefault: true,
      sortOrder: 0,
      isActive: true,
      attributes: [],
      barcodes: [
        {
          id: "barcode-1",
          code: "7501234567890",
          barcodeType: "EAN13",
          isPrimary: true,
        },
      ],
    },
  ],
  images: [],
  unitConversions: [],
  substitutes: [],
  packagingLevels: [
    {
      id: "paca-12",
      name: "PACA X12",
      level: 2,
      baseQuantity: 12,
      uomCode: "UN",
      uomAbbrev: "UN",
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
      supplierId: "a8a07f8a-e16",
      supplierDisplayName: "Cerveceria Nacional",
      supplierIdentification: "0999999999001",
      packagingLevelId: "paca-12",
      code: "8431",
      isPrimary: true,
      isActive: true,
    },
  ],
};

describe("PrincipalCodesSummary", () => {
  it("muestra resumen compacto de códigos de barras con CTA al detalle", () => {
    const { container } = render(
      <BarcodePrincipalSummary
        t={t}
        item={baseItem}
        onManageBarcodes={() => undefined}
      />,
    );

    expect(screen.getByText("7501234567890")).toBeTruthy();
    expect(
      screen.getByRole("button", {
        name: "Gestionar códigos de barras en detalle del ítem",
      }),
    ).toBeTruthy();
    expect(container.querySelector(".empty-state")).toBeNull();
  });

  it("muestra proveedor, RUC, código y presentación en edición", () => {
    render(
      <SupplierCodesPrincipalSummary
        t={t}
        item={baseItem}
        onManageSupplierPresentations={() => undefined}
      />,
    );

    expect(screen.getByText("Cerveceria Nacional")).toBeTruthy();
    expect(screen.getByText("RUC: 0999999999001")).toBeTruthy();
    expect(screen.getByText("Código: 8431")).toBeTruthy();
    expect(screen.getByText("PACA X12 x 12 UN")).toBeTruthy();
    expect(
      screen.getByRole("button", {
        name: "Completar presentación en Inventario y presentaciones",
      }),
    ).toBeTruthy();
    expect(
      screen.queryByRole("button", { name: "Agregar código de proveedor" }),
    ).toBeNull();
  });

  it("usa fallback controlado cuando el proveedor no tiene nombre", () => {
    render(
      <SupplierCodesPrincipalSummary
        t={t}
        item={{
          ...baseItem,
          supplierCodes: [
            {
              ...baseItem.supplierCodes[0],
              supplierDisplayName: null,
              supplierIdentification: null,
              packagingLevelId: null,
            },
          ],
        }}
        onManageSupplierPresentations={() => undefined}
      />,
    );

    expect(screen.getByText("Proveedor sin nombre")).toBeTruthy();
    expect(screen.getByText("Sin presentación asociada")).toBeTruthy();
  });
});
