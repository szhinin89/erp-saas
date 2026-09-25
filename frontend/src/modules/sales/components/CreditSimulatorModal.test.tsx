// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, render, screen, cleanup, fireEvent } from "@testing-library/react";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import { CreditSimulatorModal } from "./CreditSimulatorModal";
import type { CreditRow } from "../hooks/useSalesPage";

// SALES-DS-MONEY-12 — el footer "Total cuotas" de la simulación de crédito
// migró de <strong>$...</strong> a ZHMoneyValue emphasis="strong". El monto
// por cuota (fila editable) y el subtitle del modal (texto compuesto con el
// nombre del plazo de pago) se mantienen fuera de esta migración.

function renderModal(rows: CreditRow[]) {
  const onRowsChange = vi.fn();
  const onRecalculate = vi.fn();
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  const utils = render(
    <CreditSimulatorModal
      open
      amount={100}
      rows={rows}
      onRowsChange={onRowsChange}
      onRecalculate={onRecalculate}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />,
  );
  return { ...utils, onRowsChange, onRecalculate, onConfirm, onCancel };
}

afterEach(() => {
  cleanup();
});

describe("CreditSimulatorModal — total de cuotas migrado a ZHMoneyValue (SALES-DS-MONEY-12)", () => {
  it('el total de cuotas (50 + 50 = 100) usa ZHMoneyValue con emphasis="strong"', () => {
    const { container } = renderModal([
      { number: 1, dueDate: "2026-08-01", amount: 50 },
      { number: 2, dueDate: "2026-09-01", amount: 50 },
    ]);

    const footerCell = container.querySelector("tfoot .zh-table-cell--num");
    const moneyValue = footerCell?.querySelector(".zh-money-value");
    expect(moneyValue).toBeTruthy();
    expect(moneyValue?.textContent).toBe("$100.00");
    expect(moneyValue?.className).toContain("zh-money-value--strong");
  });

  it("el monto de cada cuota sigue siendo un input editable, no ZHMoneyValue", () => {
    renderModal([{ number: 1, dueDate: "2026-08-01", amount: 50 }]);

    const input = screen.getByDisplayValue("50.00");
    expect(input.tagName).toBe("INPUT");
  });

  it("no hay estilos inline en el total de cuotas", () => {
    const { container } = renderModal([
      { number: 1, dueDate: "2026-08-01", amount: 100 },
    ]);

    const moneyValue = container.querySelector("tfoot .zh-money-value");
    expect(moneyValue?.getAttribute("style")).toBeNull();
  });
});

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04G — el monto de cuota recibe el valor canónico (`defaultValue={row.amount}`)
 * y el propio ZhDecimalInput (precision="money") aplica la escala: sin formatMoney del consumidor.
 */
describe("CreditSimulatorModal — defaultValue canónico (04G)", () => {
  it("montaje con la escala de money (A=2, B=3 en un montaje nuevo); 0 → '0.00'", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    const a = renderModal([{ number: 1, dueDate: "2026-08-01", amount: 33.335 }, { number: 2, dueDate: "2026-09-01", amount: 0 }]);
    expect([...a.container.querySelectorAll<HTMLInputElement>("input.zh-numeric-input")].map((i) => i.value)).toEqual(["33.34", "0.00"]);
    cleanup();
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 });
    const b = renderModal([{ number: 1, dueDate: "2026-08-01", amount: 33.335 }]);
    expect(b.container.querySelector<HTMLInputElement>("input.zh-numeric-input")!.value).toBe("33.335");
  });

  it("focus→blur sin editar: texto intacto y el callback recibe el mismo monto que antes", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    const { container, onRowsChange } = renderModal([{ number: 1, dueDate: "2026-08-01", amount: 50 }]);
    const input = container.querySelector<HTMLInputElement>("input.zh-numeric-input")!;
    fireEvent.focus(input);
    fireEvent.blur(input);
    expect(input.value).toBe("50.00");
    expect(onRowsChange).toHaveBeenCalledWith([{ number: 1, dueDate: "2026-08-01", amount: 50 }]);
  });

  it("policy A→B sin remount: no reescribe el texto; la siguiente edición usa la escala B", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    const { container, onRowsChange } = renderModal([{ number: 1, dueDate: "2026-08-01", amount: 50 }]);
    const input = container.querySelector<HTMLInputElement>("input.zh-numeric-input")!;
    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 }));
    expect(input.value).toBe("50.00");
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "12.5" } });
    fireEvent.blur(input);
    expect(input.value).toBe("12.500");
    expect(onRowsChange).toHaveBeenLastCalledWith([{ number: 1, dueDate: "2026-08-01", amount: 12.5 }]);
  });
});
