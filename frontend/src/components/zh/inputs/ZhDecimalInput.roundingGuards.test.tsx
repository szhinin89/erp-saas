// @vitest-environment jsdom
/**
 * ZH-DESIGN-SYSTEM-PRECISION-03C-PRE — GUARDAS DE CARACTERIZACIÓN antes de reemplazar Number.toFixed
 * por el motor Decimal (ROUND_HALF_UP) en ZhDecimalInput. CERO cambios productivos.
 *
 * Los harness reproducen EXACTAMENTE el contrato de consumidores reales sin tocar módulos cerrados:
 * - Items (FROZEN): ItemEditorForm `salePrice` e InventoryTab stock mín./máx. → RHF `register` con
 *   `valueAsNumber` + `setValueAs`, valor persistido numérico, `decimals` de la policy.
 * - Purchases (CLOSED): PurchasesPage cantidad/costo base por conversión → `defaultValue` numérico SIN
 *   redondear (qty × factor), `positiveOnly`, onBlur que confirma SIEMPRE `Number(e.target.value)`.
 *
 * Convención de nombres:
 * - "03C: …"         expectativa actualizada deliberadamente al motor Decimal (ROUND_HALF_UP) en 03C
 *   (antes "legacy→03C"; el valor previo de Number.toFixed se indica como "antes").
 * - "legacy: …"      comportamiento actual que NO depende del motor (round-trip de Compras).
 * - "03D: …"         contrato "entrar y salir sin modificar no es una edición": sin edición no se
 *   reescribe el valor ni se emite onChange sintético (antes sí; el valor previo se indica como "antes").
 */
import { useEffect } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useForm, type RegisterOptions, type UseFormReturn } from "react-hook-form";
import { ZhDecimalInput } from "./ZhDecimalInput";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

afterEach(() => cleanup());

const input = () => screen.getByLabelText("valor") as HTMLInputElement;

// ── A. value/defaultValue numérico con más escala que `decimals` ─────────────────────────────────

describe("A. defaultValue numérico con midpoint binario (patrón defaultValue de Purchases/Sales)", () => {
  it.each([
    [1.005, "1.01", "1.00"],
    [0.075, "0.08", "0.07"],
  ])("03C: %s con decimals=2 monta '%s' (antes '%s'); focus/blur sin edición no emiten onChange", (value, legacy) => {
    const onChange = vi.fn();
    render(<ZhDecimalInput aria-label="valor" precision="money" defaultValue={value} onChange={onChange} />);
    expect(input().value).toBe(legacy); // al montar ya está redondeado por el motor Decimal
    fireEvent.focus(input());
    expect(input().value).toBe(legacy);
    fireEvent.blur(input());
    expect(input().value).toBe(legacy); // ya canónico → blur no reformatea
    expect(onChange).not.toHaveBeenCalled();
  });

  it("03C: value controlado numérico 1.005 (decimals=2) renderiza '1.01' (antes '1.00')", () => {
    render(<ZhDecimalInput aria-label="valor" precision="money" value={1.005} onChange={() => {}} />);
    expect(input().value).toBe("1.01");
  });
});

// ── B/C. RHF: focus → blur sin edición ───────────────────────────────────────────────────────────

type RhfResult = {
  getValues: () => unknown;
  state: () => { isDirty: boolean; dirty: boolean; touched: boolean };
  changes: { count: number };
};

/** Harness RHF: el formState se lee en render (suscripción real del proxy de RHF). */
function renderRhf(defaultValue: unknown, decimals: number, options?: RegisterOptions<{ x: unknown }, "x">): RhfResult {
  // 06 — `decimals` es la escala que entrega la policy (el input declara precision="quantity").
  setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: decimals });
  const changes = { count: 0 };
  let form: UseFormReturn<{ x: unknown }> | null = null;
  const expose = (f: UseFormReturn<{ x: unknown }>) => {
    form = f;
  };
  function Harness() {
    const form = useForm<{ x: unknown }>({ defaultValues: { x: defaultValue } });
    useEffect(() => expose(form), [form]);
    const reg = form.register("x", options);
    const { isDirty, dirtyFields, touchedFields } = form.formState;
    return (
      <>
        <ZhDecimalInput
          aria-label="valor"
          precision="quantity"
          {...reg}
          onChange={(e) => {
            changes.count++;
            return reg.onChange(e);
          }}
        />
        <output data-testid="state">
          {JSON.stringify({ isDirty, dirty: !!dirtyFields.x, touched: !!touchedFields.x })}
        </output>
      </>
    );
  }
  render(<Harness />);
  return {
    getValues: () => form?.getValues("x"),
    state: () => JSON.parse(screen.getByTestId("state").textContent!),
    changes,
  };
}

async function focusBlur() {
  await act(async () => {
    fireEvent.focus(input());
  });
  await act(async () => {
    fireEvent.blur(input());
  });
}

describe("B. RHF focus → blur sin edición, valor inicial no canónico (5, decimals=2)", () => {
  it("03D: register string — DOM '5' se conserva (antes '5.00'), 0 onChange (antes 1), valor '5', isDirty=false (antes true), touched=true", async () => {
    const rhf = renderRhf(5, 2);
    expect(input().value).toBe("5");
    await focusBlur();
    expect(input().value).toBe("5");
    expect(rhf.changes.count).toBe(0);
    expect(rhf.getValues()).toBe("5"); // el onBlur propio de RHF relee el DOM (sin normalizar)
    expect(rhf.state()).toEqual({ isDirty: false, dirty: false, touched: true });
  });

  it("03D: register + valueAsNumber — DOM '5' se conserva (antes '5.00'), 0 onChange (antes 1), valor 5, isDirty=false, touched=true", async () => {
    const rhf = renderRhf(5, 2, { valueAsNumber: true });
    await focusBlur();
    expect(input().value).toBe("5");
    expect(rhf.changes.count).toBe(0);
    expect(rhf.getValues()).toBe(5);
    expect(rhf.state()).toEqual({ isDirty: false, dirty: false, touched: true });
  });

  it("legacy: valor ya canónico ('5.00' string) — blur no emite onChange ni ensucia", async () => {
    const rhf = renderRhf("5.00", 2);
    await focusBlur();
    expect(rhf.changes.count).toBe(0);
    expect(rhf.state()).toEqual({ isDirty: false, dirty: false, touched: true });
  });
});

// ── Items (FROZEN) — contrato exacto: register + valueAsNumber + setValueAs ──────────────────────

const ITEMS_REGISTER = {
  valueAsNumber: true,
  setValueAs: (v: unknown) => (v === "" ? null : Number(v)),
} as RegisterOptions<{ x: unknown }, "x">;

describe("C. Items — valor persistido con más escala que la policy (focus → blur sin edición)", () => {
  it("03D: salePrice persistido 1.005 (decimals=2) se CONSERVA al visitar el campo (antes '1.01' y dirty); isDirty=false, touched=true", async () => {
    const rhf = renderRhf(1.005, 2, ITEMS_REGISTER);
    expect(input().value).toBe("1.005"); // RHF escribe el número persistido tal cual
    await focusBlur();
    expect(input().value).toBe("1.005");
    expect(rhf.changes.count).toBe(0);
    expect(rhf.getValues()).toBe(1.005);
    expect(rhf.state()).toEqual({ isDirty: false, dirty: false, touched: true });
  });

  it("03D: stock mínimo persistido 2.00005 (decimals=4) se CONSERVA al visitar el campo (antes '2.0001' y dirty)", async () => {
    const rhf = renderRhf(2.00005, 4, ITEMS_REGISTER);
    await focusBlur();
    expect(input().value).toBe("2.00005");
    expect(rhf.getValues()).toBe(2.00005);
    expect(rhf.state().isDirty).toBe(false);
  });

  it("03D: valor con más escala sin midpoint (1.2345, decimals=2) ya no se reescribe al visitar (antes '1.23' y dirty)", async () => {
    const rhf = renderRhf(1.2345, 2, ITEMS_REGISTER);
    await focusBlur();
    expect(input().value).toBe("1.2345");
    expect(rhf.getValues()).toBe(1.2345);
    expect(rhf.state().isDirty).toBe(false);
  });

  it("03D: valor persistido dentro de la escala (12.5, decimals=2) se conserva '12.5' (antes '12.50'), isDirty=false", async () => {
    const rhf = renderRhf(12.5, 2, ITEMS_REGISTER);
    await focusBlur();
    expect(input().value).toBe("12.5");
    expect(rhf.getValues()).toBe(12.5);
    expect(rhf.state().isDirty).toBe(false);
  });
});

describe("C2. Items — edición REAL sigue normalizando con el motor Decimal (03D)", () => {
  it("03D: persistido 1.005 → el usuario escribe 1.015 → blur '1.02' (ROUND_HALF_UP), valor 1.02, isDirty=true", async () => {
    const rhf = renderRhf(1.005, 2, ITEMS_REGISTER);
    await act(async () => {
      fireEvent.focus(input());
    });
    await act(async () => {
      fireEvent.change(input(), { target: { value: "1.015" } });
    });
    await act(async () => {
      fireEvent.blur(input());
    });
    expect(input().value).toBe("1.02");
    expect(rhf.changes.count).toBe(2); // edición del usuario + normalización del blur
    expect(rhf.getValues()).toBe(1.02);
    expect(rhf.state()).toEqual({ isDirty: true, dirty: true, touched: true });
  });
});

// ── Purchases (CLOSED) — conversión calculada, defaultValue numérico sin redondear ───────────────

/** Réplica exacta de la cantidad de PurchasesPage con presentación vinculada. */
function PurchaseQtyHarness({
  quantity,
  factor,
  updateLine,
}: {
  quantity: number;
  factor: number;
  updateLine: (field: string, value: number) => void;
}) {
  return (
    <ZhDecimalInput
      aria-label="valor"
      density="compact"
      precision="quantity"
      positiveOnly
      defaultValue={quantity * factor}
      onBlur={(e) => {
        const entered = Number(e.target.value) || 0;
        updateLine("quantity", entered / factor || 1);
        updateLine("quantityInBaseUom", entered);
      }}
    />
  );
}

/** Réplica del costo base (sin la fórmula inversa, que no depende del input). */
function PurchaseCostHarness({
  baseUnitCost,
  commit,
}: {
  baseUnitCost: number;
  commit: (entered: number) => void;
}) {
  return (
    <ZhDecimalInput
      aria-label="valor"
      density="compact"
      precision="purchaseUnitPrice"
      positiveOnly
      defaultValue={baseUnitCost}
      onBlur={(e) => commit(Number(e.target.value) || 0)}
    />
  );
}

describe("D. Purchases — cantidad/costo base calculados (focus → blur sin edición)", () => {
  it("03C: qty 2 × factor 1.000025 = 2.00005 (quantityDecimals=4) monta '2.0001' (antes '2.0000'); el blur SIGUE confirmando quantity 2.00005… ≠ 2 (deriva de Compras, fuera de 03C)", () => {
    const updateLine = vi.fn();
    render(<PurchaseQtyHarness quantity={2} factor={1.000025} updateLine={updateLine} />);
    expect(input().value).toBe("2.0001");
    fireEvent.focus(input());
    fireEvent.blur(input());
    expect(updateLine).toHaveBeenCalledWith("quantityInBaseUom", 2.0001); // antes 2
    expect(updateLine).toHaveBeenCalledWith("quantity", 2.0001 / 1.000025); // antes 2 / 1.000025
    expect(2.0001 / 1.000025).not.toBe(2);
  });

  it("legacy: el round-trip altera la cantidad AUNQUE no haya midpoint (3 × 1.00015 = 3.00045 → '3.0005' → 3.00004999…), igual con Decimal", () => {
    const updateLine = vi.fn();
    render(<PurchaseQtyHarness quantity={3} factor={1.00015} updateLine={updateLine} />);
    expect(input().value).toBe("3.0005");
    fireEvent.focus(input());
    fireEvent.blur(input());
    const committed = updateLine.mock.calls.find(([field]) => field === "quantity")![1] as number;
    expect(committed).toBe(3.0005 / 1.00015);
    expect(committed).not.toBe(3);
    // 03D: el INPUT ya no reescribe nada (DOM intacto, sin onChange sintético); la deriva la produce
    // el onBlur del CONSUMIDOR de Compras, que confirma siempre → requiere reabrir Purchases.
    expect(input().value).toBe("3.0005");
  });

  it("legacy: sin presentación con escala exacta (qty 2 × 1) el blur confirma el mismo valor", () => {
    const updateLine = vi.fn();
    render(<PurchaseQtyHarness quantity={2} factor={1} updateLine={updateLine} />);
    fireEvent.blur(input());
    expect(updateLine).toHaveBeenCalledWith("quantity", 2);
  });

  it("03C: costo base 0.30005 (purchaseUnitPriceDecimals=4) monta '0.3001' (antes '0.3000'); el blur confirma 0.3001 (antes 0.3)", () => {
    const commit = vi.fn();
    render(<PurchaseCostHarness baseUnitCost={0.30005} commit={commit} />);
    expect(input().value).toBe("0.3001");
    fireEvent.focus(input());
    fireEvent.blur(input());
    expect(commit).toHaveBeenCalledWith(0.3001);
  });
});
