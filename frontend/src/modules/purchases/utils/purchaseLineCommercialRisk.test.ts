import { describe, expect, it } from "vitest";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import type { PurchaseItemContextDto } from "../api/purchaseService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import { buildConfirmCommercialRisk, buildConfirmLineWarnings } from "./purchaseConfirmChecklist";
import { buildPurchaseLinePresentation } from "./purchaseLinePresentation";
import { getPurchaseLineReadiness } from "./purchaseLineReadiness";

// Caso real DINADEC 034-005-002241241 (datos leídos de la recepción y del borrador):
// XML cantidad 30 (SIXPACK X6) × $6.52167, descuento $16.24, precioTotalSinImpuesto $179.41,
// IVA 15 % $26.91, total $206.32. Presentación vinculada SIXPACK X6 → 180 unidades. PVP $1.09
// (sin IVA, por unidad base).
const context: PurchaseItemContextDto = {
  itemId: "corona",
  sku: "CORONA-330",
  shortName: "Corona Extra 330cc",
  description: "CERVEZA CORONA EXTRA 330CC",
  baseUomCode: "19",
  participatesInInventory: true,
  supplierCode: "22642",
  packagingLevels: [
    { id: "unit", name: "Unidad", baseQuantity: 1, uomCode: "19", isBaseUnit: true, isPurchaseDefault: false },
    { id: "six", name: "SIXPACK X6", baseQuantity: 6, uomCode: "02", isBaseUnit: false, isPurchaseDefault: true },
  ],
  currentStock: 0,
  availableStock: 0,
  reservedStock: 0,
  averageCost: 0,
  lastPurchaseCost: 0,
  pvp: 1.09,
  previousPrice: 1.09,
  maxDiscountPercent: 0,
  purchaseVatCode: "4",
  vatPercent: 15,
  exciseTaxCode: null,
  icePercent: 0,
  hasVat: true,
  hasIce: false,
  costMargin: 0,
  costMarginPercent: 0,
};

function dinadecLine(overrides: Partial<PurchaseLineFormValues> = {}): PurchaseLineFormValues {
  return {
    _key: 1,
    itemId: "corona",
    description: "CERVEZA CORONA EXTRA 330CC X6U LOCAL MQ",
    quantity: 30,
    unitPrice: 6.52167,
    discountPct: 8.3,
    vatCode: "4",
    warehouseId: "wh-1",
    packagingLevelId: "six",
    conversionFactor: 6,
    quantityInBaseUom: 180,
    xmlOriginPackagingLevelId: "six",
    xmlOriginFactor: 6,
    purchaseReceptionLineId: "rx-1",
    xmlSupplierCode: "22642",
    xmlQuantity: 30,
    xmlUnitPrice: 6.52167,
    xmlDiscountPct: 8.3,
    xmlDiscount: 16.24,
    xmlLineSubtotal: 179.41,
    xmlVatPercentage: 15,
    xmlTaxValue: 26.91,
    xmlTotalLine: 206.32,
    xmlTaxableBase: 179.41,
    context,
    ...overrides,
  } as PurchaseLineFormValues;
}

const vmOf = (line: PurchaseLineFormValues) =>
  buildPurchaseLinePresentation(line, TEST_PRECISION_POLICY);

describe("Regresión DINADEC 034-005-002241241 — 30 SIXPACK X6 → 180 unidades", () => {
  it("la equivalencia 30 × 6 = 180 correctamente vinculada no produce 'Cantidad o costo distintos'", () => {
    const vm = vmOf(dinadecLine());
    expect(vm.inventory.baseQuantityValue).toBe(180);
    expect(vm.editableHeader.taxableBase).toBeCloseTo(179.41, 2);
    expect(vm.registration.originDiffers).toBe(false);
  });

  // PENDIENTE DE DECISIÓN — el backend registra LandedUnitCost = round2(base) / 180 = 179.41 / 180
  // = $0.996722; la pantalla usa la base sin redondear (195.6501 × 0.917 = 179.41114) → $0.996729.
  // Alinear exige redondear a centavos el costo tecleado (contrato FASE 5); no se cambia aquí.
  it("costo base = neto sin IVA / 180: $0.996729 en pantalla (backend: $0.996722, diferencia < $0.00001)", () => {
    const vm = vmOf(dinadecLine());
    expect(vm.inventory.baseUnitCostValue).toBeCloseTo((30 * 6.52167 * (1 - 0.083)) / 180, 9);
    expect(vm.inventory.baseUnitCost).toBe("$0.996729");
    expect(Math.abs(vm.inventory.baseUnitCostValue - 179.41 / 180)).toBeLessThan(0.00001);
  });

  it("margen 8.56 % = (PVP 1.09 − 0.996722) / 1.09 — margen sobre precio, ambos sin IVA y por unidad", () => {
    const vm = vmOf(dinadecLine());
    expect(vm.commercial.profitability.marginPct).toBe("8.56");
  });
});

const readinessOf = (line: PurchaseLineFormValues) =>
  getPurchaseLineReadiness(line, { precisionPolicy: TEST_PRECISION_POLICY });
const withPvp = (pvp: number, overrides: Partial<PurchaseLineFormValues> = {}) =>
  dinadecLine({ context: { ...context, pvp }, ...overrides });
/** XML sin descuento a $6 el SIXPACK → $1.00 por unidad, igual al PVP. */
const zeroMarginLine = (overrides: Partial<PurchaseLineFormValues> = {}) =>
  dinadecLine({
    unitPrice: 6, discountPct: 0, xmlUnitPrice: 6, xmlDiscountPct: 0, xmlDiscount: 0,
    xmlLineSubtotal: 180, xmlTaxableBase: 180, context: { ...context, pvp: 1 }, ...overrides,
  });

describe("Regresión DINADEC — validaciones conservadas", () => {
  it("línea lista: sin bloqueo ni aviso comercial (8.56 % positivo; no hay umbral de margen bajo)", () => {
    const readiness = readinessOf(dinadecLine());
    expect(readiness.status).toBe("READY");
    expect(readiness.warning).toBeUndefined();
    expect(vmOf(dinadecLine()).commercial.profitability.marginState).toBe("positive");
  });

  it("divergencia real de cantidad (29 SIXPACK en vez de 30) sigue avisando", () => {
    expect(vmOf(dinadecLine({ quantity: 29, quantityInBaseUom: 174 })).registration.originDiffers).toBe(true);
  });

  it("divergencia real de costo (precio editado) sigue avisando", () => {
    expect(vmOf(dinadecLine({ unitPrice: 6.9 })).registration.originDiffers).toBe(true);
  });

  it("descuento editado (base distinta al XML) sigue avisando", () => {
    expect(vmOf(dinadecLine({ discountPct: 5 })).registration.originDiffers).toBe(true);
  });

  it("presentación incorrecta (Unidad en vez de SIXPACK): bloqueo de margen extremo intacto y sin aviso comercial duplicado", () => {
    const line = dinadecLine({
      packagingLevelId: "unit", conversionFactor: 1, quantityInBaseUom: 30,
      xmlOriginPackagingLevelId: "unit", xmlOriginFactor: 1,
    });
    expect(vmOf(line).inventory.baseUnitCostValue).toBeCloseTo(179.41 / 30, 4);
    const readiness = readinessOf(line);
    expect(readiness.status).toBe("SUSPICIOUS_PACKAGING_COST");
    expect(readiness.blocking).toBe(true);
    expect(readiness.warning).toBeUndefined();
  });
});

describe("Rentabilidad de línea — margen sobre precio; costo y PVP sin IVA, por unidad base", () => {
  it("margen negativo (−50 % ≤ m < 0): aviso no bloqueante con costo, PVP y margen", () => {
    const line = withPvp(0.9);
    const vm = vmOf(line);
    expect(vm.commercial.profitability.marginState).toBe("negative");
    expect(vm.commercial.profitability.marginPct).toBe("-10.75");
    const readiness = readinessOf(line);
    expect(readiness.blocking).toBe(false);
    expect(readiness.warning).toMatchObject({ status: "NEGATIVE_MARGIN", label: "Margen negativo" });
    expect(readiness.warning?.detail).toContain("$0.996729");
    expect(readiness.warning?.detail).toContain("$0.90");
    expect(readiness.warning?.detail).toContain("-10.75%");
  });

  it("margen cero (a la escala visible): aviso no bloqueante", () => {
    const line = zeroMarginLine();
    const vm = vmOf(line);
    expect(vm.inventory.baseUnitCostValue).toBe(1);
    expect(vm.registration.originDiffers).toBe(false);
    expect(vm.commercial.profitability.marginState).toBe("zero");
    expect(vm.commercial.profitability.marginPct).toBe("0.00");
    expect(readinessOf(line)).toMatchObject({ blocking: false, warning: { status: "ZERO_MARGIN", label: "Margen cero" } });
  });

  it("margen bajo positivo (0.33 %): sin aviso — no existe umbral empresarial configurable", () => {
    const line = withPvp(1);
    expect(vmOf(line).commercial.profitability).toMatchObject({ marginPct: "0.33", marginState: "positive" });
    expect(readinessOf(line).warning).toBeUndefined();
  });

  it("margen normal (PVP 1.50 → 33.55 %): sin aviso", () => {
    const line = withPvp(1.5);
    expect(vmOf(line).commercial.profitability.marginPct).toBe("33.55");
    expect(readinessOf(line).warning).toBeUndefined();
  });

  it("sin PVP: no presenta un margen ficticio y avisa que la rentabilidad no se puede evaluar", () => {
    const line = withPvp(0);
    expect(vmOf(line).commercial.profitability).toMatchObject({ marginState: "noSalePrice", marginPct: "—" });
    expect(readinessOf(line).warning).toMatchObject({ status: "MISSING_SALE_PRICE_FOR_MARGIN" });
  });

  it("sin cantidad a registrar: margen no evaluable ('—'), nunca 100 % ficticio", () => {
    const vm = vmOf(dinadecLine({ quantity: 0, quantityInBaseUom: 0 }));
    expect(vm.commercial.profitability).toMatchObject({ marginState: "unavailable", marginPct: "—" });
  });

  it("producto no inventariable (servicio): nunca avisa margen", () => {
    const line = dinadecLine({ context: { ...context, participatesInInventory: false, pvp: 0.5 } });
    expect(vmOf(line).commercial.profitability.marginState).toBe("negative");
    expect(readinessOf(line).warning).toBeUndefined();
  });

  it("el IVA no altera el costo comparado: la misma línea con IVA 0 % da igual costo y margen", () => {
    const noVat = dinadecLine({
      vatCode: "0", xmlVatPercentage: 0, xmlTaxValue: 0, xmlTotalLine: 179.41,
      context: { ...context, vatPercent: 0 },
    });
    expect(vmOf(noVat).inventory.baseUnitCostValue).toBe(vmOf(dinadecLine()).inventory.baseUnitCostValue);
    expect(vmOf(noVat).commercial.profitability.marginPct).toBe("8.56");
  });

  it("el ICE del XML no entra al costo comparado (igual que LandedUnitCost del backend)", () => {
    const withIce = dinadecLine({ xmlIceAmount: 10, iceCode: "3092", context: { ...context, hasIce: true } });
    expect(vmOf(withIce).inventory.baseUnitCostValue).toBe(vmOf(dinadecLine()).inventory.baseUnitCostValue);
  });

  it("la misma compra facturada como PACA X12 (15 × 12 = 180) da igual costo unitario y margen, sin aviso de diferencia", () => {
    const packs = {
      ...context,
      packagingLevels: [
        ...context.packagingLevels,
        { id: "paca", name: "PACA X12", baseQuantity: 12, uomCode: "03", isBaseUnit: false, isPurchaseDefault: false },
      ],
    };
    const vm = vmOf(dinadecLine({
      quantity: 15, unitPrice: 13.04334, packagingLevelId: "paca", conversionFactor: 12,
      xmlQuantity: 15, xmlUnitPrice: 13.04334, xmlOriginPackagingLevelId: "paca", xmlOriginFactor: 12,
      context: packs,
    }));
    expect(vm.registration.originDiffers).toBe(false);
    expect(vm.inventory.baseUnitCostValue).toBeCloseTo(vmOf(dinadecLine()).inventory.baseUnitCostValue, 9);
    expect(vm.commercial.profitability.marginPct).toBe("8.56");
  });
});

describe("Modal de confirmación — riesgo comercial compacto", () => {
  const t = ((key: string, params?: Record<string, unknown> | string) =>
    typeof params === "object" ? `${key}:${JSON.stringify(params)}` : key) as Parameters<
    typeof buildConfirmCommercialRisk
  >[2];

  it("resume por tipo, sin enumerar productos, y no los repite como advertencias por línea", () => {
    const lines = [
      withPvp(0.9, { _key: 1 }),
      withPvp(0.9, { _key: 2 }),
      zeroMarginLine({ _key: 3 }),
      withPvp(0, { _key: 4 }),
      dinadecLine({ _key: 5 }),
    ];
    const readinessByKey = Object.fromEntries(lines.map((l) => [l._key, readinessOf(l)]));
    const risk = buildConfirmCommercialRisk(lines, readinessByKey, t);
    expect(risk?.lineCount).toBe(4);
    expect(risk?.detail).toContain('commercialNegative:{"count":2}');
    expect(risk?.detail).toContain('commercialZero:{"count":1}');
    expect(risk?.detail).toContain('commercialNoSalePrice:{"count":1}');
    expect(risk?.detail).not.toContain("CORONA");
    expect(buildConfirmLineWarnings(lines, readinessByKey, TEST_PRECISION_POLICY, {}, {}, t)).toEqual([]);
  });

  it("una divergencia operativa real sigue listada por línea en el modal", () => {
    const lines = [dinadecLine({ quantity: 29, quantityInBaseUom: 174 })];
    const warnings = buildConfirmLineWarnings(lines, { 1: readinessOf(lines[0]) }, TEST_PRECISION_POLICY, {}, {}, t);
    expect(warnings.map((w) => w.reason)).toEqual(["purchases.lines.registrationOriginDiffers"]);
  });

  it("sin riesgo comercial no muestra indicación", () => {
    const lines = [dinadecLine()];
    expect(buildConfirmCommercialRisk(lines, { 1: readinessOf(lines[0]) }, t)).toBeNull();
  });
});
