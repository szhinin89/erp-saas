// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ExpenseReceptionMismatchNotice, type ReceptionMismatch } from "./ExpenseReceptionMismatch";

/**
 * EXPENSE-UI-02 — advertencia de diferencia contra el XML: indicador compacto desde el inicio; toast
 * oficial solo tras ingresar importes, una vez por aparición; retiro automático al cuadrar.
 */
const toast = vi.hoisted(() => ({ warning: vi.fn() }));
vi.mock("../../../lib/messages", () => ({ message: { warning: toast.warning } }));

const TOAST_TEXT = "El total no coincide con el XML.";
// Gasto recién abierto desde XML: conceptos sin completar (total calculado 0) frente a 103.50.
const FRESH: ReceptionMismatch = { calculatedTotal: 0, receivedTotal: 103.5, receivedVat: 13.5, diff: -103.5 };
const ENTERED: ReceptionMismatch = { calculatedTotal: 94.5, receivedTotal: 103.5, receivedVat: 13.5, diff: -9 };
const ui = (m: ReceptionMismatch | null) => <ExpenseReceptionMismatchNotice mismatch={m} />;
const show = (m: ReceptionMismatch | null) => render(ui(m), { wrapper: I18nProvider });
const badge = () => screen.queryByRole("button", { name: "Total distinto al XML" });

beforeEach(() => vi.clearAllMocks());
afterEach(cleanup);

describe("ExpenseReceptionMismatchNotice", () => {
  it("con totales coincidentes no muestra indicador ni avisa", () => {
    const { container } = show(null);
    expect(container.textContent).toBe("");
    expect(toast.warning).not.toHaveBeenCalled();
  });

  it("al abrir con conceptos sin completar: indicador visible y SIN toast", () => {
    show(FRESH);
    expect(badge()).toBeTruthy();
    expect(toast.warning).not.toHaveBeenCalled();
  });

  it("tras ingresar importes que no coinciden: un toast; el indicador persiste", () => {
    const { rerender } = show(FRESH);
    rerender(ui(ENTERED));
    expect(toast.warning).toHaveBeenCalledTimes(1);
    expect(toast.warning).toHaveBeenCalledWith(TOAST_TEXT);
    expect(badge()).toBeTruthy();
  });

  it("no repite el toast aunque cambien los importes, se borren o se vuelva a renderizar", () => {
    const { rerender } = show(FRESH);
    for (let i = 1; i <= 5; i++) rerender(ui({ ...ENTERED, calculatedTotal: 90 + i }));
    rerender(ui(FRESH)); // el usuario vacía los importes: la diferencia sigue, sin aviso nuevo
    rerender(ui({ ...ENTERED, calculatedTotal: 50 }));
    expect(toast.warning).toHaveBeenCalledTimes(1);
    expect(badge()).toBeTruthy();
  });

  it("se retira solo al coincidir y una diferencia nueva tras ingresar importes avisa de nuevo", () => {
    const { rerender } = show(ENTERED);
    expect(toast.warning).toHaveBeenCalledTimes(1);
    rerender(ui(null));
    expect(badge()).toBeNull();
    expect(toast.warning).toHaveBeenCalledTimes(1);
    rerender(ui(ENTERED));
    expect(toast.warning).toHaveBeenCalledTimes(2);
  });

  it("el detalle (total XML, IVA XML y total calculado) se abre con el teclado y se cierra con Escape", () => {
    show(ENTERED);
    const trigger = badge()!;
    expect(trigger.tagName).toBe("BUTTON"); // alcanzable con Tab; Enter/Espacio activan el click nativo
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(trigger);
    expect(trigger.getAttribute("aria-expanded")).toBe("true");
    const dialog = screen.getByRole("dialog");
    expect(dialog.textContent).toContain("Total XML recibido: 103.50");
    expect(dialog.textContent).toContain("IVA XML recibido: 13.50");
    expect(dialog.textContent).toContain("Total calculado en pantalla: 94.50");
    fireEvent.keyDown(window, { key: "Escape" });
    expect(screen.queryByRole("dialog")).toBeNull();
  });
});
