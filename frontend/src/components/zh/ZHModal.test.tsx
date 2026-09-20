// @vitest-environment jsdom
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import { useState } from "react";
import { afterEach } from "vitest";
import { ZHModal } from "./ZHModal";

/**
 * SALES-QUICK-CUSTOMER-MODAL-INPUT-FIX-07: reproduce el bug real de "Crear Cliente" — un
 * consumidor típico pasa un `onClose` inline (`onCancel={() => setModal(false)}`), una función
 * NUEVA en cada render. Antes del fix, el efecto de ZHModal dependía de `[open, onClose]`, así
 * que cualquier re-render del padre (incluido el que dispara cada tecla al escribir en un input
 * controlado dentro del modal) volvía a ejecutar `firstFocusable?.focus()`, robándole el foco al
 * campo donde el usuario estaba escribiendo.
 */
function TypingHarness() {
  const [open, setOpen] = useState(true);
  const [name, setName] = useState("");
  return (
    <ZHModal open={open} onClose={() => setOpen(false)} title="Crear Cliente">
      <select data-testid="tipo-id" defaultValue="">
        <option value="" />
        <option value="05">Cédula</option>
      </select>
      <input
        data-testid="nombre"
        value={name}
        onChange={(e) => setName(e.target.value)}
      />
    </ZHModal>
  );
}

afterEach(cleanup);

describe("SALES-QUICK-CUSTOMER-MODAL-INPUT-FIX-07 — ZHModal no roba el foco al escribir", () => {
  it("el foco permanece en el input mientras se escribe, aunque onClose sea una función nueva en cada render", () => {
    render(<TypingHarness />);
    const input = screen.getByTestId("nombre") as HTMLInputElement;
    input.focus();
    expect(document.activeElement).toBe(input);

    fireEvent.change(input, { target: { value: "J" } });
    expect(document.activeElement).toBe(input);

    fireEvent.change(input, { target: { value: "Ju" } });
    expect(document.activeElement).toBe(input);

    fireEvent.change(input, { target: { value: "Juan Pérez" } });
    expect(document.activeElement).toBe(input);
    expect(input.value).toBe("Juan Pérez");
  });

  it("Backspace/Delete y espacios funcionan sin que el foco se pierda", () => {
    render(<TypingHarness />);
    const input = screen.getByTestId("nombre") as HTMLInputElement;
    input.focus();

    fireEvent.change(input, { target: { value: "Ana" } });
    fireEvent.change(input, { target: { value: "Ana " } });
    fireEvent.change(input, { target: { value: "Ana M" } });
    fireEvent.keyDown(input, { key: "Backspace" });
    fireEvent.change(input, { target: { value: "Ana " } });

    expect(document.activeElement).toBe(input);
    expect(input.value).toBe("Ana ");
  });

  it("caracteres especiales (ñ, tildes, guion, arroba) no rompen el foco", () => {
    render(<TypingHarness />);
    const input = screen.getByTestId("nombre") as HTMLInputElement;
    input.focus();

    fireEvent.change(input, { target: { value: "Muñoz-Peña S.A. @cliente" } });

    expect(document.activeElement).toBe(input);
    expect(input.value).toBe("Muñoz-Peña S.A. @cliente");
  });

  it("pegar con Ctrl+V (paste event) mantiene el foco en el input", () => {
    render(<TypingHarness />);
    const input = screen.getByTestId("nombre") as HTMLInputElement;
    input.focus();

    fireEvent.paste(input);
    fireEvent.change(input, { target: { value: "Cliente Pegado S.A." } });

    expect(document.activeElement).toBe(input);
    expect(input.value).toBe("Cliente Pegado S.A.");
  });

  it("Escape sigue cerrando el modal (no se rompe por leer onClose desde un ref)", () => {
    const onClose = vi.fn();
    render(
      <ZHModal open={true} onClose={onClose} title="Crear Cliente">
        <input data-testid="nombre" />
      </ZHModal>,
    );

    fireEvent.keyDown(window, { key: "Escape" });

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("Escape usa el onClose MÁS RECIENTE, no uno obsoleto del primer render", () => {
    const onCloseFirst = vi.fn();
    const onCloseSecond = vi.fn();
    const { rerender } = render(
      <ZHModal open={true} onClose={onCloseFirst} title="Crear Cliente">
        <input data-testid="nombre" />
      </ZHModal>,
    );
    rerender(
      <ZHModal open={true} onClose={onCloseSecond} title="Crear Cliente">
        <input data-testid="nombre" />
      </ZHModal>,
    );

    fireEvent.keyDown(window, { key: "Escape" });

    expect(onCloseFirst).not.toHaveBeenCalled();
    expect(onCloseSecond).toHaveBeenCalledTimes(1);
  });

  it("re-render del padre por un cambio ajeno al modal no reenfoca el primer control", () => {
    // Simula un padre (como SalesPage) que se re-renderiza por CUALQUIER motivo mientras el
    // modal está abierto — el foco del usuario, si ya estaba en un campo del modal, no debe
    // saltar de vuelta al primer control en cada uno de esos re-renders.
    function ParentWithUnrelatedState() {
      const [, forceRerender] = useState(0);
      return (
        <div>
          <button onClick={() => forceRerender((n) => n + 1)}>rerender</button>
          <ZHModal open={true} onClose={() => {}} title="Crear Cliente">
            <select data-testid="tipo-id" defaultValue="" />
            <input data-testid="nombre" defaultValue="" />
          </ZHModal>
        </div>
      );
    }
    render(<ParentWithUnrelatedState />);
    const input = screen.getByTestId("nombre") as HTMLInputElement;
    input.focus();
    expect(document.activeElement).toBe(input);

    fireEvent.click(screen.getByText("rerender"));

    expect(document.activeElement).toBe(input);
  });

  it("cerrar y reabrir el modal vuelve a enfocar el primer control (comportamiento intencional al ABRIR)", () => {
    function ToggleHarness() {
      const [open, setOpen] = useState(true);
      return (
        <div>
          <button onClick={() => setOpen(false)}>cerrar</button>
          <button onClick={() => setOpen(true)}>abrir</button>
          <ZHModal open={open} onClose={() => setOpen(false)} title="Crear Cliente">
            <select data-testid="tipo-id" defaultValue="" />
            <input data-testid="nombre" defaultValue="" />
          </ZHModal>
        </div>
      );
    }
    render(<ToggleHarness />);
    // El primer elemento focusable dentro del diálogo es el botón "Cerrar" del header (precede a
    // los children en el DOM) — el test fija el comportamiento real, no una suposición sobre cuál
    // control específico es "el primero".
    const closeButton = screen.getByLabelText("Cerrar");
    expect(document.activeElement).toBe(closeButton);

    fireEvent.click(screen.getByText("cerrar"));
    fireEvent.click(screen.getByText("abrir"));

    expect(document.activeElement).toBe(screen.getByLabelText("Cerrar"));
  });
});
