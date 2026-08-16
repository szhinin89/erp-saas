// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { PaymentDetailModal } from "./PaymentDetailModal";

afterEach(() => {
  cleanup();
});

function renderModal() {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  const utils = render(
    <PaymentDetailModal
      open
      methodName="Transferencia"
      detailType="Transfer"
      initialRows={[
        { _k: 1, amount: 10, transfer: { bankName: "Pichincha" } },
        { _k: 2, amount: 20, transfer: { bankName: "Guayaquil" } },
      ]}
      initialKey={3}
      available={100}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />,
  );
  return { ...utils, onConfirm, onCancel };
}

describe("PaymentDetailModal — botón eliminar fila (SALES-DS-PAYMENT-REMOVE-03)", () => {
  it("renderiza un botón eliminar por cada fila", () => {
    renderModal();
    expect(screen.getAllByTitle("Eliminar")).toHaveLength(2);
  });

  it('usa el ícono de basurero ("delete"), no "close" (SALES-DS-PAYMENT-REMOVE-03A)', () => {
    renderModal();
    const removeButtons = screen.getAllByTitle("Eliminar");
    removeButtons.forEach((btn) => {
      expect(btn.querySelector(".material-symbols-outlined")?.textContent).toBe(
        "delete",
      );
      expect(btn.textContent).not.toContain("close");
    });
  });

  it("click en eliminar remueve solo la fila correspondiente", () => {
    renderModal();
    const removeButtons = screen.getAllByTitle("Eliminar");
    fireEvent.click(removeButtons[0]);

    expect(screen.getAllByTitle("Eliminar")).toHaveLength(1);
    expect(screen.getByDisplayValue("Guayaquil")).toBeTruthy();
    expect(screen.queryByDisplayValue("Pichincha")).toBeNull();
  });

  it('no muestra texto visible "Eliminar" (icon-only)', () => {
    renderModal();
    expect(screen.queryByText("Eliminar")).toBeNull();
  });

  it("conserva aria-label/title Eliminar", () => {
    renderModal();
    const btn = screen.getAllByTitle("Eliminar")[0];
    expect(btn.getAttribute("aria-label")).toBe("Eliminar");
    expect(btn.getAttribute("title")).toBe("Eliminar");
  });

  it("no hay estilos inline en el botón eliminar", () => {
    renderModal();
    screen.getAllByTitle("Eliminar").forEach((btn) => {
      expect(btn.getAttribute("style")).toBeNull();
    });
  });
});
