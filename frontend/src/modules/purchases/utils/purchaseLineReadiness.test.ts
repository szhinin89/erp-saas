import { describe, expect, it } from "vitest";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import {
  getPurchaseLineBlockingReasons,
  getPurchaseLinePrimaryAction,
  getPurchaseLineReadiness,
  isPurchaseLineReady,
} from "./purchaseLineReadiness";

function line(overrides: Partial<PurchaseLineFormValues> = {}): PurchaseLineFormValues {
  return {
    _key: 1,
    description: "Producto XML",
    quantity: 1,
    unitPrice: 1,
    vatCode: "2",
    discountPct: 0,
    purchaseReceptionLineId: "rx-line-1",
    xmlSupplierCode: "SUP-1",
    warehouseId: "wh-1",
    context: {
      itemId: "item-1",
      sku: "SKU-1",
      shortName: "Item",
      description: "Item",
      baseUomCode: "UNIT",
      tracksStock: true,
      packagingLevels: [],
      supplierCode: "SUP-1",
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
    },
    ...overrides,
  };
}

describe("purchaseLineReadiness", () => {
  it("prioriza seleccionar o crear item cuando una linea XML no tiene itemId", () => {
    const readiness = getPurchaseLineReadiness(line({ itemId: undefined }));

    expect(readiness.status).toBe("MISSING_ITEM");
    expect(readiness.label).toBe("Sin producto vinculado");
    expect(readiness.primaryAction).toBe("SELECT_ITEM");
    expect(readiness.secondaryAction).toBe("CREATE_ITEM");
    expect(readiness.blocking).toBe(true);
  });

  it("detecta codigo proveedor asociado a otro item como bloqueo prioritario", () => {
    const readiness = getPurchaseLineReadiness(
      line({
        itemId: "item-1",
        _readinessIssue: "SUPPLIER_CODE_CONFLICT",
        packagingLevelId: undefined,
      }),
    );

    expect(readiness.status).toBe("SUPPLIER_CODE_CONFLICT");
    expect(readiness.primaryAction).toBe("SELECT_ITEM");
  });

  it("detecta presentacion faltante cuando la linea XML tiene item con stock", () => {
    const readiness = getPurchaseLineReadiness(
      line({ itemId: "item-1", packagingLevelId: undefined }),
    );

    expect(readiness.status).toBe("MISSING_PRESENTATION");
    expect(readiness.primaryAction).toBe("SELECT_PRESENTATION");
    expect(readiness.secondaryAction).toBe("SAVE_PRESENTATION");
  });

  it("detecta bodega faltante despues de item y presentacion", () => {
    const readiness = getPurchaseLineReadiness(
      line({
        itemId: "item-1",
        packagingLevelId: "box-1",
        warehouseId: undefined,
      }),
    );

    expect(readiness.status).toBe("MISSING_WAREHOUSE");
    expect(
      getPurchaseLinePrimaryAction(
        line({
          itemId: "item-1",
          packagingLevelId: "box-1",
          warehouseId: undefined,
        }),
      ),
    ).toBe("SELECT_WAREHOUSE");
  });

  it("detecta impuesto no reconocido cuando el catalogo cargado no contiene el codigo", () => {
    const readiness = getPurchaseLineReadiness(
      line({ itemId: "item-1", packagingLevelId: "box-1", vatCode: "999" }),
      { vatRates: { "2": 15 } },
    );

    expect(readiness.status).toBe("INVALID_TAX");
  });

  it("PURCHASE-LINE-PACKAGING-SUSPICIOUS-COST-GUARD-01 — bloquea presentacion con margen extremo (< -50%)", () => {
    const suspiciousLine = line({
      itemId: "item-1",
      packagingLevelId: "unidad-x1",
      quantity: 8,
      unitPrice: 5.109,
      context: {
        ...line().context!,
        pvp: 1.08,
        packagingLevels: [
          {
            id: "unidad-x1",
            name: "UNIDAD X1",
            baseQuantity: 1,
            uomCode: "UNIDAD",
            isBaseUnit: true,
            isPurchaseDefault: false,
          },
        ],
      },
    });

    const readiness = getPurchaseLineReadiness(suspiciousLine);

    expect(readiness.status).toBe("SUSPICIOUS_PACKAGING_COST");
    expect(readiness.blocking).toBe(true);
    expect(readiness.detail).toContain("1.08");
    expect(getPurchaseLineBlockingReasons([suspiciousLine])).toHaveLength(1);
  });

  it("PURCHASE-LINE-MISSING-SALE-PRICE-WARNING-01 — avisa (no bloquea) cuando falta precio de venta en item inventariable", () => {
    const readyLine = line({
      itemId: "item-1",
      packagingLevelId: "box-1",
      uomCode: "BOX",
      baseUomCode: "UNIT",
      conversionFactor: 12,
      quantityInBaseUom: 12,
      context: { ...line().context!, pvp: 0 },
    });

    const readiness = getPurchaseLineReadiness(readyLine, { vatRates: { "2": 15 } });

    expect(readiness.status).toBe("READY");
    expect(readiness.blocking).toBe(false);
    expect(readiness.warning?.status).toBe("MISSING_SALE_PRICE_FOR_MARGIN");
    expect(readiness.warning?.detail).toContain("no tiene precio de venta configurado");
    expect(getPurchaseLineBlockingReasons([readyLine], { vatRates: { "2": 15 } }))
      .toHaveLength(0);
  });

  it("no genera warning de precio de venta cuando el margen extremo ya bloquea (SUSPICIOUS_PACKAGING_COST)", () => {
    const suspiciousLine = line({
      itemId: "item-1",
      packagingLevelId: "unidad-x1",
      quantity: 8,
      unitPrice: 5.109,
      context: {
        ...line().context!,
        pvp: 1.08,
        packagingLevels: [
          {
            id: "unidad-x1",
            name: "UNIDAD X1",
            baseQuantity: 1,
            uomCode: "UNIDAD",
            isBaseUnit: true,
            isPurchaseDefault: false,
          },
        ],
      },
    });

    const readiness = getPurchaseLineReadiness(suspiciousLine);

    expect(readiness.status).toBe("SUSPICIOUS_PACKAGING_COST");
    expect(readiness.blocking).toBe(true);
    expect(readiness.warning).toBeUndefined();
  });

  it("no genera warning de precio de venta con precio de venta correcto (margen normal)", () => {
    const readyLine = line({
      itemId: "item-1",
      packagingLevelId: "box-1",
      uomCode: "BOX",
      baseUomCode: "UNIT",
      conversionFactor: 12,
      quantityInBaseUom: 12,
      context: { ...line().context!, pvp: 100 },
    });

    const readiness = getPurchaseLineReadiness(readyLine, { vatRates: { "2": 15 } });

    expect(readiness.warning).toBeUndefined();
  });

  it("no genera warning de precio de venta en servicio/no inventariable", () => {
    const serviceLine = line({
      itemId: "item-1",
      packagingLevelId: "box-1",
      uomCode: "BOX",
      baseUomCode: "UNIT",
      conversionFactor: 12,
      quantityInBaseUom: 12,
      context: { ...line().context!, tracksStock: false, pvp: 0 },
    });

    const readiness = getPurchaseLineReadiness(serviceLine, { vatRates: { "2": 15 } });

    expect(readiness.warning).toBeUndefined();
  });

  it("no genera warning de precio de venta sin producto vinculado (itemId ausente)", () => {
    const noItemLine = line({ itemId: undefined });

    const readiness = getPurchaseLineReadiness(noItemLine);

    expect(readiness.warning).toBeUndefined();
  });

  it("marca lista una linea con item, presentacion, bodega e impuesto valido", () => {
    const readyLine = line({
      itemId: "item-1",
      packagingLevelId: "box-1",
      uomCode: "BOX",
      baseUomCode: "UNIT",
      conversionFactor: 12,
      quantityInBaseUom: 12,
    });

    expect(isPurchaseLineReady(readyLine, { vatRates: { "2": 15 } })).toBe(true);
    expect(getPurchaseLineReadiness(readyLine, { vatRates: { "2": 15 } }).status).toBe(
      "READY",
    );
    expect(getPurchaseLineBlockingReasons([readyLine], { vatRates: { "2": 15 } }))
      .toHaveLength(0);
  });
});
