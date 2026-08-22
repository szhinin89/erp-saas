// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { SalesInvoiceDetailsSection } from "./SalesInvoiceDetailsSection";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";

// SALES-PRESENTATIONS-03 — selector de presentación (unidad/caja/pack) en la línea POS ya
// agregada: visible solo cuando el ítem tiene más de una presentación, cálculo de equivalencia en
// unidad base, y mensaje de stock insuficiente con la equivalencia explicada.

afterEach(() => {
  cleanup();
});

const PACKAGING_LEVELS = [
  { id: "unit-1", name: "UNIDAD", uomCode: "UNIT", baseQuantity: 1, barcode: null, isBaseUnit: true, isSaleDefault: true },
  { id: "caja-12", name: "CAJA", uomCode: "CAJA", baseQuantity: 12, barcode: "7501234567890", isBaseUnit: false, isSaleDefault: false },
];

function boxLine(overrides: Partial<SalesLineFormValues> = {}): SalesLineFormValues {
  return {
    _key: 1,
    itemId: "item-1",
    warehouseId: "wh-1",
    description: "15865 — CLUB 850CC RB CAJA X12",
    quantity: 1,
    unitPrice: 18,
    vatCode: "10",
    discountPct: 0,
    iceCode: undefined,
    packagingLevelId: "caja-12",
    uomCode: "CAJA",
    baseUomCode: "UNIT",
    conversionFactor: 12,
    _sku: "15865",
    _name: "CLUB 850CC RB CAJA X12",
    _pvp: 1.5,
    _stockQty: 20,
    _tracksStock: true,
    _packagingLevels: PACKAGING_LEVELS,
    ...overrides,
  };
}

function renderSection(
  lines: SalesLineFormValues[],
  onUpdatePresentation = vi.fn(),
) {
  render(
    <MemoryRouter>
      <SalesInvoiceDetailsSection
        lines={lines}
        readOnly={false}
        disabled={false}
        onRemoveLine={vi.fn()}
        onUpdateLine={vi.fn()}
        onAddItemLine={vi.fn()}
        onUpdateLineWarehouse={vi.fn()}
        onUpdateLinePresentation={onUpdatePresentation}
        warehouses={[]}
        selectedWarehouseId=""
        onWarehouseChange={vi.fn()}
        vatRates={{ "10": 15 }}
      />
    </MemoryRouter>,
  );
  return onUpdatePresentation;
}

describe("SalesInvoiceDetailsSection — selector de presentación", () => {
  it("producto con caja x12: selector visible con la presentación seleccionada", () => {
    renderSection([boxLine()]);
    expect(screen.getByText("Presentación")).not.toBeNull();
    expect((screen.getByRole("combobox") as HTMLSelectElement).value).toBe(
      "caja-12",
    );
  });

  it("producto sin presentaciones configuradas: no muestra selector (comportamiento actual)", () => {
    renderSection([
      boxLine({ packagingLevelId: null, uomCode: "UNIT", conversionFactor: 1, _packagingLevels: [] }),
    ]);
    expect(screen.queryByRole("combobox", { name: "" })).toBeNull();
  });

  it("cantidad 1 en caja x12: muestra la equivalencia en unidad base", () => {
    renderSection([boxLine({ quantity: 1 })]);
    expect(screen.getByText("Equivale a 12 UNIT")).not.toBeNull();
  });

  it("subtotal (Base sin IVA) usa el precio por caja, no el precio por unidad", () => {
    // quantity=1 (caja) * unitPrice=18 (precio por caja) = 18 — nunca 1.5 (precio por unidad)
    // ni 216 (doble multiplicación por 12 otra vez).
    const { container } = render(
      <MemoryRouter>
        <SalesInvoiceDetailsSection
          lines={[boxLine({ quantity: 1, unitPrice: 18 })]}
          readOnly={false}
          disabled={false}
          onRemoveLine={vi.fn()}
          onUpdateLine={vi.fn()}
          onAddItemLine={vi.fn()}
          onUpdateLineWarehouse={vi.fn()}
          onUpdateLinePresentation={vi.fn()}
          warehouses={[]}
          selectedWarehouseId=""
          onWarehouseChange={vi.fn()}
          vatRates={{ "10": 15 }}
        />
      </MemoryRouter>,
    );
    const baseAmount = container.querySelector(".sf-product__subtotal-value")?.textContent ?? "";
    expect(baseAmount).toContain("18");
  });

  it("stock insuficiente con presentación: mensaje explica la equivalencia en unidad base", () => {
    // Stock 10 unidades base, venta 1 caja x12 = 12 unidades base requeridas → bloquea.
    renderSection([boxLine({ quantity: 1, _stockQty: 10 })]);
    expect(
      screen.getByText(
        "Stock insuficiente: 1 CAJA equivale a 12 UNIT, disponible 10 UNIT.",
      ),
    ).not.toBeNull();
  });

  it("cambiar de presentación invoca onUpdateLinePresentation con la línea y el id seleccionados", () => {
    const onUpdatePresentation = renderSection([boxLine()]);
    const select = screen.getByRole("combobox");
    fireEvent.change(select, { target: { value: "" } });
    expect(onUpdatePresentation).toHaveBeenCalledWith(1, "");
  });
});
