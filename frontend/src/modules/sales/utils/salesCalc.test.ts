import { describe, it, expect } from "vitest";
import {
  lineGross,
  lineDiscountAmt,
  lineNet,
  calcLineTax,
  calcSummary,
  lineExceedsStock,
  lineQuantityInBaseUom,
  stockExceededMessage,
  presentationEquivalenceLabel,
  suggestedUnitPriceForPresentation,
  resolveDefaultLinePresentation,
  resolveLinePresentationChange,
  findMergeableLineIndex,
  stockBadgeInfo,
  parenthesizeRateLabel,
} from "./salesCalc";
import type {
  SalesLineInput,
  SalesInvoiceDetailDto,
} from "../api/salesService";

// ── Mock data: deterministic, no business logic, numeric placeholders only ──

const LINES: Record<string, SalesLineInput> = {
  simple: {
    description: "Resma de papel A4",
    quantity: 10,
    unitPrice: 5.0,
    vatCode: "10",
    discountPct: 0,
  },
  withDiscount: {
    itemId: "item-002",
    description: "Toner HP 85A",
    quantity: 2,
    unitPrice: 45.0,
    vatCode: "10",
    discountPct: 10,
  },
  vatZero: {
    description: "Servicio de consultoría",
    quantity: 1,
    unitPrice: 200.0,
    vatCode: "0",
    discountPct: 0,
  },
  vat5pct: {
    description: "Producto tarifa reducida",
    quantity: 3,
    unitPrice: 30.0,
    vatCode: "20",
    discountPct: 0,
  },
  withIce: {
    description: "Bebida gaseosa 500ml",
    quantity: 24,
    unitPrice: 0.75,
    vatCode: "10",
    discountPct: 0,
    iceCode: "ICE01",
  },
};

const TEST_VAT_RATES: Record<string, number> = { "0": 0, "10": 15, "20": 5 };
const TEST_ICE_RATES: Record<string, number> = { ICE01: 10 }; // 10% ad valorem

export const mockBackendDetails: SalesInvoiceDetailDto[] = [
  {
    id: "det-001",
    itemId: "item-001",
    warehouseId: null,
    description: "Resma de papel A4",
    snapshotSku: "PAP-A4",
    snapshotItemName: "Resma Papel A4 75gr",
    packagingLevelId: null,
    uomCode: "UNIT",
    baseUomCode: "UNIT",
    conversionFactor: 1,
    quantityInBaseUom: 10,
    quantity: 10,
    unitPrice: 5.0,
    discountPct: 0,
    discountAmount: 0,
    taxableBase: 50.0,
    vatCode: "10",
    vatRate: 15,
    vatAmount: 7.5,
    snapshotVatName: "IVA 15%",
    iceCode: null,
    iceRate: 0,
    iceAmount: 0,
    snapshotIceName: null,
    taxInclusiveTotal: 57.5,
    notes: null,
    sortOrder: 1,
  },
  {
    id: "det-002",
    itemId: "item-002",
    warehouseId: null,
    description: "Toner HP 85A",
    snapshotSku: "TON-85A",
    snapshotItemName: "Toner HP 85A Compatible",
    packagingLevelId: null,
    uomCode: "UNIT",
    baseUomCode: "UNIT",
    conversionFactor: 1,
    quantityInBaseUom: 2,
    quantity: 2,
    unitPrice: 45.0,
    discountPct: 10,
    discountAmount: 9.0,
    taxableBase: 81.0,
    vatCode: "10",
    vatRate: 15,
    vatAmount: 12.15,
    snapshotVatName: "IVA 15%",
    iceCode: null,
    iceRate: 0,
    iceAmount: 0,
    snapshotIceName: null,
    taxInclusiveTotal: 93.15,
    notes: null,
    sortOrder: 2,
  },
  {
    id: "det-003",
    itemId: null,
    warehouseId: null,
    description: "Servicio de consultoría",
    snapshotSku: null,
    snapshotItemName: null,
    packagingLevelId: null,
    uomCode: "UNIT",
    baseUomCode: "UNIT",
    conversionFactor: 1,
    quantityInBaseUom: 1,
    quantity: 1,
    unitPrice: 200.0,
    discountPct: 0,
    discountAmount: 0,
    taxableBase: 200.0,
    vatCode: "0",
    vatRate: 0,
    vatAmount: 0,
    snapshotVatName: "IVA 0%",
    iceCode: null,
    iceRate: 0,
    iceAmount: 0,
    snapshotIceName: null,
    taxInclusiveTotal: 200.0,
    notes: null,
    sortOrder: 3,
  },
];

// ── Tests: frontend preview approximation (NOT fiscal authority) ──

describe("salesCalc — UI preview layer", () => {
  describe("lineGross", () => {
    it("multiplies quantity by unit price", () => {
      expect(lineGross(LINES.simple)).toBe(50);
    });

    it("handles fractional quantities", () => {
      expect(lineGross({ ...LINES.simple, quantity: 2.5 })).toBe(12.5);
    });
  });

  describe("lineDiscountAmt", () => {
    it("returns 0 when no discount", () => {
      expect(lineDiscountAmt(LINES.simple)).toBe(0);
    });

    it("calculates discount from percentage", () => {
      expect(lineDiscountAmt(LINES.withDiscount)).toBe(9);
    });

    it("handles 100% discount", () => {
      expect(lineDiscountAmt({ ...LINES.simple, discountPct: 100 })).toBe(50);
    });
  });

  describe("lineNet", () => {
    it("returns gross minus discount", () => {
      expect(lineNet(LINES.withDiscount)).toBe(81);
    });

    it("equals gross when no discount", () => {
      expect(lineNet(LINES.simple)).toBe(50);
    });
  });

  describe("calcLineTax — preview approximation", () => {
    it("approximates IVA 15% for vatCode 10", () => {
      const { vat } = calcLineTax(LINES.simple, TEST_VAT_RATES);
      expect(vat).toBeCloseTo(7.5, 2);
    });

    it("returns 0 for vatCode 0", () => {
      const { vat } = calcLineTax(LINES.vatZero, TEST_VAT_RATES);
      expect(vat).toBe(0);
    });

    it("approximates IVA 5% for vatCode 20", () => {
      const { vat } = calcLineTax(LINES.vat5pct, TEST_VAT_RATES);
      expect(vat).toBeCloseTo(4.5, 2);
    });

    it("returns ice=0 when no iceRates provided (degradación sin catálogo)", () => {
      const { ice } = calcLineTax(LINES.withIce, TEST_VAT_RATES);
      expect(ice).toBe(0);
    });

    it("returns ice=0 when iceCode not found in iceRates", () => {
      const { ice } = calcLineTax(LINES.withIce, TEST_VAT_RATES, { OTHER: 5 });
      expect(ice).toBe(0);
    });

    it("calcula ICE ad valorem cuando se provee el catálogo", () => {
      // net = 24 × 0.75 = 18; ICE 10% = 1.8
      const { ice } = calcLineTax(
        LINES.withIce,
        TEST_VAT_RATES,
        TEST_ICE_RATES,
      );
      expect(ice).toBeCloseTo(1.8, 2);
    });

    it("calcula IVA sobre base neta + ICE (normativa SRI)", () => {
      // net = 18, ice = 1.8, base IVA = 19.8, IVA 15% = 2.97
      const { vat, ice } = calcLineTax(
        LINES.withIce,
        TEST_VAT_RATES,
        TEST_ICE_RATES,
      );
      expect(ice).toBeCloseTo(1.8, 2);
      expect(vat).toBeCloseTo(2.97, 2);
    });

    it("línea sin iceCode: IVA calculado sobre base neta únicamente", () => {
      const { vat, ice } = calcLineTax(
        LINES.simple,
        TEST_VAT_RATES,
        TEST_ICE_RATES,
      );
      // net = 50, sin ice, base IVA = 50, IVA 15% = 7.5
      expect(ice).toBe(0);
      expect(vat).toBeCloseTo(7.5, 2);
    });
  });

  describe("calcSummary", () => {
    it("aggregates multiple lines correctly", () => {
      const all = [LINES.simple, LINES.withDiscount, LINES.vatZero];
      const s = calcSummary(all, TEST_VAT_RATES);
      expect(s.subtotal).toBe(50 + 90 + 200);
      expect(s.discount).toBe(0 + 9 + 0);
      expect(s.netSubtotal).toBe(s.subtotal - s.discount);
    });

    it("returns zeros for empty list", () => {
      const s = calcSummary([]);
      expect(s.subtotal).toBe(0);
      expect(s.discount).toBe(0);
      expect(s.total).toBe(0);
    });

    it("acumula ICE en total cuando se provee el catálogo", () => {
      // withIce: net=18, ice=1.8, base IVA=19.8, IVA=2.97
      const s = calcSummary([LINES.withIce], TEST_VAT_RATES, TEST_ICE_RATES);
      expect(s.ice).toBeCloseTo(1.8, 2);
      expect(s.vat).toBeCloseTo(2.97, 2);
      expect(s.total).toBeCloseTo(18 + 1.8 + 2.97, 2);
    });

    it("total sin ICE cuando no se provee catálogo (degradación)", () => {
      const s = calcSummary([LINES.withIce], TEST_VAT_RATES);
      expect(s.ice).toBe(0);
      // IVA sobre net (sin ice) = 18 × 15% = 2.7
      expect(s.vat).toBeCloseTo(2.7, 2);
    });
  });

  describe("mock backend data — structural integrity", () => {
    it("has consistent taxInclusiveTotal = taxableBase + vatAmount + iceAmount", () => {
      for (const d of mockBackendDetails) {
        expect(d.taxInclusiveTotal).toBeCloseTo(
          d.taxableBase + d.vatAmount + d.iceAmount,
          2,
        );
      }
    });

    it("has consistent taxableBase = (quantity * unitPrice) - discountAmount", () => {
      for (const d of mockBackendDetails) {
        expect(d.taxableBase).toBeCloseTo(
          d.quantity * d.unitPrice - d.discountAmount,
          2,
        );
      }
    });

    it("has sequential sortOrder", () => {
      mockBackendDetails.forEach((d, i) => {
        expect(d.sortOrder).toBe(i + 1);
      });
    });
  });
});

// ── lineExceedsStock — advertencia preventiva de stock (SALES-RETAIL-READY-01-FIX03) ──
describe("lineExceedsStock", () => {
  it("true cuando la cantidad supera el stock disponible en un ítem inventariable", () => {
    expect(
      lineExceedsStock({ _tracksStock: true, _stockQty: 0, quantity: 1 }),
    ).toBe(true);
    expect(
      lineExceedsStock({ _tracksStock: true, _stockQty: 3, quantity: 5 }),
    ).toBe(true);
  });

  it("false cuando la cantidad no supera el stock disponible", () => {
    expect(
      lineExceedsStock({ _tracksStock: true, _stockQty: 5, quantity: 5 }),
    ).toBe(false);
    expect(
      lineExceedsStock({ _tracksStock: true, _stockQty: 10, quantity: 1 }),
    ).toBe(false);
  });

  it("false para ítems que no controlan inventario, sin importar la cantidad", () => {
    expect(
      lineExceedsStock({ _tracksStock: false, _stockQty: 0, quantity: 100 }),
    ).toBe(false);
  });

  it("false (no bloquea) cuando el dato de disponibilidad no está cargado — nunca inventa stock", () => {
    expect(
      lineExceedsStock({ _tracksStock: true, _stockQty: undefined, quantity: 100 }),
    ).toBe(false);
  });

  // SALES-PRESENTATIONS-03: compara contra unidad base (quantity * conversionFactor), no contra
  // la cantidad cruda en la presentación vendida.
  it("true cuando la cantidad en unidad base (caja x12) supera el stock disponible", () => {
    // Stock 10 unidades base, venta 1 caja x12 = 12 unidades base requeridas → excede.
    expect(
      lineExceedsStock({
        _tracksStock: true,
        _stockQty: 10,
        quantity: 1,
        conversionFactor: 12,
      }),
    ).toBe(true);
  });

  it("false cuando la cantidad en unidad base (caja x12) no supera el stock disponible", () => {
    // Stock 20 unidades base, venta 1 caja x12 = 12 unidades base requeridas → no excede.
    expect(
      lineExceedsStock({
        _tracksStock: true,
        _stockQty: 20,
        quantity: 1,
        conversionFactor: 12,
      }),
    ).toBe(false);
  });
});

describe("lineQuantityInBaseUom", () => {
  it("sin presentación (factor 1), es igual a quantity", () => {
    expect(lineQuantityInBaseUom({ quantity: 5 })).toBe(5);
    expect(lineQuantityInBaseUom({ quantity: 5, conversionFactor: 1 })).toBe(5);
  });

  it("con presentación caja x12, multiplica quantity por el factor", () => {
    expect(lineQuantityInBaseUom({ quantity: 2, conversionFactor: 12 })).toBe(24);
  });
});

describe("stockExceededMessage", () => {
  it("mensaje simple sin presentación (factor 1)", () => {
    expect(
      stockExceededMessage({ quantity: 5, _stockQty: 3 }),
    ).toBe("Supera el disponible (3 UDS)");
  });

  it("mensaje claro con equivalencia cuando hay presentación (caja x12)", () => {
    expect(
      stockExceededMessage({
        quantity: 1,
        conversionFactor: 12,
        uomCode: "CAJA",
        baseUomCode: "UNIT",
        _stockQty: 10,
      }),
    ).toBe(
      "Stock insuficiente: 1 CAJA equivale a 12 UNIT, disponible 10 UNIT.",
    );
  });
});

describe("presentationEquivalenceLabel", () => {
  it("null cuando no hay presentación (factor 1) — no aporta nada, no se muestra", () => {
    expect(
      presentationEquivalenceLabel({ quantity: 5, baseUomCode: "UNIT" }),
    ).toBeNull();
  });

  it("'Equivale a X unidades' cuando hay presentación con factor > 1", () => {
    expect(
      presentationEquivalenceLabel({
        quantity: 2,
        conversionFactor: 12,
        baseUomCode: "UNIT",
      }),
    ).toBe("Equivale a 24 UNIT");
  });
});

describe("suggestedUnitPriceForPresentation", () => {
  it("precio sugerido = precio base * factor de conversión", () => {
    expect(suggestedUnitPriceForPresentation(1.5, 12)).toBe(18);
  });

  it("factor 1 (unidad base) deja el precio sin cambios", () => {
    expect(suggestedUnitPriceForPresentation(1.5, 1)).toBe(1.5);
  });
});

// ── resolveDefaultLinePresentation — regla 2/5: barcode de presentación autoselecciona,
// búsqueda normal usa unidad base por defecto ──────────────────────────────────────
describe("resolveDefaultLinePresentation", () => {
  const packagingLevels = [
    { id: "unit-1", uomCode: "UNIT", baseQuantity: 1 },
    { id: "caja-12", uomCode: "CAJA", baseQuantity: 12 },
  ];

  it("sin coincidencia de barcode de presentación, usa unidad base por defecto", () => {
    const result = resolveDefaultLinePresentation({
      baseUomCode: "UNIT",
      packagingLevels,
      matchedPackagingLevelId: null,
    });
    expect(result).toEqual({
      packagingLevelId: null,
      uomCode: "UNIT",
      conversionFactor: 1,
    });
  });

  it("con barcode de presentación coincidente (caja x12), la autoselecciona", () => {
    const result = resolveDefaultLinePresentation({
      baseUomCode: "UNIT",
      packagingLevels,
      matchedPackagingLevelId: "caja-12",
    });
    expect(result).toEqual({
      packagingLevelId: "caja-12",
      uomCode: "CAJA",
      conversionFactor: 12,
    });
  });

  it("matchedPackagingLevelId que ya no existe en el catálogo, cae a unidad base (no inventa)", () => {
    const result = resolveDefaultLinePresentation({
      baseUomCode: "UNIT",
      packagingLevels,
      matchedPackagingLevelId: "no-existe",
    });
    expect(result.packagingLevelId).toBeNull();
    expect(result.conversionFactor).toBe(1);
  });
});

// ── resolveLinePresentationChange — regla 8: sin doble multiplicación al cambiar de
// presentación (siempre parte del precio BASE, nunca del unitPrice ya escalado) ──────
describe("resolveLinePresentationChange", () => {
  const packagingLevels = [
    { id: "unit-1", uomCode: "UNIT", baseQuantity: 1 },
    { id: "caja-12", uomCode: "CAJA", baseQuantity: 12 },
    { id: "pack-6", uomCode: "PACK", baseQuantity: 6 },
  ];

  it("cambiar de unidad base a caja x12: recalcula factor, uomCode y precio sugerido", () => {
    const result = resolveLinePresentationChange(
      "caja-12",
      packagingLevels,
      "UNIT",
      1.5, // precio base
    );
    expect(result).toEqual({
      packagingLevelId: "caja-12",
      uomCode: "CAJA",
      conversionFactor: 12,
      unitPrice: 18,
    });
  });

  it("cambiar de caja x12 a pack x6: recalcula desde el precio BASE, no desde 18 (caja)", () => {
    // Si partiera de unitPrice=18 (ya escalado por caja) el resultado sería 108 — el cálculo
    // correcto siempre parte de basePrice=1.5 (regla 8, evita doble multiplicación).
    const result = resolveLinePresentationChange(
      "pack-6",
      packagingLevels,
      "UNIT",
      1.5,
    );
    expect(result.conversionFactor).toBe(6);
    expect(result.unitPrice).toBe(9);
  });

  it("volver a unidad base (packagingLevelId vacío): factor 1, precio = precio base", () => {
    const result = resolveLinePresentationChange("", packagingLevels, "UNIT", 1.5);
    expect(result).toEqual({
      packagingLevelId: null,
      uomCode: "UNIT",
      conversionFactor: 1,
      unitPrice: 1.5,
    });
  });
});

// ── findMergeableLineIndex — condición de fusión al reescanear (SALES-RETAIL-READY-01-FIX04) ──
describe("findMergeableLineIndex", () => {
  type TestLine = {
    itemId?: string | null;
    unitPrice: number;
    discountPct?: number | null;
    vatCode: string;
    iceCode?: string | null;
    warehouseId?: string | null;
    quantity: number;
  };

  const existing: TestLine = {
    itemId: "item-1",
    unitPrice: 29.9,
    discountPct: 0,
    vatCode: "0",
    iceCode: null,
    warehouseId: "wh-1",
    quantity: 1,
  };

  it("encuentra la línea y, al aplicar el incremento, la cantidad pasa de 1 a 2 (sin crear otra línea)", () => {
    const lines: TestLine[] = [existing];
    const idx = findMergeableLineIndex(lines, {
      itemId: "item-1",
      unitPrice: 29.9,
      vatCode: "0",
      iceCode: null,
      warehouseId: "wh-1",
    });

    expect(idx).toBe(0);

    // Mismo merge inmutable que usa useSalesPage.addLineWithItem.
    const merged = lines.map((l, i) =>
      i === idx ? { ...l, quantity: l.quantity + 1 } : l,
    );

    expect(merged).toHaveLength(1); // ninguna línea oculta/nueva
    expect(merged[0].quantity).toBe(2);
    expect(lines[0].quantity).toBe(1); // el original no se mutó (inmutable)
  });

  it("no fusiona si el precio no coincide (precio ya editado manualmente)", () => {
    const idx = findMergeableLineIndex([existing], {
      itemId: "item-1",
      unitPrice: 25, // distinto
      vatCode: "0",
      iceCode: null,
      warehouseId: "wh-1",
    });
    expect(idx).toBe(-1);
  });

  it("no fusiona si la línea existente ya tiene un descuento manual aplicado", () => {
    const discounted: TestLine = { ...existing, discountPct: 15 };
    const idx = findMergeableLineIndex([discounted], {
      itemId: "item-1",
      unitPrice: 29.9,
      vatCode: "0",
      iceCode: null,
      warehouseId: "wh-1",
    });
    expect(idx).toBe(-1);
  });

  it("no fusiona si el código de IVA no coincide", () => {
    const idx = findMergeableLineIndex([existing], {
      itemId: "item-1",
      unitPrice: 29.9,
      vatCode: "10",
      iceCode: null,
      warehouseId: "wh-1",
    });
    expect(idx).toBe(-1);
  });

  it("no fusiona si el código de ICE no coincide", () => {
    const idx = findMergeableLineIndex([existing], {
      itemId: "item-1",
      unitPrice: 29.9,
      vatCode: "0",
      iceCode: "ICE01",
      warehouseId: "wh-1",
    });
    expect(idx).toBe(-1);
  });

  it("no fusiona si la bodega no coincide", () => {
    const idx = findMergeableLineIndex([existing], {
      itemId: "item-1",
      unitPrice: 29.9,
      vatCode: "0",
      iceCode: null,
      warehouseId: "wh-2",
    });
    expect(idx).toBe(-1);
  });

  it("no fusiona si el itemId no coincide (producto distinto)", () => {
    const idx = findMergeableLineIndex([existing], {
      itemId: "item-2",
      unitPrice: 29.9,
      vatCode: "0",
      iceCode: null,
      warehouseId: "wh-1",
    });
    expect(idx).toBe(-1);
  });

  // SALES-PRESENTATIONS-03: reescanear el barcode de una presentación distinta a la ya agregada
  // no debe fusionar — cada presentación es su propia línea (nunca se mezcla "2 unidades" con
  // "1 caja" bajo la misma fila).
  it("no fusiona si la presentación (packagingLevelId) no coincide", () => {
    const withPresentation = { ...existing, packagingLevelId: "caja-x12" };
    const idx = findMergeableLineIndex([withPresentation], {
      itemId: "item-1",
      unitPrice: 29.9,
      vatCode: "0",
      iceCode: null,
      warehouseId: "wh-1",
      packagingLevelId: null,
    });
    expect(idx).toBe(-1);
  });

  it("fusiona cuando la misma presentación se reescanea (packagingLevelId coincide)", () => {
    const withPresentation = { ...existing, packagingLevelId: "caja-x12" };
    const idx = findMergeableLineIndex([withPresentation], {
      itemId: "item-1",
      unitPrice: 29.9,
      vatCode: "0",
      iceCode: null,
      warehouseId: "wh-1",
      packagingLevelId: "caja-x12",
    });
    expect(idx).toBe(0);
  });
});

// ── stockBadgeInfo / parenthesizeRateLabel — buscador POS (SALES-RETAIL-READY-01-FIX05) ──
describe("stockBadgeInfo", () => {
  it("Sin stock (rojo) cuando la cantidad es 0 o negativa", () => {
    expect(stockBadgeInfo(0)).toEqual({ label: "Sin stock", variant: "red" });
  });

  it("Stock bajo (naranja) cuando la cantidad es baja pero positiva (≤5)", () => {
    expect(stockBadgeInfo(2)).toEqual({ label: "Stock bajo", variant: "orange" });
    expect(stockBadgeInfo(5)).toEqual({ label: "Stock bajo", variant: "orange" });
  });

  it("Disponible (verde) cuando la cantidad supera el umbral bajo", () => {
    expect(stockBadgeInfo(6)).toEqual({ label: "Disponible", variant: "green" });
  });
});

describe("parenthesizeRateLabel", () => {
  it("convierte 'IVA 15%' en 'IVA (15%)'", () => {
    expect(parenthesizeRateLabel("IVA 15%")).toBe("IVA (15%)");
  });

  it("convierte 'IVA 0%' en 'IVA (0%)'", () => {
    expect(parenthesizeRateLabel("IVA 0%")).toBe("IVA (0%)");
  });

  it("deja sin cambios un texto que no tiene formato de tasa porcentual", () => {
    expect(parenthesizeRateLabel("Sin IVA")).toBe("Sin IVA");
  });
});
