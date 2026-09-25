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
 * - "legacy→03C: …"  expectativa que DEBE cambiar deliberadamente con el motor Decimal (se indica el
 *   valor futuro). No es una regla del ERP: es la deuda binaria de Number.toFixed.
 * - "legacy: …"      comportamiento actual que NO depende del motor (blur/dirty/round-trip).
 */
import { useEffect } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useForm, type RegisterOptions, type UseFormReturn } from "react-hook-form";
import { ZhDecimalInput } from "./ZhDecimalInput";

afterEach(() => cleanup());

const input = () => screen.getByLabelText("valor") as HTMLInputElement;

// ── A. value/defaultValue numérico con más escala que `decimals` ─────────────────────────────────

describe("A. defaultValue numérico con midpoint binario (patrón defaultValue de Purchases/Sales)", () => {
  it.each([
    [1.005, "1.00", "1.01"],
    [0.075, "0.07", "0.08"],
  ])("legacy→03C: %s con decimals=2 monta '%s' (futuro '%s'); focus/blur sin edición no emiten onChange", (value, legacy) => {
    const onChange = vi.fn();
    render(<ZhDecimalInput aria-label="valor" decimals={2} defaultValue={value} onChange={onChange} />);
    expect(input().value).toBe(legacy); // al montar ya está redondeado por Number.toFixed
    fireEvent.focus(input());
    expect(input().value).toBe(legacy);
    fireEvent.blur(input());
    expect(input().value).toBe(legacy); // ya canónico → blur no reformatea
    expect(onChange).not.toHaveBeenCalled();
  });

  it("legacy→03C: value controlado numérico 1.005 (decimals=2) renderiza '1.00' (futuro '1.01')", () => {
    render(<ZhDecimalInput aria-label="valor" decimals={2} value={1.005} onChange={() => {}} />);
    expect(input().value).toBe("1.00");
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
          decimals={decimals}
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
  it("legacy: register string — DOM '5' → '5.00', 1 onChange, valor '5.00', isDirty=true, touched=true", async () => {
    const rhf = renderRhf(5, 2);
    expect(input().value).toBe("5");
    await focusBlur();
    expect(input().value).toBe("5.00");
    expect(rhf.changes.count).toBe(1);
    expect(rhf.getValues()).toBe("5.00");
    expect(rhf.state()).toEqual({ isDirty: true, dirty: true, touched: true });
  });

  it("legacy: register + valueAsNumber — DOM '5' → '5.00', 1 onChange, valor 5, isDirty=false, touched=true", async () => {
    const rhf = renderRhf(5, 2, { valueAsNumber: true });
    await focusBlur();
    expect(input().value).toBe("5.00");
    expect(rhf.changes.count).toBe(1);
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
  it("legacy→03C: salePrice 1.005 con salesUnitPriceDecimals=2 → DOM '1.005' → '1.00' (futuro '1.01'); valor 1 (futuro 1.01); isDirty=true", async () => {
    const rhf = renderRhf(1.005, 2, ITEMS_REGISTER);
    expect(input().value).toBe("1.005"); // RHF escribe el número persistido tal cual
    await focusBlur();
    expect(input().value).toBe("1.00");
    expect(rhf.changes.count).toBe(1);
    expect(rhf.getValues()).toBe(1);
    expect(rhf.state()).toEqual({ isDirty: true, dirty: true, touched: true });
  });

  it("legacy→03C: stock mínimo 2.00005 con quantityDecimals=4 → '2.0000' (futuro '2.0001'); valor 2 (futuro 2.0001); isDirty=true", async () => {
    const rhf = renderRhf(2.00005, 4, ITEMS_REGISTER);
    await focusBlur();
    expect(input().value).toBe("2.0000");
    expect(rhf.getValues()).toBe(2);
    expect(rhf.state().isDirty).toBe(true);
  });

  it("legacy: valor con más escala SIN midpoint (1.2345, decimals=2) también se reescribe → '1.23', isDirty=true (igual con Decimal)", async () => {
    const rhf = renderRhf(1.2345, 2, ITEMS_REGISTER);
    await focusBlur();
    expect(input().value).toBe("1.23");
    expect(rhf.getValues()).toBe(1.23);
    expect(rhf.state().isDirty).toBe(true);
  });

  it("legacy: valor persistido dentro de la escala (12.5, decimals=2) → '12.50', valor 12.5, isDirty=false", async () => {
    const rhf = renderRhf(12.5, 2, ITEMS_REGISTER);
    await focusBlur();
    expect(input().value).toBe("12.50");
    expect(rhf.getValues()).toBe(12.5);
    expect(rhf.state().isDirty).toBe(false);
  });
});

// ── Purchases (CLOSED) — conversión calculada, defaultValue numérico sin redondear ───────────────

/** Réplica exacta de la cantidad de PurchasesPage con presentación vinculada. */
function PurchaseQtyHarness({
  quantity,
  factor,
  decimals,
  updateLine,
}: {
  quantity: number;
  factor: number;
  decimals: number;
  updateLine: (field: string, value: number) => void;
}) {
  return (
    <ZhDecimalInput
      aria-label="valor"
      density="compact"
      decimals={decimals}
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
  decimals,
  commit,
}: {
  baseUnitCost: number;
  decimals: number;
  commit: (entered: number) => void;
}) {
  return (
    <ZhDecimalInput
      aria-label="valor"
      density="compact"
      decimals={decimals}
      positiveOnly
      defaultValue={baseUnitCost}
      onBlur={(e) => commit(Number(e.target.value) || 0)}
    />
  );
}

describe("D. Purchases — cantidad/costo base calculados (focus → blur sin edición)", () => {
  it("legacy→03C: qty 2 × factor 1.000025 = 2.00005 (quantityDecimals=4) monta '2.0000' (futuro '2.0001'); el blur CONFIRMA quantity 1.99995… ≠ 2", () => {
    const updateLine = vi.fn();
    render(<PurchaseQtyHarness quantity={2} factor={1.000025} decimals={4} updateLine={updateLine} />);
    expect(input().value).toBe("2.0000");
    fireEvent.focus(input());
    fireEvent.blur(input());
    expect(updateLine).toHaveBeenCalledWith("quantityInBaseUom", 2); // futuro 2.0001
    expect(updateLine).toHaveBeenCalledWith("quantity", 2 / 1.000025); // futuro 2.0001 / 1.000025
    expect(2 / 1.000025).not.toBe(2);
  });

  it("legacy: el round-trip altera la cantidad AUNQUE no haya midpoint (3 × 1.00015 = 3.00045 → '3.0005' → 3.00004999…), igual con Decimal", () => {
    const updateLine = vi.fn();
    render(<PurchaseQtyHarness quantity={3} factor={1.00015} decimals={4} updateLine={updateLine} />);
    expect(input().value).toBe("3.0005");
    fireEvent.focus(input());
    fireEvent.blur(input());
    const committed = updateLine.mock.calls.find(([field]) => field === "quantity")![1] as number;
    expect(committed).toBe(3.0005 / 1.00015);
    expect(committed).not.toBe(3);
  });

  it("legacy: sin presentación con escala exacta (qty 2 × 1) el blur confirma el mismo valor", () => {
    const updateLine = vi.fn();
    render(<PurchaseQtyHarness quantity={2} factor={1} decimals={4} updateLine={updateLine} />);
    fireEvent.blur(input());
    expect(updateLine).toHaveBeenCalledWith("quantity", 2);
  });

  it("legacy→03C: costo base 0.30005 (purchaseUnitPriceDecimals=4) monta '0.3000' (futuro '0.3001'); el blur confirma 0.3 (futuro 0.3001)", () => {
    const commit = vi.fn();
    render(<PurchaseCostHarness baseUnitCost={0.30005} decimals={4} commit={commit} />);
    expect(input().value).toBe("0.3000");
    fireEvent.focus(input());
    fireEvent.blur(input());
    expect(commit).toHaveBeenCalledWith(0.3);
  });
});
