// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { SalesInvoiceDetailsSection } from "./SalesInvoiceDetailsSection";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";

// SALES-RETAIL-READY-01-FIX03 — advertencia preventiva de stock por línea (lineExceedsStock).

afterEach(() => {
  cleanup();
});

function baseLine(overrides: Partial<SalesLineFormValues>): SalesLineFormValues {
  return {
    _key: 1,
    itemId: "item-1",
    warehouseId: "wh-1",
    description: "CUBHUEVO — CUBETA DE HUEVO",
    quantity: 1,
    unitPrice: 3.5,
    vatCode: "0",
    discountPct: 0,
    iceCode: undefined,
    _sku: "CUBHUEVO",
    _name: "CUBETA DE HUEVO",
    _tracksStock: true,
    ...overrides,
  };
}

function renderSection(lines: SalesLineFormValues[]) {
  return render(
    <SalesInvoiceDetailsSection
      lines={lines}
      readOnly
      disabled={false}
      onRemoveLine={vi.fn()}
      onUpdateLine={vi.fn()}
      onAddItemLine={vi.fn()}
      onUpdateLineWarehouse={vi.fn()}
      warehouses={[]}
      selectedWarehouseId=""
      onWarehouseChange={vi.fn()}
      vatRates={{ "0": 0 }}
    />,
  );
}

describe("SalesInvoiceDetailsSection — advertencia de stock por línea", () => {
  it("muestra advertencia cuando la cantidad solicitada supera el stock disponible", () => {
    renderSection([
      baseLine({ quantity: 5, _stockQty: 0 }),
    ]);

    expect(screen.getByText(/Cantidad excede stock/i)).not.toBeNull();
    expect(screen.getByText(/Supera el disponible/i)).not.toBeNull();
  });

  it("no muestra advertencia cuando la cantidad no supera el stock disponible", () => {
    renderSection([baseLine({ quantity: 1, _stockQty: 10 })]);

    expect(screen.queryByText(/Cantidad excede stock/i)).toBeNull();
    expect(screen.queryByText(/Supera el disponible/i)).toBeNull();
  });

  it("no bloquea/advierte cuando el dato de stock no está disponible (undefined) — no inventa stock", () => {
    renderSection([baseLine({ quantity: 100, _stockQty: undefined })]);

    expect(screen.queryByText(/Cantidad excede stock/i)).toBeNull();
    expect(screen.queryByText(/Supera el disponible/i)).toBeNull();
  });
});
