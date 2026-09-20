// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { SalesInvoiceDetailsSection } from "./SalesInvoiceDetailsSection";

// SALES-QUICK-CUSTOMER-MODAL-INPUT-FIX-07: el atajo global F2 (reenfocar el buscador de
// productos) nunca debe robar el foco de un control editable — p. ej. mientras el usuario
// escribe en el modal "Crear Cliente" abierto sobre la página de Ventas.

vi.mock("../api/invoiceItemSearchService", () => ({
  invoiceItemSearchService: { search: vi.fn().mockResolvedValue([]) },
}));

afterEach(() => {
  cleanup();
});

function renderSection() {
  return render(
    <SalesInvoiceDetailsSection
      lines={[]}
      readOnly={false}
      disabled={false}
      onRemoveLine={vi.fn()}
      onUpdateLine={vi.fn()}
      onAddItemLine={vi.fn().mockResolvedValue(undefined)}
      onUpdateLineWarehouse={vi.fn()}
      warehouses={[]}
      selectedWarehouseId=""
      onWarehouseChange={vi.fn()}
      vatRates={{ "10": 15 }}
    />,
  );
}

describe("SALES-QUICK-CUSTOMER-MODAL-INPUT-FIX-07 — atajo global F2 respeta el foco editable", () => {
  it("con el foco en un input ajeno (simulando un modal abierto), F2 NO reenfoca el buscador de productos", () => {
    renderSection();
    const searchInput = screen.getByPlaceholderText(/Escribe el nombre del producto/i) as HTMLInputElement;

    // Input "externo" — simula un campo del modal "Crear Cliente" con el foco activo.
    const externalInput = document.createElement("input");
    document.body.appendChild(externalInput);
    externalInput.focus();
    expect(document.activeElement).toBe(externalInput);

    fireEvent.keyDown(externalInput, { key: "F2" });

    expect(document.activeElement).toBe(externalInput);
    expect(document.activeElement).not.toBe(searchInput);

    document.body.removeChild(externalInput);
  });

  it("con el foco fuera de cualquier campo editable, F2 sí reenfoca el buscador de productos", () => {
    renderSection();
    const searchInput = screen.getByPlaceholderText(/Escribe el nombre del producto/i) as HTMLInputElement;

    const externalButton = document.createElement("button");
    document.body.appendChild(externalButton);
    externalButton.focus();

    fireEvent.keyDown(externalButton, { key: "F2" });

    expect(document.activeElement).toBe(searchInput);

    document.body.removeChild(externalButton);
  });
});
