// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { useState } from "react";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { ZHConfirmModal } from "./ZHConfirmModal";

/**
 * Accesibilidad del diálogo de confirmación oficial (BP-CONSOLIDATION-01 Etapa 4): nombre y descripción
 * ARIA, foco inicial en la acción menos destructiva, Escape cancela, Tab no sale del diálogo y el foco
 * vuelve a quien lo abrió. El comportamiento por clic (confirmar/cancelar/fondo) no cambia.
 */

function Harness({ onConfirm = vi.fn(), onCancel = vi.fn() }: { onConfirm?: () => void; onCancel?: () => void }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button onClick={() => setOpen(true)}>Abrir</button>
      <button>Otro control</button>
      <ZHConfirmModal
        open={open}
        title="Desactivar cliente"
        message="El cliente dejará de estar disponible."
        confirmLabel="Desactivar"
        cancelLabel="Cancelar"
        variant="danger"
        onConfirm={() => { onConfirm(); setOpen(false); }}
        onCancel={() => { onCancel(); setOpen(false); }}
      />
    </>
  );
}

afterEach(cleanup);

function openDialog() {
  const opener = screen.getByRole("button", { name: "Abrir" });
  opener.focus();
  fireEvent.click(opener);
  return opener;
}

describe("ZHConfirmModal — accesibilidad", () => {
  it("el diálogo tiene nombre (título) y descripción (mensaje)", () => {
    render(<Harness />);
    openDialog();
    const dialog = screen.getByRole("alertdialog", { name: "Desactivar cliente" });
    const described = dialog.getAttribute("aria-describedby");
    expect(document.getElementById(described!)?.textContent).toBe("El cliente dejará de estar disponible.");
  });

  it("el foco inicial va a «Cancelar» (acción menos destructiva) dentro del diálogo", () => {
    render(<Harness />);
    openDialog();
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Cancelar" }));
  });

  it("Escape cancela una sola vez y devuelve el foco a quien abrió el diálogo", () => {
    const onCancel = vi.fn();
    render(<Harness onCancel={onCancel} />);
    const opener = openDialog();
    fireEvent.keyDown(document.activeElement!, { key: "Escape" });
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole("alertdialog")).toBeNull();
    expect(document.activeElement).toBe(opener);
  });

  it("Tab y Shift+Tab ciclan dentro del diálogo y no llegan al contenido de fondo", () => {
    render(<Harness />);
    openDialog();
    const cancel = screen.getByRole("button", { name: "Cancelar" });
    const confirm = screen.getByRole("button", { name: "Desactivar" });
    expect(document.activeElement).toBe(cancel);
    confirm.focus();
    fireEvent.keyDown(confirm, { key: "Tab" });
    expect(document.activeElement).toBe(cancel);
    fireEvent.keyDown(cancel, { key: "Tab", shiftKey: true });
    expect(document.activeElement).toBe(confirm);
    // Foco fuera del diálogo (p. ej. por clic en el fondo): Tab lo devuelve dentro.
    screen.getByRole("button", { name: "Otro control" }).focus();
    fireEvent.keyDown(document.activeElement!, { key: "Tab" });
    expect(document.activeElement).toBe(cancel);
  });

  it("el icono decorativo no forma parte del contenido accesible", () => {
    render(<Harness />);
    openDialog();
    expect(screen.getByRole("alertdialog").querySelector(".zh-confirm-icon")?.getAttribute("aria-hidden")).toBe("true");
  });
});

describe("ZHConfirmModal — comportamiento por clic sin cambios", () => {
  it("confirmar y cancelar llaman a su callback; el clic en el fondo cancela", () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    const { container, unmount } = render(<Harness onConfirm={onConfirm} onCancel={onCancel} />);
    openDialog();
    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onCancel).not.toHaveBeenCalled();
    unmount();

    render(<Harness onConfirm={onConfirm} onCancel={onCancel} />);
    openDialog();
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(onCancel).toHaveBeenCalledTimes(1);
    openDialog();
    fireEvent.click(document.querySelector(".zh-modal-overlay")!);
    expect(onCancel).toHaveBeenCalledTimes(2);
    expect(container).toBeTruthy();
  });
});
