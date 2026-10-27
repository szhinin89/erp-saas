// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import { PaymentScheduleSection } from "./PurchasesPage";

// PURCHASE-PAYABLE-E2E-01 — tras Regenerar (o cargar el borrador) la cuota cambia de monto con el
// input ya montado: debe mostrarse el monto vigente y entrar/salir sin editar no debe re-confirmar
// el valor anterior (dejaba "Las cuotas no cuadran" y bloqueaba Confirmar compra).

type Ctx = Parameters<typeof PaymentScheduleSection>[0]["ctx"];
type Row = { number: number; dueDate: string; amount: number; notes: string };

function buildCtx(rows: Row[], updateScheduleRow = vi.fn()) {
  return {
    hasPersistedSchedule: false,
    ptLoaded: true,
    isDraft: true,
    saving: false,
    fieldDisabled: false,
    formWatch: { issueDate: "2026-10-27" },
    ptInstallments: rows.length,
    ptDaysBetween: 30,
    ptRows: rows,
    ptRowsSum: rows.reduce((s, r) => s + r.amount, 0),
    ptMismatch: false,
    localTotal: 115,
    editing: null,
    regenerateSchedule: vi.fn(),
    addInstallment: vi.fn(),
    removeInstallment: vi.fn(),
    setPtInstallments: vi.fn(),
    setPtDaysBetween: vi.fn(),
    updateScheduleRow,
  } as unknown as Ctx;
}

function ui(ctx: Ctx) {
  return (
    <MemoryRouter>
      <I18nProvider>
        <PaymentScheduleSection ctx={ctx} />
      </I18nProvider>
    </MemoryRouter>
  );
}

const amountInput = (container: HTMLElement) =>
  container.querySelector<HTMLInputElement>("input.pf-schedule-input--amount")!;

const row = (amount: number): Row => ({ number: 1, dueDate: "2026-11-26", amount, notes: "" });

beforeEach(() => setPrecisionPolicyForTests(TEST_PRECISION_POLICY));
afterEach(cleanup);

describe("PaymentScheduleSection — monto de cuota", () => {
  it("muestra el monto regenerado aunque el input ya estuviera montado", () => {
    const { container, rerender } = render(ui(buildCtx([row(0)])));
    expect(amountInput(container).value).toBe("0.00");

    rerender(ui(buildCtx([row(115)])));

    expect(amountInput(container).value).toBe("115.00");
  });

  it("entrar y salir sin editar no re-confirma el monto", () => {
    const update = vi.fn();
    const { container, rerender } = render(ui(buildCtx([row(0)], update)));
    rerender(ui(buildCtx([row(115)], update)));

    fireEvent.focus(amountInput(container));
    fireEvent.blur(amountInput(container));

    expect(update).not.toHaveBeenCalled();
  });

  it("confirma el monto editado por el usuario", () => {
    const update = vi.fn();
    const { container } = render(ui(buildCtx([row(115)], update)));
    const input = amountInput(container);

    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "60" } });
    fireEvent.blur(input);

    expect(update).toHaveBeenCalledWith(0, "amount", 60);
  });
});
