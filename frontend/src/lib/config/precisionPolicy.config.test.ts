import { afterEach, describe, expect, it, vi } from "vitest";
import { TEST_PRECISION_POLICY } from "../../test/precisionPolicyFixture";
import {
  PRECISION_FIELD_BY_KIND,
  resolvePrecisionDecimals,
  type PrecisionDecimalsField,
  type PrecisionKind,
  type PrecisionPolicy,
} from "./precisionPolicy.config";

const apiGet = vi.fn();
vi.mock("../../modules/lib/apiEnvelope", () => ({
  apiGet: (...a: unknown[]) => apiGet(...a),
  apiPut: vi.fn(),
}));

// El setup global ya importó el módulo real: se reimporta con el transporte mockeado.
async function freshConfig() {
  vi.resetModules();
  return import("./precisionPolicy.config");
}

afterEach(() => {
  apiGet.mockReset();
});

describe("precisionPolicy.config — sin defaults, fail-closed", () => {
  it("getPrecisionPolicy lanza si la política no está cargada (no devuelve defaults)", async () => {
    const { clearPrecisionPolicy, isPrecisionPolicyLoaded, getPrecisionPolicy, PrecisionPolicyNotLoadedError } =
      await freshConfig();
    clearPrecisionPolicy();
    expect(isPrecisionPolicyLoaded()).toBe(false);
    expect(() => getPrecisionPolicy()).toThrow(PrecisionPolicyNotLoadedError);
  });

  it("si la API falla, loadPrecisionPolicy propaga el error y sigue sin política", async () => {
    const { clearPrecisionPolicy, isPrecisionPolicyLoaded, loadPrecisionPolicy } = await freshConfig();
    clearPrecisionPolicy();
    apiGet.mockRejectedValue(new Error("down"));
    await expect(loadPrecisionPolicy()).rejects.toThrow("down");
    expect(isPrecisionPolicyLoaded()).toBe(false);
  });

  it("tras cargar, getPrecisionPolicy devuelve los valores de la API", async () => {
    const { clearPrecisionPolicy, getPrecisionPolicy, loadPrecisionPolicy } = await freshConfig();
    clearPrecisionPolicy();
    apiGet.mockResolvedValue({ ...TEST_PRECISION_POLICY, salesUnitPriceDecimals: 5 });
    await loadPrecisionPolicy();
    expect(getPrecisionPolicy().salesUnitPriceDecimals).toBe(5);
  });
});

// ZH-DESIGN-SYSTEM-PRECISION-01A — cobertura mínima de contrato (sin tocar reactividad / race A-B:
// esos temas son de Fase 1C).
describe("precisionPolicy.config — characterization 01A", () => {
  it("contract: loadPrecisionPolicy consulta GET /api/v1/config/precision-policy", async () => {
    const { clearPrecisionPolicy, loadPrecisionPolicy } = await freshConfig();
    clearPrecisionPolicy();
    apiGet.mockResolvedValue(TEST_PRECISION_POLICY);
    await loadPrecisionPolicy();
    expect(apiGet).toHaveBeenCalledWith("/api/v1/config/precision-policy");
  });

  it("contract: clearPrecisionPolicy tras una carga vuelve al estado no cargado (fail-closed)", async () => {
    const { clearPrecisionPolicy, getPrecisionPolicy, isPrecisionPolicyLoaded, loadPrecisionPolicy, PrecisionPolicyNotLoadedError } =
      await freshConfig();
    apiGet.mockResolvedValue(TEST_PRECISION_POLICY);
    await loadPrecisionPolicy();
    expect(isPrecisionPolicyLoaded()).toBe(true);
    clearPrecisionPolicy();
    expect(isPrecisionPolicyLoaded()).toBe(false);
    expect(() => getPrecisionPolicy()).toThrow(PrecisionPolicyNotLoadedError);
  });

  it("contract: getPrecisionPolicy devuelve los campos fijos del sistema tal como llegan de la API", async () => {
    const { clearPrecisionPolicy, getPrecisionPolicy, loadPrecisionPolicy } = await freshConfig();
    clearPrecisionPolicy();
    apiGet.mockResolvedValue(TEST_PRECISION_POLICY);
    await loadPrecisionPolicy();
    const p = getPrecisionPolicy();
    expect([p.moneyDecimals, p.taxDecimals, p.accountingDecimals]).toEqual([
      TEST_PRECISION_POLICY.moneyDecimals,
      TEST_PRECISION_POLICY.taxDecimals,
      TEST_PRECISION_POLICY.accountingDecimals,
    ]);
  });
});

// ── ZH-DESIGN-SYSTEM-PRECISION-01B: resolver semántico puro ────────────────────────────────────
describe("resolvePrecisionDecimals — semántica → campo real de la policy", () => {
  /**
   * Policy con un valor DISTINTO por campo (dentro de los rangos reales de
   * PrecisionPolicyDefinitions) para detectar mappings cruzados. money/tax/accounting son tres
   * campos independientes del DTO; hoy el backend los envía iguales, pero el frontend no debe
   * depender de eso, así que aquí se usan valores distintos.
   */
  const DISTINCT_POLICY: PrecisionPolicy = {
    ...TEST_PRECISION_POLICY,
    salesUnitPriceDecimals: 3,
    purchaseUnitPriceDecimals: 5,
    quantityDecimals: 1,
    percentageDecimals: 4,
    unitCostDecimals: 7,
    averageCostDecimals: 8,
    conversionFactorDecimals: 10,
    moneyDecimals: 11,
    taxDecimals: 12,
    accountingDecimals: 13,
  };

  it.each<[PrecisionKind, number]>([
    ["money", 11],
    ["tax", 12],
    ["accounting", 13],
    ["salesUnitPrice", 3],
    ["purchaseUnitPrice", 5],
    ["unitCost", 7],
    ["averageCost", 8],
    ["quantity", 1],
    ["percentage", 4],
    ["conversionFactor", 10],
  ])("%s → %s", (kind, expected) => {
    expect(resolvePrecisionDecimals(DISTINCT_POLICY, kind)).toBe(expected);
  });

  it("money/tax/accounting se leen de la policy (valores reales del backend, sin literal 2)", () => {
    expect(resolvePrecisionDecimals(TEST_PRECISION_POLICY, "money")).toBe(TEST_PRECISION_POLICY.moneyDecimals);
    expect(resolvePrecisionDecimals(TEST_PRECISION_POLICY, "tax")).toBe(TEST_PRECISION_POLICY.taxDecimals);
    expect(resolvePrecisionDecimals(TEST_PRECISION_POLICY, "accounting")).toBe(
      TEST_PRECISION_POLICY.accountingDecimals,
    );
  });

  it("no muta la policy", () => {
    const policy = Object.freeze({ ...DISTINCT_POLICY });
    const before = JSON.stringify(policy);
    for (const kind of Object.keys(PRECISION_FIELD_BY_KIND) as PrecisionKind[]) {
      resolvePrecisionDecimals(policy, kind);
    }
    expect(JSON.stringify(policy)).toBe(before);
  });

  it("dos policies distintas producen resultados distintos para toda semántica configurable", () => {
    const configurable: PrecisionKind[] = [
      "salesUnitPrice",
      "purchaseUnitPrice",
      "unitCost",
      "averageCost",
      "quantity",
      "percentage",
      "conversionFactor",
    ];
    for (const kind of configurable) {
      expect(resolvePrecisionDecimals(DISTINCT_POLICY, kind)).not.toBe(
        resolvePrecisionDecimals(TEST_PRECISION_POLICY, kind),
      );
    }
  });

  it("no aplica fallback: un campo ausente o no entero lanza (fail-closed)", () => {
    const broken = { ...TEST_PRECISION_POLICY, unitCostDecimals: undefined } as unknown as PrecisionPolicy;
    expect(() => resolvePrecisionDecimals(broken, "unitCost")).toThrow(TypeError);
    const fractional = { ...TEST_PRECISION_POLICY, quantityDecimals: 2.5 };
    expect(() => resolvePrecisionDecimals(fractional, "quantity")).toThrow(TypeError);
  });

  it("exhaustividad: el mapa cubre las 10 semánticas y cada campo *Decimals de la policy exactamente una vez", () => {
    expect(Object.keys(PRECISION_FIELD_BY_KIND).sort()).toEqual(
      [
        "accounting",
        "averageCost",
        "conversionFactor",
        "money",
        "percentage",
        "purchaseUnitPrice",
        "quantity",
        "salesUnitPrice",
        "tax",
        "unitCost",
      ],
    );
    const decimalsFields = Object.keys(TEST_PRECISION_POLICY)
      .filter((k) => k.endsWith("Decimals"))
      .sort();
    expect([...Object.values(PRECISION_FIELD_BY_KIND)].sort()).toEqual(decimalsFields);
    expect(Object.values(PRECISION_FIELD_BY_KIND)).not.toContain("settlementToleranceAmount");
  });

  it("exhaustividad en compile-time: omitir una semántica o agregar una desconocida no compila", () => {
    const missing = {
      money: "moneyDecimals",
      // @ts-expect-error — falta "conversionFactor" (y otras): Record<PrecisionKind, …> lo exige.
    } satisfies Record<PrecisionKind, PrecisionDecimalsField>;
    // @ts-expect-error — "settlementTolerance" no es un PrecisionKind.
    const unknownKind: PrecisionKind = "settlementTolerance";
    // @ts-expect-error — settlementToleranceAmount no es un campo de decimales.
    const notDecimals: PrecisionDecimalsField = "settlementToleranceAmount";
    expect([missing, unknownKind, notDecimals]).toHaveLength(3);
  });
});
