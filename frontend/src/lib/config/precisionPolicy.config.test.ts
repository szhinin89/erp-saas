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
const apiPut = vi.fn();
vi.mock("../../modules/lib/apiEnvelope", () => ({
  apiGet: (...a: unknown[]) => apiGet(...a),
  apiPut: (...a: unknown[]) => apiPut(...a),
}));

// El setup global ya importó el módulo real: se reimporta con el transporte mockeado.
async function freshConfig() {
  vi.resetModules();
  return import("./precisionPolicy.config");
}

afterEach(() => {
  apiGet.mockReset();
  apiPut.mockReset();
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
    expect([p.moneyDecimals, p.taxDecimals, p.accountingDecimals, p.fiscalPercentageDecimals]).toEqual([
      TEST_PRECISION_POLICY.moneyDecimals,
      TEST_PRECISION_POLICY.taxDecimals,
      TEST_PRECISION_POLICY.accountingDecimals,
      TEST_PRECISION_POLICY.fiscalPercentageDecimals, // 04C1: parte del contrato de la API
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
    fiscalPercentageDecimals: 2,
    warehouseCapacityDecimals: 9,
    creditInstallmentPercentageDecimals: 6,
    packagingWeightDecimals: 5,
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
    ["fiscalPercentage", 2],
    ["warehouseCapacity", 9],
    ["creditInstallmentPercentage", 6],
    ["packagingWeight", 5],
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

  it("percentage (operativo) y fiscalPercentage (fiscal fijo) resuelven campos distintos (04C1)", () => {
    const policy = { ...TEST_PRECISION_POLICY, percentageDecimals: 4, fiscalPercentageDecimals: 2 };
    expect(resolvePrecisionDecimals(policy, "percentage")).toBe(4);
    expect(resolvePrecisionDecimals(policy, "fiscalPercentage")).toBe(2);
  });

  it("exhaustividad: el mapa cubre las 14 semánticas y cada campo *Decimals de la policy exactamente una vez", () => {
    expect(Object.keys(PRECISION_FIELD_BY_KIND).sort()).toEqual(
      [
        "accounting",
        "averageCost",
        "conversionFactor",
        "creditInstallmentPercentage",
        "fiscalPercentage",
        "money",
        "packagingWeight",
        "percentage",
        "purchaseUnitPrice",
        "quantity",
        "salesUnitPrice",
        "tax",
        "unitCost",
        "warehouseCapacity",
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

// ── ZH-DESIGN-SYSTEM-PRECISION-01C: estado único, concurrencia y suscripción ───────────────────
/** Promesa controlada manualmente: la carrera se ordena explícitamente, sin timers. */
function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

const POLICY_A: PrecisionPolicy = { ...TEST_PRECISION_POLICY, salesUnitPriceDecimals: 3 };
const POLICY_B: PrecisionPolicy = { ...TEST_PRECISION_POLICY, salesUnitPriceDecimals: 4 };
const POLICY_SAVED: PrecisionPolicy = { ...TEST_PRECISION_POLICY, salesUnitPriceDecimals: 5 };

/** Encola respuestas controladas para las siguientes llamadas a apiGet. */
function queueGets(count: number) {
  const pending = Array.from({ length: count }, () => deferred<PrecisionPolicy>());
  for (const d of pending) apiGet.mockImplementationOnce(() => d.promise);
  return pending;
}

describe("precisionPolicy.config — suscripción (01C)", () => {
  it("notifica tras un load aceptado y el snapshot expone la policy del backend", async () => {
    const cfg = await freshConfig();
    const listener = vi.fn();
    cfg.subscribePrecisionPolicy(listener);
    apiGet.mockResolvedValue(POLICY_A);
    await cfg.loadPrecisionPolicy();
    expect(listener).toHaveBeenCalledTimes(1);
    expect(cfg.getPrecisionPolicySnapshot()).toEqual({ loaded: true, policy: POLICY_A });
  });

  it("notifica tras clear de una policy cargada; clear sin policy no notifica (sin cambio de estado)", async () => {
    const cfg = await freshConfig();
    const listener = vi.fn();
    cfg.subscribePrecisionPolicy(listener);
    cfg.clearPrecisionPolicy();
    expect(listener).not.toHaveBeenCalled();
    apiGet.mockResolvedValue(POLICY_A);
    await cfg.loadPrecisionPolicy();
    listener.mockClear();
    cfg.clearPrecisionPolicy();
    expect(listener).toHaveBeenCalledTimes(1);
    expect(cfg.isPrecisionPolicyLoaded()).toBe(false);
  });

  it("save publica la respuesta real del backend y notifica", async () => {
    const cfg = await freshConfig();
    const listener = vi.fn();
    cfg.subscribePrecisionPolicy(listener);
    apiPut.mockResolvedValue(POLICY_SAVED);
    const result = await cfg.savePrecisionPolicy(POLICY_SAVED);
    expect(result).toBe(POLICY_SAVED);
    expect(apiPut).toHaveBeenCalledWith("/api/v1/config/precision-policy", POLICY_SAVED);
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_SAVED);
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("unsubscribe deja de recibir cambios", async () => {
    const cfg = await freshConfig();
    const listener = vi.fn();
    const unsubscribe = cfg.subscribePrecisionPolicy(listener);
    unsubscribe();
    apiGet.mockResolvedValue(POLICY_A);
    await cfg.loadPrecisionPolicy();
    cfg.clearPrecisionPolicy();
    expect(listener).not.toHaveBeenCalled();
  });

  it("una respuesta stale no notifica", async () => {
    const cfg = await freshConfig();
    const [first, second] = queueGets(2);
    const load1 = cfg.loadPrecisionPolicy();
    const load2 = cfg.loadPrecisionPolicy();
    const listener = vi.fn();
    cfg.subscribePrecisionPolicy(listener);
    second!.resolve(POLICY_B);
    await load2;
    expect(listener).toHaveBeenCalledTimes(1);
    first!.resolve(POLICY_A);
    await load1;
    expect(listener).toHaveBeenCalledTimes(1);
  });
});

describe("precisionPolicy.config — concurrencia (01C)", () => {
  it("carrera A → clear → B: la respuesta tardía de A nunca sobrescribe B", async () => {
    const cfg = await freshConfig();
    const [reqA, reqB] = queueGets(2);
    const loadA = cfg.loadPrecisionPolicy();
    cfg.clearPrecisionPolicy();
    const loadB = cfg.loadPrecisionPolicy();
    reqB!.resolve(POLICY_B);
    await loadB;
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_B);
    reqA!.resolve(POLICY_A);
    await expect(loadA).resolves.toBe(POLICY_A); // el caller recibe su respuesta…
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_B); // …pero no se publica
  });

  it("clear invalida un load en vuelo: el estado sigue vacío y get sigue fail-closed", async () => {
    const cfg = await freshConfig();
    const [req] = queueGets(1);
    const load = cfg.loadPrecisionPolicy();
    cfg.clearPrecisionPolicy();
    req!.resolve(POLICY_A);
    await load;
    expect(cfg.isPrecisionPolicyLoaded()).toBe(false);
    expect(cfg.getPrecisionPolicySnapshot()).toEqual({ loaded: false, policy: null });
    expect(() => cfg.getPrecisionPolicy()).toThrow(cfg.PrecisionPolicyNotLoadedError);
  });

  it("save gana a un load anterior que resuelve después", async () => {
    const cfg = await freshConfig();
    const [oldReq] = queueGets(1);
    const oldLoad = cfg.loadPrecisionPolicy();
    apiPut.mockResolvedValue(POLICY_SAVED);
    await cfg.savePrecisionPolicy(POLICY_SAVED);
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_SAVED);
    oldReq!.resolve(POLICY_A);
    await oldLoad;
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_SAVED);
  });

  it("un save invalidado por clear devuelve el resultado pero no lo publica", async () => {
    const cfg = await freshConfig();
    const put = deferred<PrecisionPolicy>();
    apiPut.mockImplementationOnce(() => put.promise);
    const save = cfg.savePrecisionPolicy(POLICY_SAVED);
    cfg.clearPrecisionPolicy();
    put.resolve(POLICY_SAVED);
    await expect(save).resolves.toBe(POLICY_SAVED);
    expect(cfg.isPrecisionPolicyLoaded()).toBe(false);
  });

  it.each(["load2 primero", "load1 primero"])("dos loads (%s): solo load2 queda vigente", async (order) => {
    const cfg = await freshConfig();
    const [req1, req2] = queueGets(2);
    const load1 = cfg.loadPrecisionPolicy();
    const load2 = cfg.loadPrecisionPolicy();
    if (order === "load2 primero") {
      req2!.resolve(POLICY_B);
      await load2;
      req1!.resolve(POLICY_A);
      await load1;
    } else {
      req1!.resolve(POLICY_A);
      await load1;
      expect(cfg.isPrecisionPolicyLoaded()).toBe(false);
      req2!.resolve(POLICY_B);
      await load2;
    }
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_B);
  });

  it("un load vigente que falla propaga el error y conserva el estado anterior (sin defaults)", async () => {
    const cfg = await freshConfig();
    apiGet.mockResolvedValueOnce(POLICY_A);
    await cfg.loadPrecisionPolicy();
    apiGet.mockRejectedValueOnce(new Error("down"));
    await expect(cfg.loadPrecisionPolicy()).rejects.toThrow("down");
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_A);
  });

  it("un load stale que falla no destruye el estado vigente", async () => {
    const cfg = await freshConfig();
    const [req1, req2] = queueGets(2);
    const load1 = cfg.loadPrecisionPolicy();
    const load2 = cfg.loadPrecisionPolicy();
    req2!.resolve(POLICY_B);
    await load2;
    req1!.reject(new Error("late failure"));
    await expect(load1).rejects.toThrow("late failure");
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_B);
  });

  it("setPrecisionPolicyForTests escribe el mismo estado único e invalida lo que esté en vuelo", async () => {
    const cfg = await freshConfig();
    const listener = vi.fn();
    cfg.subscribePrecisionPolicy(listener);
    const [req] = queueGets(1);
    const load = cfg.loadPrecisionPolicy();
    cfg.setPrecisionPolicyForTests(POLICY_B);
    expect(cfg.getPrecisionPolicySnapshot()).toEqual({ loaded: true, policy: POLICY_B });
    expect(listener).toHaveBeenCalledTimes(1);
    req!.resolve(POLICY_A);
    await load;
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_B);
  });
});

describe("precisionPolicy.config — snapshot (01C)", () => {
  it("vacío por defecto, sin policy ficticia", async () => {
    const cfg = await freshConfig();
    expect(cfg.getPrecisionPolicySnapshot()).toEqual({ loaded: false, policy: null });
  });

  it("referencia estable mientras el estado no cambie (apto para useSyncExternalStore)", async () => {
    const cfg = await freshConfig();
    const empty = cfg.getPrecisionPolicySnapshot();
    expect(cfg.getPrecisionPolicySnapshot()).toBe(empty);
    apiGet.mockResolvedValue(POLICY_A);
    await cfg.loadPrecisionPolicy();
    const loaded = cfg.getPrecisionPolicySnapshot();
    expect(loaded).not.toBe(empty);
    expect(cfg.getPrecisionPolicySnapshot()).toBe(loaded);
    expect(Object.isFrozen(loaded)).toBe(true);
    cfg.clearPrecisionPolicy();
    expect(cfg.getPrecisionPolicySnapshot()).toBe(empty);
  });

  it("getPrecisionPolicy, isPrecisionPolicyLoaded y el snapshot leen la misma fuente", async () => {
    const cfg = await freshConfig();
    apiGet.mockResolvedValue(POLICY_A);
    await cfg.loadPrecisionPolicy();
    const snapshot = cfg.getPrecisionPolicySnapshot();
    expect(snapshot.loaded).toBe(cfg.isPrecisionPolicyLoaded());
    expect(snapshot.policy).toBe(cfg.getPrecisionPolicy());
  });

  it("no muta la policy recibida del backend (ni la congela)", async () => {
    const cfg = await freshConfig();
    const fromApi: PrecisionPolicy = { ...POLICY_A };
    const before = JSON.stringify(fromApi);
    apiGet.mockResolvedValue(fromApi);
    await cfg.loadPrecisionPolicy();
    resolvePrecisionDecimals(cfg.getPrecisionPolicy(), "salesUnitPrice");
    expect(JSON.stringify(fromApi)).toBe(before);
    expect(Object.isFrozen(fromApi)).toBe(false);
    expect(cfg.getPrecisionPolicy()).toBe(fromApi);
  });
});

// ── ZH-DESIGN-SYSTEM-PRECISION-01C1: load iniciado durante un save en vuelo ──────────────────────
describe("precisionPolicy.config — save vs load iniciado durante el save (01C1)", () => {
  const POLICY_BEFORE_SAVE: PrecisionPolicy = { ...TEST_PRECISION_POLICY, salesUnitPriceDecimals: 2 };

  /** Deja la policy previa publicada y un save en vuelo controlado manualmente. */
  async function startPendingSave() {
    const cfg = await freshConfig();
    cfg.setPrecisionPolicyForTests(POLICY_BEFORE_SAVE);
    const put = deferred<PrecisionPolicy>();
    apiPut.mockImplementationOnce(() => put.promise);
    const save = cfg.savePrecisionPolicy(POLICY_SAVED);
    return { cfg, put, save };
  }

  it.each(["load resuelve primero", "save resuelve primero"])(
    "save → load (policy anterior) → %s: queda la policy confirmada por el save",
    async (order) => {
      const { cfg, put, save } = await startPendingSave();
      const [get] = queueGets(1);
      const load = cfg.loadPrecisionPolicy();
      if (order === "load resuelve primero") {
        get!.resolve(POLICY_BEFORE_SAVE);
        await load;
        put.resolve(POLICY_SAVED);
        await save;
      } else {
        put.resolve(POLICY_SAVED);
        await save;
        get!.resolve(POLICY_BEFORE_SAVE);
        await load;
      }
      expect(cfg.getPrecisionPolicy()).toBe(POLICY_SAVED);
    },
  );

  it("el load iniciado durante el save es stale: devuelve su respuesta pero no publica ni notifica", async () => {
    const { cfg, put, save } = await startPendingSave();
    const listener = vi.fn();
    cfg.subscribePrecisionPolicy(listener);
    const [get] = queueGets(1);
    const load = cfg.loadPrecisionPolicy();
    get!.resolve(POLICY_BEFORE_SAVE);
    await expect(load).resolves.toBe(POLICY_BEFORE_SAVE);
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_BEFORE_SAVE); // estado previo intacto, sin publicar
    expect(listener).not.toHaveBeenCalled();
    put.resolve(POLICY_SAVED);
    await save;
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("si el save falla, el load iniciado durante él tampoco publica: queda el estado previo", async () => {
    const { cfg, put, save } = await startPendingSave();
    const [get] = queueGets(1);
    const load = cfg.loadPrecisionPolicy();
    get!.resolve(POLICY_A);
    await load;
    put.reject(new Error("save failed"));
    await expect(save).rejects.toThrow("save failed");
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_BEFORE_SAVE);
  });

  it("tras terminar el save, un load nuevo vuelve a publicarse con normalidad", async () => {
    const { cfg, put, save } = await startPendingSave();
    put.resolve(POLICY_SAVED);
    await save;
    apiGet.mockResolvedValueOnce(POLICY_B);
    await cfg.loadPrecisionPolicy();
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_B);
  });

  it("clear durante el save anula el marcador: el load de la nueva empresa se publica y el save no revive", async () => {
    const { cfg, put, save } = await startPendingSave();
    cfg.clearPrecisionPolicy();
    const [get] = queueGets(1);
    const load = cfg.loadPrecisionPolicy();
    get!.resolve(POLICY_B);
    await load;
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_B);
    put.resolve(POLICY_SAVED);
    await save;
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_B);
  });

  it("dos saves: solo el último publica y un load durante el segundo sigue siendo stale", async () => {
    const cfg = await freshConfig();
    const put1 = deferred<PrecisionPolicy>();
    const put2 = deferred<PrecisionPolicy>();
    apiPut.mockImplementationOnce(() => put1.promise).mockImplementationOnce(() => put2.promise);
    const save1 = cfg.savePrecisionPolicy(POLICY_A);
    const save2 = cfg.savePrecisionPolicy(POLICY_SAVED);
    put1.resolve(POLICY_A);
    await save1; // el finally del save1 no debe liberar el marcador del save2
    const [get] = queueGets(1);
    const load = cfg.loadPrecisionPolicy();
    get!.resolve(POLICY_B);
    await load;
    put2.resolve(POLICY_SAVED);
    await save2;
    expect(cfg.getPrecisionPolicy()).toBe(POLICY_SAVED);
  });
});
