import { afterEach, describe, expect, it, vi } from "vitest";
import { TEST_PRECISION_POLICY } from "../../test/precisionPolicyFixture";

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
