// @vitest-environment jsdom
import { Component, type ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, renderHook, screen } from "@testing-library/react";
import { usePrecisionDecimals, usePrecisionPolicy } from "./usePrecisionPolicy";
import {
  clearPrecisionPolicy,
  getPrecisionPolicy,
  getPrecisionPolicySnapshot,
  loadPrecisionPolicy,
  resolvePrecisionDecimals,
  setPrecisionPolicyForTests,
  PrecisionPolicyNotLoadedError,
  type PrecisionKind,
  type PrecisionPolicy,
} from "../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../test/precisionPolicyFixture";
import { useAuthStore } from "../store/authStore";

const apiGet = vi.fn();
vi.mock("../modules/lib/apiEnvelope", () => ({
  apiGet: (...a: unknown[]) => apiGet(...a),
  apiPut: vi.fn(),
}));

afterEach(() => {
  cleanup();
  apiGet.mockReset();
});

/** Empresa A / B con escalas deliberadamente distintas (dentro de los rangos reales). */
const POLICY_A: PrecisionPolicy = {
  ...TEST_PRECISION_POLICY,
  unitCostDecimals: 6,
  averageCostDecimals: 8,
  salesUnitPriceDecimals: 4,
  quantityDecimals: 4,
  moneyDecimals: 2,
  taxDecimals: 3,
  accountingDecimals: 5,
};
const POLICY_B: PrecisionPolicy = {
  ...TEST_PRECISION_POLICY,
  unitCostDecimals: 4,
  averageCostDecimals: 10,
  salesUnitPriceDecimals: 6,
  quantityDecimals: 0,
  moneyDecimals: 7,
  taxDecimals: 8,
  accountingDecimals: 9,
};

/** Solo para observar el fail-closed del hook sin que el error escape del test. */
class Boundary extends Component<{ children: ReactNode }, { error: Error | null }> {
  state = { error: null as Error | null };
  static getDerivedStateFromError(error: Error) {
    return { error };
  }
  render() {
    return this.state.error ? <span data-testid="error">{this.state.error.name}</span> : this.props.children;
  }
}

describe("usePrecisionPolicy — observa el único snapshot (01D)", () => {
  it("devuelve exactamente la referencia del snapshot vigente (sin copia ni transformación)", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { result } = renderHook(() => usePrecisionPolicy());
    expect(result.current).toBe(POLICY_A);
    expect(result.current).toBe(getPrecisionPolicySnapshot().policy);
    expect(Object.isFrozen(result.current)).toBe(false);
  });

  it("mismo snapshot → misma referencia entre renders y un solo render por cambio (sin bucles)", () => {
    setPrecisionPolicyForTests(POLICY_A);
    let renders = 0;
    const { result, rerender } = renderHook(() => {
      renders++;
      return usePrecisionPolicy();
    });
    const first = result.current;
    expect(renders).toBe(1);
    rerender();
    expect(result.current).toBe(first);
    act(() => setPrecisionPolicyForTests(POLICY_B));
    expect(renders).toBe(3); // mount + rerender manual + 1 por el cambio del store
    expect(result.current).toBe(POLICY_B);
  });

  it("un load aceptado re-renderiza con la policy del backend", async () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { result } = renderHook(() => usePrecisionPolicy());
    apiGet.mockResolvedValue(POLICY_B);
    await act(async () => {
      await loadPrecisionPolicy();
    });
    expect(result.current).toBe(POLICY_B);
  });

  it("clear lleva al contrato fail-closed (PrecisionPolicyNotLoadedError, sin default)", () => {
    setPrecisionPolicyForTests(POLICY_A);
    vi.spyOn(console, "error").mockImplementation(() => {});
    function Probe() {
      return <span data-testid="value">{usePrecisionPolicy().unitCostDecimals}</span>;
    }
    render(<Boundary><Probe /></Boundary>);
    expect(screen.getByTestId("value").textContent).toBe("6");
    act(() => clearPrecisionPolicy());
    expect(screen.getByTestId("error").textContent).toBe(new PrecisionPolicyNotLoadedError().name);
    vi.mocked(console.error).mockRestore();
  });

  it("sin policy cargada al montar, lanza el mismo error que getPrecisionPolicy()", () => {
    setPrecisionPolicyForTests(null);
    vi.spyOn(console, "error").mockImplementation(() => {});
    expect(() => getPrecisionPolicy()).toThrow(PrecisionPolicyNotLoadedError);
    expect(() => renderHook(() => usePrecisionPolicy())).toThrow(PrecisionPolicyNotLoadedError);
    vi.mocked(console.error).mockRestore();
  });

  it("se desuscribe al desmontar: cambios posteriores no re-renderizan", () => {
    setPrecisionPolicyForTests(POLICY_A);
    let renders = 0;
    const { unmount } = renderHook(() => {
      renders++;
      return usePrecisionPolicy();
    });
    unmount();
    act(() => setPrecisionPolicyForTests(POLICY_B));
    expect(renders).toBe(1);
  });

  it("múltiples consumidores observan la MISMA referencia, igual que getPrecisionPolicy()", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const a = renderHook(() => usePrecisionPolicy());
    const b = renderHook(() => usePrecisionPolicy());
    expect(a.result.current).toBe(b.result.current);
    expect(a.result.current).toBe(getPrecisionPolicy());
    act(() => setPrecisionPolicyForTests(POLICY_B));
    expect(a.result.current).toBe(POLICY_B);
    expect(b.result.current).toBe(POLICY_B);
    expect(getPrecisionPolicy()).toBe(a.result.current);
  });
});

describe("usePrecisionDecimals — snapshot → React → resolver central (01D)", () => {
  const KINDS: PrecisionKind[] = [
    "money",
    "tax",
    "accounting",
    "salesUnitPrice",
    "purchaseUnitPrice",
    "unitCost",
    "averageCost",
    "quantity",
    "percentage",
    "conversionFactor",
  ];

  it.each(KINDS)("%s devuelve el resultado de resolvePrecisionDecimals para la policy vigente", (kind) => {
    setPrecisionPolicyForTests(POLICY_B);
    const { result } = renderHook(() => usePrecisionDecimals(kind));
    expect(result.current).toBe(resolvePrecisionDecimals(POLICY_B, kind));
  });

  it("money/tax/accounting salen de la policy, no de un literal 2", () => {
    setPrecisionPolicyForTests(POLICY_B);
    const { result } = renderHook(() => [
      usePrecisionDecimals("money"),
      usePrecisionDecimals("tax"),
      usePrecisionDecimals("accounting"),
    ]);
    expect(result.current).toEqual([7, 8, 9]);
  });

  it("unitCost, averageCost y quantity siguen el cambio de policy (escalas distintas)", () => {
    setPrecisionPolicyForTests(POLICY_A);
    const { result } = renderHook(() => ({
      unitCost: usePrecisionDecimals("unitCost"),
      averageCost: usePrecisionDecimals("averageCost"),
      quantity: usePrecisionDecimals("quantity"),
    }));
    expect(result.current).toEqual({ unitCost: 6, averageCost: 8, quantity: 4 });
    act(() => setPrecisionPolicyForTests(POLICY_B));
    expect(result.current).toEqual({ unitCost: 4, averageCost: 10, quantity: 0 });
  });
});

describe("multiempresa — A → B reactivo sin desmontar (01D)", () => {
  function Probe() {
    const unitCost = usePrecisionDecimals("unitCost");
    const salesUnitPrice = usePrecisionDecimals("salesUnitPrice");
    return <output data-testid="probe">{`${unitCost}/${salesUnitPrice}`}</output>;
  }

  it("publicar la policy de la empresa B actualiza el probe montado de 6/4 a 4/6", async () => {
    setPrecisionPolicyForTests(POLICY_A);
    render(<Probe />);
    const probe = screen.getByTestId("probe");
    expect(probe.textContent).toBe("6/4");
    apiGet.mockResolvedValue(POLICY_B);
    // Load de la empresa B sobre el árbol montado (sin gating: el clear del switch se prueba abajo,
    // con el gating de SessionBootstrap, porque un consumidor montado sin gate es fail-closed).
    await act(async () => {
      await loadPrecisionPolicy();
    });
    expect(screen.getByTestId("probe")).toBe(probe); // mismo nodo: no hubo remount
    expect(probe.textContent).toBe("4/6");
  });

  it("gating tipo SessionBootstrap: switch de empresa + clear en el mismo tick no expone error ni policy anterior", async () => {
    // Réplica mínima del gating real: el padre lee companyId de authStore y no renderiza hijos
    // hasta que la policy de ESA empresa está cargada.
    function Gate({ readyCompanyId, children }: { readyCompanyId: string; children: ReactNode }) {
      const companyId = useAuthStore((s) => s.user?.companyId ?? null);
      return readyCompanyId === companyId ? <>{children}</> : null;
    }
    const tree = (readyCompanyId: string) => (
      <Boundary><Gate readyCompanyId={readyCompanyId}><Probe /></Gate></Boundary>
    );
    vi.spyOn(console, "error").mockImplementation(() => {});
    useAuthStore.setState({ user: { companyId: "company-a" } as never, isAuthenticated: true });
    setPrecisionPolicyForTests(POLICY_A);
    const { rerender } = render(tree("company-a"));
    expect(screen.getByTestId("probe").textContent).toBe("6/4");

    // Mismo orden que syncCompanySelection: cambia la empresa y limpia la policy sincrónicamente.
    act(() => {
      useAuthStore.setState({ user: { companyId: "company-b" } as never });
      clearPrecisionPolicy();
    });
    expect(screen.queryByTestId("probe")).toBeNull();
    expect(screen.queryByTestId("error")).toBeNull();

    apiGet.mockResolvedValue(POLICY_B);
    await act(async () => {
      await loadPrecisionPolicy();
    });
    rerender(tree("company-b")); // como SessionBootstrap al marcar la policy lista para B
    expect(screen.getByTestId("probe").textContent).toBe("4/6");
    expect(screen.queryByTestId("error")).toBeNull();
    vi.mocked(console.error).mockRestore();
    useAuthStore.setState({ user: null, isAuthenticated: false });
  });
});
