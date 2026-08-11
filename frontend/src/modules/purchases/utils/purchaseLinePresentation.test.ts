import { describe, expect, it } from "vitest";
import type { PurchaseItemContextDto } from "../api/purchaseService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import { buildPurchaseLinePresentation } from "./purchaseLinePresentation";

const context: PurchaseItemContextDto = {
  itemId: "item-1",
  sku: "FANTA",
  shortName: "Fanta",
  description: "Fanta naranja",
  baseUomCode: "UNIT",
  tracksStock: true,
  supplierCode: "3172",
  packagingLevels: [
    {
      id: "paca-12",
      name: "PACA",
      baseQuantity: 12,
      uomCode: "PACA",
      isBaseUnit: false,
      isPurchaseDefault: true,
    },
  ],
  currentStock: 0,
  availableStock: 0,
  reservedStock: 0,
  averageCost: 0,
  lastPurchaseCost: 0,
  pvp: 0,
  previousPrice: 0,
  maxDiscountPercent: 0,
  purchaseVatCode: "2",
  vatPercent: 15,
  exciseTaxCode: null,
  icePercent: 0,
  hasVat: true,
  hasIce: false,
  costMargin: 0,
  costMarginPercent: 0,
};

function line(overrides: Partial<PurchaseLineFormValues>): PurchaseLineFormValues {
  return {
    _key: 1,
    itemId: "item-1",
    description: "FANTA",
    quantity: 2,
    unitPrice: 9.29,
    vatCode: "2",
    discountPct: 0,
    purchaseReceptionLineId: "line-1",
    xmlSupplierCode: "3172",
    context,
    ...overrides,
  } as PurchaseLineFormValues;
}

describe("buildPurchaseLinePresentation — supplier presentation UX", () => {
  it("muestra ítem vinculado sin presentación cuando la línea XML no tiene packagingLevelId", () => {
    const vm = buildPurchaseLinePresentation(line({ packagingLevelId: undefined }));

    expect(vm.status.label).toBe("Ítem vinculado sin presentación");
    expect(vm.status.tone).toBe("warning");
    expect(vm.inventory.hasPresentation).toBe(false);
  });

  it("muestra ítem + presentación y conversión base cuando hay PackagingLevelId", () => {
    const vm = buildPurchaseLinePresentation(
      line({
        packagingLevelId: "paca-12",
        uomCode: "PACA",
        baseUomCode: "UNIT",
        conversionFactor: 12,
        quantityInBaseUom: 24,
      }),
    );

    expect(vm.status.label).toBe("Ítem + PACA");
    expect(vm.inventory.hasPresentation).toBe(true);
    expect(vm.inventory.conversionDetail).toBe("2.0000 PACA -> 24.0000 UNIT");
  });

  it("muestra presentación rehidratada aunque el contexto de bodega aún no haya cargado", () => {
    const vm = buildPurchaseLinePresentation(
      line({
        context: undefined,
        packagingLevelId: "paca-12",
        uomCode: "PACA",
        baseUomCode: "UNIT",
        conversionFactor: 12,
        quantityInBaseUom: 24,
      }),
    );

    expect(vm.status.label).toBe("Ítem + PACA x 12.0000");
    expect(vm.inventory.hasPresentation).toBe(true);
    expect(vm.inventory.conversionDetail).toBe("2.0000 PACA -> 24.0000 UNIT");
  });

  it("muestra alerta si el costo base cambia de forma extrema contra el último costo", () => {
    const vm = buildPurchaseLinePresentation(
      line({
        unitPrice: 120,
        packagingLevelId: "paca-12",
        uomCode: "PACA",
        baseUomCode: "UNIT",
        conversionFactor: 12,
        quantityInBaseUom: 24,
        context: {
          ...context,
          lastPurchaseCost: 5,
          averageCost: 4,
        },
      }),
    );

    expect(vm.commercial.costs.showDeviationAlert).toBe(true);
    expect(vm.commercial.costs.deviationLabel).toContain("Costo base");
    expect(vm.commercial.costs.deviationLabel).toContain(
      "Revise presentación/factor",
    );
  });
});
