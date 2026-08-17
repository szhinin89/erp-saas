// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
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
