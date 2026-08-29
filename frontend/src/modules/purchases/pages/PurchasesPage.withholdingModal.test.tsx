// @vitest-environment jsdom
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import { afterEach } from "vitest";
import { ZHPromptModal } from "../../../components/zh/ZHConfirmModal";
import { buildWithholdingIssueMessage } from "../utils/withholdingMessages";

/**
 * CRITICAL-CONFIRMATIONS-PURCHASES-EXPENSES-03 — cubre la configuración real del modal "Emitir
 * retención" tal como PurchasesPage.tsx la usa (mismo componente ZHPromptModal, mismo mensaje vía
 * buildWithholdingIssueMessage, mismo label/placeholder/onConfirm/onCancel), sin montar la
 * pantalla completa de Compras (3200+ líneas, fuera de alcance mockear su superficie completa).
 * PurchasesPage.tsx en sí solo cambia el prop `message` de este mismo componente — no se creó
 * ningún componente DS nuevo.
 */

afterEach(() => cleanup());

function renderWithholdingIssueModal(onConfirm: (epId: string) => void) {
  return render(
    <ZHPromptModal
      open
      title="Emitir retención"
      variant="warning"
      message={buildWithholdingIssueMessage(
        "001-001-000000123",
        "Proveedor Uno",
        42.5,
      )}
      label="ID del punto de emisión"
      placeholder="ID del punto de emisión"
      confirmLabel="Emitir"
      onCancel={vi.fn()}
      onConfirm={onConfirm}
    />,
  );
}

describe("Modal 'Emitir retención' — resumen y comportamiento (CRITICAL-CONFIRMATIONS-PURCHASES-EXPENSES-03)", () => {
  it("muestra el resumen/advertencia suficiente antes de emitir", () => {
    renderWithholdingIssueModal(vi.fn());

    expect(screen.getByText(/001-001-000000123/)).toBeTruthy();
    expect(screen.getByText(/Proveedor Uno/)).toBeTruthy();
    expect(screen.getByText(/\$42\.50/)).toBeTruthy();
    expect(screen.getByText(/SRI/)).toBeTruthy();
  });

  it("conserva la selección de punto de emisión escrita por el usuario y la pasa al confirmar", () => {
    const onConfirm = vi.fn();
    renderWithholdingIssueModal(onConfirm);

    const input = screen.getByPlaceholderText("ID del punto de emisión");
    fireEvent.change(input, { target: { value: "001-001" } });
    expect((input as HTMLInputElement).value).toBe("001-001");

    fireEvent.click(screen.getByRole("button", { name: "Emitir" }));
    expect(onConfirm).toHaveBeenCalledWith("001-001");
  });

  it("si se cancela, no llama onConfirm (no se emite)", () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    render(
      <ZHPromptModal
        open
        title="Emitir retención"
        variant="warning"
        message={buildWithholdingIssueMessage("001-001-000000123", "Proveedor Uno", 42.5)}
        label="ID del punto de emisión"
        placeholder="ID del punto de emisión"
        confirmLabel="Emitir"
        onCancel={onCancel}
        onConfirm={onConfirm}
      />,
    );

    fireEvent.change(screen.getByPlaceholderText("ID del punto de emisión"), {
      target: { value: "001-001" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(onCancel).toHaveBeenCalled();
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
