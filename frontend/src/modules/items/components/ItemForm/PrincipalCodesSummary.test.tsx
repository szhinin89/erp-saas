// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import {
  BarcodePrincipalManager,
  SupplierCodesPrincipalManager,
} from "./PrincipalCodesSummary";
import type { ItemDetailDto } from "../../../../types/items";

const itemServiceMock = vi.hoisted(() => ({
  addBarcode: vi.fn().mockResolvedValue({}),
  disableBarcode: vi.fn().mockResolvedValue(true),
}));

vi.mock("../../api/itemService", () => ({
  itemService: itemServiceMock,
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const t = (_key: string, fallback?: string) => fallback ?? _key;
const oldBarcodeCta = [
  "Gestionar códigos de barras",
  "en detalle del ítem",
].join(" ");
const oldSupplierCta = [
  "Completar presentación",
  "en Inventario y presentaciones",
].join(" ");

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
      supplierId: "a8a07f8a-e16d-4a8d-a111-c16559616566",
      supplierDisplayName: "Cerveceria Nacional",
      supplierIdentification: "0999999999001",
      packagingLevelId: "paca-12",
      code: "8431",
      isPrimary: true,
      isActive: true,
    },
  ],
};

describe("Principal code managers", () => {
  it("renderiza códigos de barras como sección accionable sin CTA al detalle", async () => {
    const onRefresh = vi.fn();
    render(
      <BarcodePrincipalManager
        t={t}
        item={baseItem}
        barcodeTypeOptions={[{ code: "EAN13", name: "EAN 13" }]}
        onRefresh={onRefresh}
      />,
    );

    expect(screen.getByRole("columnheader", { name: "Código" })).toBeTruthy();
    expect(screen.getByRole("columnheader", { name: "Tipo" })).toBeTruthy();
    expect(screen.getByText("7501234567890")).toBeTruthy();
    expect(
      screen.getByRole("button", { name: "Agregar código de barras" }),
    ).toBeTruthy();
    expect(
      screen.queryByRole("button", {
        name: oldBarcodeCta,
      }),
    ).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Quitar" }));

    await waitFor(() => {
      expect(itemServiceMock.disableBarcode).toHaveBeenCalledWith(
        "item-1",
        "variant-1",
        "barcode-1",
      );
      expect(onRefresh).toHaveBeenCalled();
    });
  });

  it("renderiza proveedor, RUC, código y presentación sin mostrar GUID", () => {
    render(
      <SupplierCodesPrincipalManager
        t={t}
        item={baseItem}
        onUpdatePresentation={vi.fn()}
      />,
    );

    expect(screen.getByText("Cerveceria Nacional")).toBeTruthy();
    expect(screen.getByText("RUC: 0999999999001")).toBeTruthy();
    expect(screen.getByText("8431")).toBeTruthy();
    expect(screen.getAllByText("PACA X12 x 12 UN").length).toBeGreaterThan(0);
    expect(
      screen.queryByText("a8a07f8a-e16d-4a8d-a111-c16559616566"),
    ).toBeNull();
    expect(
      screen.queryByRole("button", {
        name: oldSupplierCta,
      }),
    ).toBeNull();
  });

  it("permite asociar presentación desde Principal", async () => {
    const onUpdatePresentation = vi.fn().mockResolvedValue(undefined);
    render(
      <SupplierCodesPrincipalManager
        t={t}
        item={{
          ...baseItem,
          supplierCodes: [
            {
              ...baseItem.supplierCodes[0],
              packagingLevelId: null,
            },
          ],
        }}
        onUpdatePresentation={onUpdatePresentation}
      />,
    );

    fireEvent.change(
      screen.getByLabelText("Presentación del código proveedor"),
      { target: { value: "paca-12" } },
    );

    await waitFor(() => {
      expect(onUpdatePresentation).toHaveBeenCalledWith(
        "a8a07f8a-e16d-4a8d-a111-c16559616566",
        "8431",
        "paca-12",
      );
    });
  });

  it("usa fallback controlado cuando el proveedor no tiene nombre", () => {
    render(
      <SupplierCodesPrincipalManager
        t={t}
        item={{
          ...baseItem,
          packagingLevels: [],
          supplierCodes: [
            {
              ...baseItem.supplierCodes[0],
              supplierDisplayName: null,
              supplierIdentification: null,
              packagingLevelId: null,
            },
          ],
        }}
        onUpdatePresentation={vi.fn()}
      />,
    );

    expect(screen.getByText("Proveedor sin nombre")).toBeTruthy();
    expect(
      screen.getAllByText("Presentación pendiente").length,
    ).toBeGreaterThan(0);
  });
});
