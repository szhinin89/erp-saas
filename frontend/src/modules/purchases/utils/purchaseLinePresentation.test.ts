import { describe, expect, it } from "vitest";
import type { PurchaseItemContextDto } from "../api/purchaseService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import {
  buildMissingSalePriceForMarginWarning,
  buildPurchaseLinePresentation,
  buildSuspiciousPackagingCostWarning,
  computeUnitPriceFromBaseUnitCost,
  headerInputKey,
} from "./purchaseLinePresentation";

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
    // Redacción legible para el usuario (PURCHASE-UI-P4): nunca expone el
    // código técnico crudo de UOM ("UNIT"/"04"/"19"), usa la palabra
    // genérica "unidades" en su lugar.
    expect(vm.inventory.equivalenceDetail).toBe("1 PACA = 12.0000 unidades");
    expect(vm.inventory.baseQuantity).toBe("24.0000 unidades");
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

  it("PURCHASE-PRESENTATION-MARGIN-AUDIT-01 — el margen usa el costo por unidad base, no el costo por presentación", () => {
    // Caso reportado: CLUB PLATINO LATA 355CC NRB X6 TERMO — costo por SIXPACK X6 = 5.9125,
    // factor 6, PVP por unidad base = 1.08. Antes del fix, calcMarginPercent(unitPrice, pvp)
    // comparaba 5.9125 (costo por presentación) contra 1.08 (precio por unidad base) y daba
    // -447.45%. baseUnitCost = (8 * 5.9125) / 48 ≈ 0.9854 → margen esperado ≈ 8.76%.
    const vm = buildPurchaseLinePresentation(
      line({
        quantity: 8,
        unitPrice: 5.9125,
        packagingLevelId: "sixpack-6",
        uomCode: "SIXPACK",
        baseUomCode: "UNIT",
        conversionFactor: 6,
        quantityInBaseUom: 48,
        context: {
          ...context,
          packagingLevels: [
            {
              id: "sixpack-6",
              name: "SIXPACK X6",
              baseQuantity: 6,
              uomCode: "SIXPACK",
              isBaseUnit: false,
              isPurchaseDefault: true,
            },
          ],
          pvp: 1.08,
        },
      }),
    );

    expect(vm.inventory.baseUnitCost).toBe("$0.9854");
    expect(vm.commercial.profitability.marginPctValue).toBeCloseTo(8.76, 1);
    expect(vm.commercial.profitability.marginPctValue).toBeGreaterThan(0);
  });

  describe("PURCHASE-LINE-HEADER-INVENTORY-MODE-01 — modo de cabecera", () => {
    it("sin presentación vinculada: hasPresentation es false (cabecera mantiene datos XML/factura)", () => {
      const vm = buildPurchaseLinePresentation(line({ packagingLevelId: undefined }));

      expect(vm.inventory.hasPresentation).toBe(false);
    });

    it("con presentación vinculada: hasPresentation es true y expone cantidad/factor base numéricos para la cabecera", () => {
      const vm = buildPurchaseLinePresentation(
        line({
          quantity: 8,
          packagingLevelId: "sixpack-6",
          uomCode: "SIXPACK",
          baseUomCode: "UNIT",
          conversionFactor: 6,
          quantityInBaseUom: 48,
          context: {
            ...context,
            packagingLevels: [
              {
                id: "sixpack-6",
                name: "SIXPACK X6",
                baseQuantity: 6,
                uomCode: "SIXPACK",
                isBaseUnit: false,
                isPurchaseDefault: true,
              },
            ],
          },
        }),
      );

      expect(vm.inventory.hasPresentation).toBe(true);
      expect(vm.inventory.baseQuantityValue).toBe(48);
      expect(vm.inventory.conversionFactorValue).toBe(6);
    });

    it("caso exacto — 8 SIXPACK / 48 unidades / desc 13.59% / PVP 1.08 → costo real ≈ 0.8515 y margen ≈ 21.16%", () => {
      // Subtotal bruto: 8 * 5.9125 = 47.30. Descuento 13.59% ≈ 6.43. Neto ≈ 40.87.
      // Costo real unitario base: 40.87 / 48 ≈ 0.8515. Margen: (1.08 - 0.8515) / 1.08 * 100 ≈ 21.16%.
      const vm = buildPurchaseLinePresentation(
        line({
          quantity: 8,
          unitPrice: 5.9125,
          discountPct: (6.43 / 47.3) * 100,
          packagingLevelId: "sixpack-6",
          uomCode: "SIXPACK",
          baseUomCode: "UNIT",
          conversionFactor: 6,
          quantityInBaseUom: 48,
          context: {
            ...context,
            packagingLevels: [
              {
                id: "sixpack-6",
                name: "SIXPACK X6",
                baseQuantity: 6,
                uomCode: "SIXPACK",
                isBaseUnit: false,
                isPurchaseDefault: true,
              },
            ],
            pvp: 1.08,
          },
        }),
      );

      expect(vm.inventory.baseUnitCost).toBe("$0.8515");
      expect(vm.commercial.profitability.marginPctValue).toBeCloseTo(21.16, 1);
    });

    it("con flete/gasto asignado: el costo real unitario suma freightAllocated y otherCostsAllocated antes de dividir", () => {
      // Mismo caso base sin descuento: neto = 47.30. + flete 4.80 + otros 0.86 = 52.96. / 48 ≈ 1.1033.
      const vm = buildPurchaseLinePresentation(
        line({
          quantity: 8,
          unitPrice: 5.9125,
          discountPct: 0,
          freightAllocated: 4.8,
          otherCostsAllocated: 0.86,
          packagingLevelId: "sixpack-6",
          uomCode: "SIXPACK",
          baseUomCode: "UNIT",
          conversionFactor: 6,
          quantityInBaseUom: 48,
          context: {
            ...context,
            packagingLevels: [
              {
                id: "sixpack-6",
                name: "SIXPACK X6",
                baseQuantity: 6,
                uomCode: "SIXPACK",
                isBaseUnit: false,
                isPurchaseDefault: true,
              },
            ],
          },
        }),
      );

      const expected = (47.3 + 4.8 + 0.86) / 48;
      expect(vm.inventory.baseUnitCost).toBe(`$${expected.toFixed(4)}`);
    });
  });

  describe("PURCHASE-LINE-HEADER-COST-BASE-MODE-01 — Costo de cabecera no debe mezclar cantidad base con costo de presentación", () => {
    it("sin presentación: hasPresentation es false — PurchaseLineCard debe usar l.unitPrice como Costo de cabecera, no baseUnitCostValue", () => {
      const vm = buildPurchaseLinePresentation(
        line({ packagingLevelId: undefined, quantity: 8, unitPrice: 5.9125 }),
      );

      expect(vm.inventory.hasPresentation).toBe(false);
    });

    it("con presentación: expone baseUnitCostValue numérico distinto de unitPrice, para que la cabecera muestre costo real, no costo de presentación", () => {
      const vm = buildPurchaseLinePresentation(
        line({
          quantity: 8,
          unitPrice: 5.9125,
          discountPct: 0,
          packagingLevelId: "sixpack-6",
          uomCode: "SIXPACK",
          baseUomCode: "UNIT",
          conversionFactor: 6,
          quantityInBaseUom: 48,
          context: {
            ...context,
            packagingLevels: [
              {
                id: "sixpack-6",
                name: "SIXPACK X6",
                baseQuantity: 6,
                uomCode: "SIXPACK",
                isBaseUnit: false,
                isPurchaseDefault: true,
              },
            ],
          },
        }),
      );

      expect(vm.inventory.hasPresentation).toBe(true);
      expect(vm.inventory.baseQuantityValue).toBe(48);
      // Sin descuento/flete: baseUnitCostValue = (8*5.9125)/48, muy distinto de unitPrice (5.9125) —
      // confirma que la cabecera nunca debe combinar Cantidad=48 con Costo=5.9125.
      expect(vm.inventory.baseUnitCostValue).toBeCloseTo((8 * 5.9125) / 48, 4);
      expect(vm.inventory.baseUnitCostValue).not.toBeCloseTo(5.9125, 1);
    });

    it("caso exacto — Cantidad 48, Costo 0.8515, Margen 21.16%, Total línea 47.00", () => {
      const discountPct = (6.43 / 47.3) * 100;
      const vm = buildPurchaseLinePresentation(
        line({
          quantity: 8,
          unitPrice: 5.9125,
          discountPct,
          packagingLevelId: "sixpack-6",
          uomCode: "SIXPACK",
          baseUomCode: "UNIT",
          conversionFactor: 6,
          quantityInBaseUom: 48,
          context: {
            ...context,
            packagingLevels: [
              {
                id: "sixpack-6",
                name: "SIXPACK X6",
                baseQuantity: 6,
                uomCode: "SIXPACK",
                isBaseUnit: false,
                isPurchaseDefault: true,
              },
            ],
            pvp: 1.08,
          },
        }),
      );

      expect(vm.inventory.baseQuantityValue).toBe(48);
      expect(vm.inventory.baseUnitCostValue).toBeCloseTo(0.8515, 3);
      expect(vm.inventory.baseUnitCost).toBe("$0.8515");
      expect(vm.commercial.profitability.marginPctValue).toBeCloseTo(21.16, 1);
      // Total línea 47.00 se compone fuera del view-model (netLine + IVA/ICE en PurchaseLineCard);
      // aquí solo se confirma que el costo real usado para el margen es consistente con esa cifra:
      // 48 * 0.8515 ≈ 40.87 (subtotal neto) + 6.13 IVA ≈ 47.00.
      expect(48 * (vm.inventory.baseUnitCostValue ?? 0)).toBeCloseTo(40.87, 1);
    });

    it("con flete/otros gastos: baseUnitCostValue (costo de cabecera) los incluye", () => {
      const vm = buildPurchaseLinePresentation(
        line({
          quantity: 8,
          unitPrice: 5.9125,
          discountPct: 0,
          freightAllocated: 4.8,
          otherCostsAllocated: 0.86,
          packagingLevelId: "sixpack-6",
          uomCode: "SIXPACK",
          baseUomCode: "UNIT",
          conversionFactor: 6,
          quantityInBaseUom: 48,
          context: {
            ...context,
            packagingLevels: [
              {
                id: "sixpack-6",
                name: "SIXPACK X6",
                baseQuantity: 6,
                uomCode: "SIXPACK",
                isBaseUnit: false,
                isPurchaseDefault: true,
              },
            ],
          },
        }),
      );

      const expected = (47.3 + 4.8 + 0.86) / 48;
      expect(vm.inventory.baseUnitCostValue).toBeCloseTo(expected, 4);
    });
  });

  describe("PURCHASE-LINE-HEADER-CLEAN-COST-01 — Desc sale de cabecera, pero discountPct sigue vivo en cálculo y en XML", () => {
    it("discountPct sigue afectando baseUnitCostValue/margen aunque ya no tenga celda propia en cabecera", () => {
      const withoutDiscount = buildPurchaseLinePresentation(
        line({
          quantity: 8,
          unitPrice: 5.9125,
          discountPct: 0,
          packagingLevelId: "sixpack-6",
          uomCode: "SIXPACK",
          baseUomCode: "UNIT",
          conversionFactor: 6,
          quantityInBaseUom: 48,
          context: {
            ...context,
            packagingLevels: [
              {
                id: "sixpack-6",
                name: "SIXPACK X6",
                baseQuantity: 6,
                uomCode: "SIXPACK",
                isBaseUnit: false,
                isPurchaseDefault: true,
              },
            ],
          },
        }),
      );
      const withDiscount = buildPurchaseLinePresentation(
        line({
          quantity: 8,
          unitPrice: 5.9125,
          discountPct: (6.43 / 47.3) * 100,
          packagingLevelId: "sixpack-6",
          uomCode: "SIXPACK",
          baseUomCode: "UNIT",
          conversionFactor: 6,
          quantityInBaseUom: 48,
          context: {
            ...context,
            packagingLevels: [
              {
                id: "sixpack-6",
                name: "SIXPACK X6",
                baseQuantity: 6,
                uomCode: "SIXPACK",
                isBaseUnit: false,
                isPurchaseDefault: true,
              },
            ],
          },
        }),
      );

      expect(withoutDiscount.inventory.baseUnitCostValue).toBeCloseTo(
        (8 * 5.9125) / 48,
        4,
      );
      expect(withDiscount.inventory.baseUnitCostValue).toBeCloseTo(0.8515, 3);
      expect(withDiscount.inventory.baseUnitCostValue).toBeLessThan(
        withoutDiscount.inventory.baseUnitCostValue,
      );
    });

    it("el descuento documental (xmlDiscount) sigue disponible para 'Producto recibido (XML)' independiente de discountPct", () => {
      const vm = buildPurchaseLinePresentation(
        line({
          quantity: 8,
          unitPrice: 5.9125,
          discountPct: (6.43 / 47.3) * 100,
          xmlDiscount: 6.43,
          packagingLevelId: "sixpack-6",
          uomCode: "SIXPACK",
          baseUomCode: "UNIT",
          conversionFactor: 6,
          quantityInBaseUom: 48,
          context: {
            ...context,
            packagingLevels: [
              {
                id: "sixpack-6",
                name: "SIXPACK X6",
                baseQuantity: 6,
                uomCode: "SIXPACK",
                isBaseUnit: false,
                isPurchaseDefault: true,
              },
            ],
          },
        }),
      );

      // "Producto recibido (XML)" sigue mostrando el descuento documental — no se tocó vm.xml.*.
      expect(vm.xml.discount).toBe("$6.43");
    });
  });

  describe("IRBPNR — caso real Arca Continental, factura 029-001-001293714 (dos líneas del mismo XML)", () => {
    // Nota: un test anterior de esta sesión etiquetaba estos valores (Base 12.98, IVA 1.61, ICE 0,
    // IRBPNR 0.24, Total 14.83) como "INCA-KOLA" — son en realidad de la línea SPRITE HARMONY 1350
    // PET(12) (primera línea del XML real, ver PurchaseXmlDraftParserTests.cs). INCA-KOLA (segunda
    // línea, codigoPrincipal 12469) trae Base 12.98 / IVA 1.95 / IRBPNR 0.72 / Total 15.65 — corregido
    // abajo, con un caso separado para cada línea para no perder cobertura de ninguna de las dos.
    it("SPRITE HARMONY 1350 PET(12) — expone taxableBase/iceValue/irbpnrValue/hasIrbpnr con los montos exactos del XML", () => {
      const vm = buildPurchaseLinePresentation(
        line({
          quantity: 1,
          unitPrice: 10.7213,
          xmlTaxableBase: 12.98,
          xmlTaxValue: 1.61,
          xmlIceAmount: 0,
          xmlIrbpnrAmount: 0.24,
          xmlTotalLine: 14.83,
        }),
      );

      expect(vm.xml.taxableBase).toBe("$12.98");
      expect(vm.xml.taxValue).toBe("$1.61");
      expect(vm.xml.iceValue).toBe("$0.00");
      expect(vm.xml.irbpnrValue).toBe("$0.24");
      expect(vm.xml.totalLine).toBe("$14.83");
      expect(vm.xml.hasIrbpnr).toBe(true);
    });

    it("INCA-KOLA ORGL 900ML PET NR 12 — caso reportado por el usuario, expone IRBPNR=0.72 con los montos reales del XML", () => {
      const vm = buildPurchaseLinePresentation(
        line({
          quantity: 3,
          unitPrice: 4.32817,
          xmlTaxableBase: 12.98,
          xmlTaxValue: 1.95,
          xmlIceAmount: 0,
          xmlIrbpnrAmount: 0.72,
          xmlTotalLine: 15.65,
        }),
      );

      expect(vm.xml.taxableBase).toBe("$12.98");
      expect(vm.xml.taxValue).toBe("$1.95");
      expect(vm.xml.iceValue).toBe("$0.00");
      expect(vm.xml.irbpnrValue).toBe("$0.72");
      expect(vm.xml.totalLine).toBe("$15.65");
      expect(vm.xml.hasIrbpnr).toBe(true);
    });

    it("hasIrbpnr es false cuando xmlIrbpnrAmount es 0", () => {
      const vm = buildPurchaseLinePresentation(
        line({ xmlIrbpnrAmount: 0 }),
      );

      expect(vm.xml.hasIrbpnr).toBe(false);
    });

    it("hasIrbpnr es false cuando xmlIrbpnrAmount es undefined", () => {
      const vm = buildPurchaseLinePresentation(
        line({ xmlIrbpnrAmount: undefined }),
      );

      expect(vm.xml.hasIrbpnr).toBe(false);
    });
  });

  describe("PURCHASE-LINE-HEADER-PACKAGING-CHANGE-SYNC-01 — cabecera sincronizada al cambiar presentación", () => {
    // Caso real reportado: CLUB PLATINO LATA 355CC NRB X6 TERMO. quantity=8 (cantidad XML),
    // unitPrice=5.109 fijo — solo cambia el factor de conversión (SIXPACK X6 → 6, UNIDAD X1 → 1).
    const clubPlatinoContext: PurchaseItemContextDto = {
      ...context,
      packagingLevels: [
        {
          id: "sixpack-x6",
          name: "SIXPACK X6",
          baseQuantity: 6,
          uomCode: "SIXPACK",
          isBaseUnit: false,
          isPurchaseDefault: true,
        },
        {
          id: "unidad-x1",
          name: "UNIDAD X1",
          baseQuantity: 1,
          uomCode: "UNIDAD",
          isBaseUnit: true,
          isPurchaseDefault: false,
        },
      ],
      pvp: 1.08,
    };

    function clubPlatinoLine(packagingLevelId: string) {
      return line({
        quantity: 8,
        unitPrice: 5.109,
        discountPct: 0,
        packagingLevelId,
        context: clubPlatinoContext,
      });
    }

    it("SIXPACK X6 (factor 6): cantidad base 48, costo base 0.8515, margen ~21.16%", () => {
      const vm = buildPurchaseLinePresentation(clubPlatinoLine("sixpack-x6"));

      expect(vm.inventory.baseQuantityValue).toBe(48);
      expect(vm.inventory.baseUnitCostValue).toBeCloseTo(0.8515, 4);
      expect(vm.commercial.profitability.marginPctValue).toBeCloseTo(21.16, 1);
    });

    it("UNIDAD X1 (factor 1): cantidad base 8, costo base 5.1090, margen ~-373.05%", () => {
      const vm = buildPurchaseLinePresentation(clubPlatinoLine("unidad-x1"));

      expect(vm.inventory.baseQuantityValue).toBe(8);
      expect(vm.inventory.baseUnitCostValue).toBeCloseTo(5.109, 4);
      expect(vm.commercial.profitability.marginPctValue).toBeCloseTo(-373.05, 0);
    });

    it("volver a SIXPACK X6 recupera exactamente 48 / 0.8515 (no queda pegado en UNIDAD X1)", () => {
      const vm = buildPurchaseLinePresentation(clubPlatinoLine("sixpack-x6"));

      expect(vm.inventory.baseQuantityValue).toBe(48);
      expect(vm.inventory.baseUnitCostValue).toBeCloseTo(0.8515, 4);
    });

    it("headerInputKey cambia entre SIXPACK X6 y UNIDAD X1 aunque ambas tengan hasPresentation=true", () => {
      const sixpack = buildPurchaseLinePresentation(clubPlatinoLine("sixpack-x6"));
      const unidad = buildPurchaseLinePresentation(clubPlatinoLine("unidad-x1"));

      // Ambas presentaciones son reales (packagingLevelId siempre presente) — hasPresentation es
      // true en las dos, por lo que la key NO puede depender solo de ese booleano (esa era la causa
      // exacta del bug: el input no controlado nunca remonteaba al cambiar de presentación).
      expect(sixpack.inventory.hasPresentation).toBe(true);
      expect(unidad.inventory.hasPresentation).toBe(true);

      const qtyKeySixpack = headerInputKey("qty", sixpack, "sixpack-x6");
      const qtyKeyUnidad = headerInputKey("qty", unidad, "unidad-x1");
      const costKeySixpack = headerInputKey("cost", sixpack, "sixpack-x6");
      const costKeyUnidad = headerInputKey("cost", unidad, "unidad-x1");

      expect(qtyKeySixpack).not.toBe(qtyKeyUnidad);
      expect(costKeySixpack).not.toBe(costKeyUnidad);
    });

    it("headerInputKey es estable para la MISMA presentación (no fuerza remount en cada render)", () => {
      const vm1 = buildPurchaseLinePresentation(clubPlatinoLine("sixpack-x6"));
      const vm2 = buildPurchaseLinePresentation(clubPlatinoLine("sixpack-x6"));

      expect(headerInputKey("qty", vm1, "sixpack-x6")).toBe(
        headerInputKey("qty", vm2, "sixpack-x6"),
      );
      expect(headerInputKey("cost", vm1, "sixpack-x6")).toBe(
        headerInputKey("cost", vm2, "sixpack-x6"),
      );
    });

    it("headerInputKey distingue modo sin presentación (invoice) del modo con presentación (base)", () => {
      const withPackaging = buildPurchaseLinePresentation(clubPlatinoLine("sixpack-x6"));
      const withoutPackaging = buildPurchaseLinePresentation(
        line({ packagingLevelId: undefined }),
      );

      expect(
        headerInputKey("qty", withPackaging, "sixpack-x6"),
      ).not.toBe(headerInputKey("qty", withoutPackaging, undefined));
    });

    it("edición manual de cantidad con presentación sigue reversando a quantity/quantityInBaseUom (no rompe con la key nueva)", () => {
      // Mismo cálculo que ya usa PurchasesPage.tsx en el onBlur del input de Cantidad —
      // verifica que la fórmula de reversa sigue intacta, la key nueva no la afecta.
      const vm = buildPurchaseLinePresentation(clubPlatinoLine("unidad-x1"));
      const factor = vm.inventory.conversionFactorValue;
      const entered = 10; // usuario teclea 10 unidades base
      const reversedQuantity = entered / factor || 1;

      expect(factor).toBe(1);
      expect(reversedQuantity).toBe(10);
    });

    it("edición manual de costo con presentación sigue usando computeUnitPriceFromBaseUnitCost (no rompe con la key nueva)", () => {
      const vm = buildPurchaseLinePresentation(clubPlatinoLine("sixpack-x6"));

      const recalculated = computeUnitPriceFromBaseUnitCost({
        newBaseUnitCost: 0.9,
        quantity: 8,
        quantityInBaseUom: vm.inventory.baseQuantityValue,
        discountPct: 0,
        freightAllocated: 0,
        otherCostsAllocated: 0,
      });

      // 0.9 * 48 / 8 = 5.4 — el mismo cálculo que ya prueba
      // PURCHASE-LINE-HEADER-BASE-QTY-COST-EDIT-01, sin ninguna dependencia de la key de remount.
      expect(recalculated).toBeCloseTo(5.4, 6);
    });
  });

  describe("PURCHASE-LINE-PACKAGING-SUSPICIOUS-COST-GUARD-01 — buildSuspiciousPackagingCostWarning", () => {
    // Mismo caso real (CLUB PLATINO LATA 355CC NRB X6 TERMO) que
    // "PURCHASE-LINE-HEADER-PACKAGING-CHANGE-SYNC-01" arriba, redeclarado localmente porque los
    // fixtures de ese describe no son visibles fuera de su propio callback.
    const clubPlatinoContext: PurchaseItemContextDto = {
      ...context,
      packagingLevels: [
        {
          id: "sixpack-x6",
          name: "SIXPACK X6",
          baseQuantity: 6,
          uomCode: "SIXPACK",
          isBaseUnit: false,
          isPurchaseDefault: true,
        },
        {
          id: "unidad-x1",
          name: "UNIDAD X1",
          baseQuantity: 1,
          uomCode: "UNIDAD",
          isBaseUnit: true,
          isPurchaseDefault: false,
        },
      ],
      pvp: 1.08,
    };

    function clubPlatinoLine(packagingLevelId: string) {
      return line({
        quantity: 8,
        unitPrice: 5.109,
        discountPct: 0,
        packagingLevelId,
        context: clubPlatinoContext,
      });
    }

    it("bloquea UNIDAD X1: costo base 5.1090, precio venta 1.08, margen -373.05% (< -50%)", () => {
      const warning = buildSuspiciousPackagingCostWarning(
        clubPlatinoLine("unidad-x1"),
      );

      expect(warning).not.toBeNull();
      expect(warning?.blocking).toBe(true);
      expect(warning?.message).toContain("1.08");
    });

    it("no bloquea SIXPACK X6: costo base 0.8515, precio venta 1.08, margen 21.16%", () => {
      const warning = buildSuspiciousPackagingCostWarning(
        clubPlatinoLine("sixpack-x6"),
      );

      expect(warning).toBeNull();
    });

    it("no bloquea sin precio de venta (pvp 0/null)", () => {
      const warning = buildSuspiciousPackagingCostWarning(
        line({
          quantity: 8,
          unitPrice: 5.109,
          packagingLevelId: "unidad-x1",
          context: {
            ...clubPlatinoContext,
            pvp: 0,
          },
        }),
      );

      expect(warning).toBeNull();
    });

    it("no bloquea servicio/no inventariable (tracksStock false)", () => {
      const warning = buildSuspiciousPackagingCostWarning(
        line({
          quantity: 8,
          unitPrice: 5.109,
          packagingLevelId: "unidad-x1",
          context: {
            ...clubPlatinoContext,
            tracksStock: false,
          },
        }),
      );

      expect(warning).toBeNull();
    });

    it("no bloquea sin producto vinculado (itemId ausente)", () => {
      const warning = buildSuspiciousPackagingCostWarning(
        line({
          itemId: undefined,
          quantity: 8,
          unitPrice: 5.109,
          packagingLevelId: "unidad-x1",
          context: clubPlatinoContext,
        }),
      );

      expect(warning).toBeNull();
    });

    it("no bloquea margen negativo leve (-10%, no extremo)", () => {
      const warning = buildSuspiciousPackagingCostWarning(
        line({
          quantity: 1,
          unitPrice: 1.2,
          packagingLevelId: "unidad-x1",
          context: { ...clubPlatinoContext, pvp: 1.08 },
        }),
      );

      expect(warning).toBeNull();
    });
  });

  describe("PURCHASE-LINE-MISSING-SALE-PRICE-WARNING-01 — buildMissingSalePriceForMarginWarning", () => {
    // Mismo producto real (CLUB PLATINO LATA 355CC NRB X6 TERMO), redeclarado localmente porque
    // los fixtures del describe de SUSPICIOUS-COST-GUARD no son visibles fuera de su callback.
    const noPvpContext: PurchaseItemContextDto = {
      ...context,
      packagingLevels: [
        {
          id: "sixpack-x6",
          name: "SIXPACK X6",
          baseQuantity: 6,
          uomCode: "SIXPACK",
          isBaseUnit: false,
          isPurchaseDefault: true,
        },
        {
          id: "unidad-x1",
          name: "UNIDAD X1",
          baseQuantity: 1,
          uomCode: "UNIDAD",
          isBaseUnit: true,
          isPurchaseDefault: false,
        },
      ],
      pvp: 0,
    };

    it("avisa (no bloquea) cuando el producto inventariable no tiene precio de venta configurado", () => {
      const warning = buildMissingSalePriceForMarginWarning(
        line({
          quantity: 8,
          unitPrice: 5.109,
          packagingLevelId: "unidad-x1",
          context: noPvpContext,
        }),
      );

      expect(warning).not.toBeNull();
      expect(warning?.blocking).toBe(false);
      expect(warning?.message).toContain("no tiene precio de venta configurado");
    });

    it("no avisa cuando SÍ hay precio de venta (aunque el margen sea extremo — eso es SUSPICIOUS_PACKAGING_COST)", () => {
      const warning = buildMissingSalePriceForMarginWarning(
        line({
          quantity: 8,
          unitPrice: 5.109,
          packagingLevelId: "unidad-x1",
          context: { ...noPvpContext, pvp: 1.08 },
        }),
      );

      expect(warning).toBeNull();
    });

    it("no avisa con precio de venta correcto (margen normal)", () => {
      const warning = buildMissingSalePriceForMarginWarning(
        line({
          quantity: 8,
          unitPrice: 0.8515,
          packagingLevelId: "sixpack-x6",
          uomCode: "SIXPACK",
          baseUomCode: "UNIT",
          conversionFactor: 6,
          context: { ...noPvpContext, pvp: 1.08 },
        }),
      );

      expect(warning).toBeNull();
    });

    it("no avisa en servicio/no inventariable sin precio de venta", () => {
      const warning = buildMissingSalePriceForMarginWarning(
        line({
          quantity: 8,
          unitPrice: 5.109,
          packagingLevelId: "unidad-x1",
          context: { ...noPvpContext, tracksStock: false },
        }),
      );

      expect(warning).toBeNull();
    });

    it("no avisa sin producto vinculado (itemId ausente)", () => {
      const warning = buildMissingSalePriceForMarginWarning(
        line({
          itemId: undefined,
          quantity: 8,
          unitPrice: 5.109,
          packagingLevelId: "unidad-x1",
          context: noPvpContext,
        }),
      );

      expect(warning).toBeNull();
    });
  });

  describe("PURCHASE-LINE-HEADER-EDIT-RECALCULATE-TAX-TOTAL-01 — IVA/Total de cabecera recalculan al editar Cantidad/Costo", () => {
    // Caso real: CLUB PLATINO LATA 355CC NRB X6 TERMO. Snapshot XML: cantidad 8, precio 5.9125,
    // descuento $6.43 (≈13.594% sobre 47.30 bruto) → base 40.87, IVA 15% → 6.13, total 47.00.
    // Mismo fixture numérico que "Base imponible de cabecera" en purchaseCalc.test.ts.
    const xmlDiscountPct = (6.43 / 47.3) * 100;
    const clubPlatinoXmlLine = (overrides: Partial<PurchaseLineFormValues> = {}) =>
      line({
        quantity: 8,
        unitPrice: 5.9125,
        discountPct: xmlDiscountPct,
        vatCode: "2",
        context: { ...context, vatPercent: 15, icePercent: 0 },
        xmlQuantity: 8,
        xmlUnitPrice: 5.9125,
        xmlDiscount: 6.43,
        xmlLineSubtotal: 40.87,
        xmlVatPercentage: 15,
        xmlTaxValue: 6.13,
        xmlTotalLine: 47.0,
        xmlTaxableBase: 40.87,
        xmlIceAmount: 0,
        xmlIrbpnrAmount: undefined,
        ...overrides,
      });

    it("recién cargada (sin editar): editableHeader ya coincide con el snapshot XML (misma fórmula, base/IVA/total)", () => {
      const vm = buildPurchaseLinePresentation(clubPlatinoXmlLine());

      expect(vm.editableHeader.taxableBase).toBeCloseTo(40.87, 2);
      expect(vm.editableHeader.vatAmount).toBeCloseTo(6.13, 2);
      expect(vm.editableHeader.total).toBeCloseTo(47.0, 2);
    });

    it("caso real: editar a Cantidad 50 / Costo 2 → cabecera editable muestra Base 100 / IVA 15 / Total 115, NO el snapshot XML", () => {
      const vm = buildPurchaseLinePresentation(
        clubPlatinoXmlLine({ quantity: 50, unitPrice: 2, discountPct: 0 }),
      );

      expect(vm.editableHeader.taxableBase).toBe(100);
      expect(vm.editableHeader.vatAmount).toBe(15);
      expect(vm.editableHeader.iceAmount).toBe(0);
      expect(vm.editableHeader.total).toBe(115);
    });

    it("el bloque 'Producto recibido (XML)' conserva el snapshot original (8 / 5.9125 / 40.87 / 6.13 / 0.00 / 47.00) después de la edición", () => {
      const vm = buildPurchaseLinePresentation(
        clubPlatinoXmlLine({ quantity: 50, unitPrice: 2, discountPct: 0 }),
      );

      // vm.xml.* nunca depende de quantity/unitPrice editados — solo de los campos xml* congelados.
      expect(vm.xml.quantity).toBe("8.0000");
      expect(vm.xml.unitPrice).toBe("$5.9125");
      expect(vm.xml.taxableBase).toBe("$40.87");
      expect(vm.xml.taxValue).toBe("$6.13");
      expect(vm.xml.iceValue).toBe("$0.00");
      expect(vm.xml.totalLine).toBe("$47.00");
    });

    it("PURCHASE-LINE-PACKAGING-SUSPICIOUS-COST-GUARD-01 — Producto recibido (XML) NUNCA muestra valores derivados de la edición manual (no 8.3333 / $13.8873 / $100 / $15 / $115)", () => {
      const vm = buildPurchaseLinePresentation(
        clubPlatinoXmlLine({ quantity: 50, unitPrice: 2, discountPct: 0 }),
      );

      expect(vm.xml.quantity).not.toBe("8.3333");
      expect(vm.xml.unitPrice).not.toBe("$13.8873");
      expect(vm.xml.taxableBase).not.toBe("$100.00");
      expect(vm.xml.taxValue).not.toBe("$15.00");
      expect(vm.xml.totalLine).not.toBe("$115.00");
    });

    it("datos adicionales XML no cambian tras editar cantidad/costo/presentación", () => {
      const additionalFields = [{ name: "TIPO DE MATERIAL", value: "Liquido" }];
      const before = buildPurchaseLinePresentation(
        clubPlatinoXmlLine({ xmlAdditionalFields: additionalFields }),
      );
      const afterEdit = buildPurchaseLinePresentation(
        clubPlatinoXmlLine({
          xmlAdditionalFields: additionalFields,
          quantity: 50,
          unitPrice: 2,
          discountPct: 0,
          packagingLevelId: "unidad-x1",
          conversionFactor: 1,
          quantityInBaseUom: 50,
        }),
      );

      expect(before.xml.additionalFields).toEqual(additionalFields);
      expect(afterEdit.xml.additionalFields).toEqual(additionalFields);
    });

    it("editar solo Cantidad recalcula IVA/Total de cabecera", () => {
      const vm = buildPurchaseLinePresentation(
        clubPlatinoXmlLine({ quantity: 100, unitPrice: 2, discountPct: 0 }),
      );

      expect(vm.editableHeader.taxableBase).toBe(200);
      expect(vm.editableHeader.vatAmount).toBe(30);
      expect(vm.editableHeader.total).toBe(230);
    });

    it("editar solo Costo recalcula IVA/Total de cabecera", () => {
      const vm = buildPurchaseLinePresentation(
        clubPlatinoXmlLine({ quantity: 50, unitPrice: 3, discountPct: 0 }),
      );

      expect(vm.editableHeader.taxableBase).toBe(150);
      expect(vm.editableHeader.vatAmount).toBe(22.5);
      expect(vm.editableHeader.total).toBe(172.5);
    });

    it("cambiar presentación y luego editar no deja IVA/Total pegados a un valor anterior", () => {
      // Primer estado: presentación SIXPACK X6, sin editar (vm1 refleja quantity/unitPrice XML).
      const vm1 = buildPurchaseLinePresentation(
        clubPlatinoXmlLine({ packagingLevelId: "sixpack-x6" }),
      );
      // Segundo estado: el usuario cambió de presentación Y editó cabecera (quantity/unitPrice ya
      // no son los del XML) — editableHeader debe reflejar el estado nuevo, no arrastrar vm1.
      const vm2 = buildPurchaseLinePresentation(
        clubPlatinoXmlLine({
          packagingLevelId: "unidad-x1",
          quantity: 50,
          unitPrice: 2,
          discountPct: 0,
        }),
      );

      expect(vm1.editableHeader.total).toBeCloseTo(47.0, 2);
      expect(vm2.editableHeader.total).toBe(115);
      expect(vm2.editableHeader.total).not.toBeCloseTo(vm1.editableHeader.total, 2);

      // El bloque XML es ajeno al cambio de presentación: sigue siendo el mismo en los dos estados.
      expect(vm1.xml.quantity).toBe("8.0000");
      expect(vm1.xml.totalLine).toBe("$47.00");
      expect(vm2.xml.quantity).toBe("8.0000");
      expect(vm2.xml.totalLine).toBe("$47.00");
    });

    it("IRBPNR se conserva del snapshot XML tras editar (no se inventa un recálculo proporcional)", () => {
      const vm = buildPurchaseLinePresentation(
        clubPlatinoXmlLine({
          quantity: 50,
          unitPrice: 2,
          discountPct: 0,
          xmlIrbpnrAmount: 0.72,
        }),
      );

      expect(vm.editableHeader.irbpnrAmount).toBe(0.72);
      expect(vm.editableHeader.total).toBe(100 + 15 + 0 + 0.72);
      // El snapshot XML tampoco cambia.
      expect(vm.xml.irbpnrValue).toBe("$0.72");
    });

    it("línea manual (sin origen XML) sigue recalculando igual que antes — sin regresión", () => {
      const vm = buildPurchaseLinePresentation(
        line({
          purchaseReceptionLineId: undefined,
          xmlSupplierCode: undefined,
          quantity: 10,
          unitPrice: 10,
          discountPct: 0,
          context: { ...context, vatPercent: 15, icePercent: 0 },
        }),
      );

      expect(vm.editableHeader.taxableBase).toBe(100);
      expect(vm.editableHeader.vatAmount).toBe(15);
      expect(vm.editableHeader.irbpnrAmount).toBe(0);
      expect(vm.editableHeader.total).toBe(115);
    });
  });

  describe("PURCHASE-XML-LINE-ADDITIONAL-FIELDS-01 — datos adicionales del XML (detAdicional)", () => {
    it("expone additionalFields tal cual, sin traducir ni interpretar nombre/valor", () => {
      const vm = buildPurchaseLinePresentation(
        line({
          xmlAdditionalFields: [
            { name: "Unidad", value: "3 /  0" },
            { name: "valor2", value: "0.72" },
          ],
        }),
      );

      expect(vm.xml.hasAdditionalFields).toBe(true);
      expect(vm.xml.additionalFields).toEqual([
        { name: "Unidad", value: "3 /  0" },
        { name: "valor2", value: "0.72" },
      ]);
    });

    it("hasAdditionalFields es false y additionalFields es [] cuando la línea no trae detAdicional", () => {
      const vm = buildPurchaseLinePresentation(
        line({ xmlAdditionalFields: undefined }),
      );

      expect(vm.xml.hasAdditionalFields).toBe(false);
      expect(vm.xml.additionalFields).toEqual([]);
    });

    it("hasAdditionalFields es false cuando additionalFields es un arreglo vacío", () => {
      const vm = buildPurchaseLinePresentation(
        line({ xmlAdditionalFields: [] }),
      );

      expect(vm.xml.hasAdditionalFields).toBe(false);
      expect(vm.xml.additionalFields).toEqual([]);
    });
  });

  describe("PURCHASE-LINE-HEADER-BASE-QTY-COST-EDIT-01 — computeUnitPriceFromBaseUnitCost", () => {
    it("caso exacto — round-trip: 8 SIXPACK/48 unidades, costo real 0.8515 → 0.8615 recalcula unitPrice consistente", () => {
      const quantity = 8;
      const quantityInBaseUom = 48;
      const discountPct = (6.43 / 47.3) * 100;

      const unitPrice = computeUnitPriceFromBaseUnitCost({
        newBaseUnitCost: 0.8615,
        quantity,
        quantityInBaseUom,
        discountPct,
      });

      expect(unitPrice).not.toBeNull();
      // Round-trip: recalcular baseUnitCost hacia adelante con el unitPrice obtenido debe devolver
      // el mismo costo real que el usuario tecleó (misma fórmula que purchaseLinePresentation.ts).
      const netLineSubtotal =
        quantity * (unitPrice as number) -
        quantity * (unitPrice as number) * (discountPct / 100);
      const roundTripBaseUnitCost = netLineSubtotal / quantityInBaseUom;
      expect(roundTripBaseUnitCost).toBeCloseTo(0.8615, 4);
    });

    it("sin descuento/flete/otros gastos: unitPrice = (newBaseUnitCost * quantityInBaseUom) / quantity", () => {
      const unitPrice = computeUnitPriceFromBaseUnitCost({
        newBaseUnitCost: 1,
        quantity: 6,
        quantityInBaseUom: 12,
      });

      expect(unitPrice).toBeCloseTo((1 * 12) / 6, 6);
    });

    it("con flete/otros gastos: se descuentan del subtotal objetivo antes de despejar unitPrice (round-trip)", () => {
      const quantity = 8;
      const quantityInBaseUom = 48;
      const freightAllocated = 4.8;
      const otherCostsAllocated = 0.86;

      const unitPrice = computeUnitPriceFromBaseUnitCost({
        newBaseUnitCost: 1.1033,
        quantity,
        quantityInBaseUom,
        discountPct: 0,
        freightAllocated,
        otherCostsAllocated,
      });

      expect(unitPrice).not.toBeNull();
      const netLineSubtotal = quantity * (unitPrice as number);
      const roundTripBaseUnitCost =
        (netLineSubtotal + freightAllocated + otherCostsAllocated) / quantityInBaseUom;
      expect(roundTripBaseUnitCost).toBeCloseTo(1.1033, 3);
    });

    it("devuelve null si quantity es 0 (cálculo inseguro, no se aplica)", () => {
      expect(
        computeUnitPriceFromBaseUnitCost({
          newBaseUnitCost: 0.85,
          quantity: 0,
          quantityInBaseUom: 48,
        }),
      ).toBeNull();
    });

    it("devuelve null si quantityInBaseUom es 0 (cálculo inseguro, no se aplica)", () => {
      expect(
        computeUnitPriceFromBaseUnitCost({
          newBaseUnitCost: 0.85,
          quantity: 8,
          quantityInBaseUom: 0,
        }),
      ).toBeNull();
    });

    it("devuelve null si discountPct >= 100 (denominador inválido)", () => {
      expect(
        computeUnitPriceFromBaseUnitCost({
          newBaseUnitCost: 0.85,
          quantity: 8,
          quantityInBaseUom: 48,
          discountPct: 100,
        }),
      ).toBeNull();
    });

    it("devuelve null si el resultado sería un unitPrice negativo (flete/otros gastos superan el costo real objetivo)", () => {
      expect(
        computeUnitPriceFromBaseUnitCost({
          newBaseUnitCost: 0.1,
          quantity: 8,
          quantityInBaseUom: 48,
          freightAllocated: 100,
        }),
      ).toBeNull();
    });
  });
});
