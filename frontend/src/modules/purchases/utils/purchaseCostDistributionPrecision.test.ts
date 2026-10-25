import { afterEach, describe, expect, it } from "vitest";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import Decimal from "decimal.js";
import { buildCostDistributionInputFromFormLines, simulateCostDistribution, type DistributeCostSourceLine } from "./purchaseCalc";

afterEach(() => setPrecisionPolicyForTests(TEST_PRECISION_POLICY));

function lines(prices: number[], discount = false): DistributeCostSourceLine[] {
  return prices.map((price, index) => ({
    id: String(index), description: "Producto de prueba", quantity: 1,
    quantityInBaseUom: 1, unitPrice: price, discountAmount: discount ? price : 0,
    landedUnitCost: discount ? 0 : price, totalLineCost: discount ? 0 : price,
  }));
}

describe("distribución: paridad con las asignaciones actuales del backend", () => {
  it.each([2, 6, 10])("tres bases iguales con UnitCostDecimals=%i", (unitCostDecimals) => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, unitCostDecimals });
    const source = lines([1, 1, 1]);
    const result = simulateCostDistribution(source, new Set(source.map(l => l.id)), 1);
    // ProrateAmount usa FiscalPrecision.UnitCost para las asignaciones (no costo unitario).
    expect(result.map(l => l.allocatedAmount)).toEqual([0.333333, 0.333333, 0.333334]);
  });

  it.each([false, true])("base conjunta cero, descuento completo=%s", (discount) => {
    const source = lines(discount ? [10, 20, 30] : [0, 0, 0], discount);
    const result = simulateCostDistribution(source, new Set(source.map(l => l.id)), 1);
    expect(result.map(l => l.allocatedAmount)).toEqual([0.333333, 0.333333, 0.333334]);
  });

  it("bases desiguales y residuo", () => {
    const source = lines([1, 2, 4]);
    const result = simulateCostDistribution(source, new Set(source.map(l => l.id)), 1);
    expect(result.map(l => l.allocatedAmount)).toEqual([0.142857, 0.285714, 0.571429]);
  });
});


describe("distribuci?n: autoridad y conservaci?n del costo", () => {
  it("lee la escala de asignaci?n del contrato, independientemente del costo unitario", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, costAllocationDecimals: 4, unitCostDecimals: 10 });
    const source = lines([1, 1, 1]);
    expect(simulateCostDistribution(source, new Set(source.map(l => l.id)), 1).map(l => l.allocatedAmount))
      .toEqual([0.3333, 0.3333, 0.3334]);
  });

  it("no pierde las asignaciones existentes ni capitaliza diferencias del costo unitario redondeado", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, unitCostDecimals: 2 });
    const source = [{ ...lines([1])[0], quantity: 3, quantityInBaseUom: 3,
      unitPrice: 1 / 3, landedUnitCost: 0.33, totalLineCost: 1 }];
    const before = structuredClone(source);
    const result = simulateCostDistribution(source, new Set(["0"]), 1)[0];
    expect(result.newLineTotal).toBe(2);
    expect(result.newUnitCost).toBe(0.67);
    expect(source).toEqual(before);
    expect(simulateCostDistribution(source, new Set(["0"]), 1)[0]).toEqual(result);
  });

  it("mantiene el residuo exacto y el costo de l?nea al reconstruir el formulario", () => {
    const source = lines([1, 1, 1]);
    const preview = simulateCostDistribution(source, new Set(source.map(l => l.id)), 1);
    expect(preview.reduce((sum, l) => sum.plus(l.allocatedAmount), new Decimal(0)).toNumber()).toBe(1);
    const rebuilt = buildCostDistributionInputFromFormLines(preview.map((p, index) => ({
      _key: index, description: "Producto", quantity: 1, unitPrice: 1, vatCode: "0",
      discountPct: 0, freightAllocated: p.allocatedAmount,
    })));
    expect(rebuilt.map(l => l.totalLineCost)).toEqual([1.333333, 1.333333, 1.333334]);
  });
});
