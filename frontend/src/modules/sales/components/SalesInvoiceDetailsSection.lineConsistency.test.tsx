// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { SalesInvoiceDetailsSection } from "./SalesInvoiceDetailsSection";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";

// SALES-RETAIL-READY-01-FIX04 — quantity/price/discount/subtotal deben salir de una única
// fuente de verdad (line.quantity/unitPrice/discountPct), tanto en lo mostrado en el input como
// en el subtotal calculado. Reproduce el bug reportado: reescanear el mismo producto duplicaba
// el subtotal mientras el input de cantidad seguía mostrando "1.0000".

afterEach(() => {
  cleanup();
});

function baseLine(overrides: Partial<SalesLineFormValues> = {}): SalesLineFormValues {
  return {
    _key: 1,
    itemId: "item-1",
    warehouseId: null,
    description: "CUBHUEVO — CUBETA DE HUEVO",
    quantity: 1,
    unitPrice: 29.9,
    vatCode: "0",
    discountPct: 0,
    iceCode: undefined,
    _sku: "CUBHUEVO",
    _name: "CUBETA DE HUEVO",
    _tracksStock: false,
    ...overrides,
  };
}

// El total de línea y la base sin IVA pueden coincidir en dólares cuando la tasa de IVA es 0%
// (caso usado en estos tests) — se leen por su fila/clase específica, no por el string bruto,
// para no depender de que los montos sean numéricamente distintos.
function totalAmountText(container: HTMLElement): string | null {
  return container.querySelector(".sf-product__total-amount")?.textContent ?? null;
}

// SalesProductCard incluye un <Link> ("Ver stock global") desde FIX06C — requiere contexto de
// router incluso en tests que no lo ejercen directamente.
function sectionElement(
  lines: SalesLineFormValues[],
  onUpdateLine: (key: number, field: string, value: unknown) => void = vi.fn(),
) {
  return (
    <MemoryRouter>
      <SalesInvoiceDetailsSection
        lines={lines}
        readOnly={false}
        disabled={false}
        onRemoveLine={vi.fn()}
        onUpdateLine={onUpdateLine}
        onAddItemLine={vi.fn()}
        onUpdateLineWarehouse={vi.fn()}
        warehouses={[]}
        selectedWarehouseId=""
        onWarehouseChange={vi.fn()}
        vatRates={{ "0": 0 }}
      />
    </MemoryRouter>
  );
}

function renderSection(lines: SalesLineFormValues[]) {
  return render(sectionElement(lines));
}

describe("SalesInvoiceDetailsSection — consistencia cantidad/precio/descuento/subtotal", () => {
  it("al reescanear (quantity pasa de 1 a 2 vía props), el input de cantidad y el subtotal se actualizan juntos", () => {
    const { rerender, container } = renderSection([baseLine({ quantity: 1 })]);

    expect(screen.getByDisplayValue("1.0000")).not.toBeNull();
    expect(totalAmountText(container)).toBe("$29.90");

    // Simula lo que hace useSalesPage.addLineWithItem al fusionar un reescaneo: incrementa
    // quantity en la MISMA línea (mismo _key), sin tocar el resto de campos.
    rerender(sectionElement([baseLine({ quantity: 2 })]));

    // La cantidad visible ya no debe seguir mostrando "1.0000" — ese era el bug reportado.
    expect(screen.queryByDisplayValue("1.0000")).toBeNull();
    expect(screen.getByDisplayValue("2.0000")).not.toBeNull();
    // El subtotal (2 × 29.90) debe corresponder a la cantidad ahora visible, no quedar
    // desincronizado ni duplicarse por fuera de esa cantidad.
    expect(totalAmountText(container)).toBe("$59.80");
  });

  it("no se crean líneas ocultas: una sola tarjeta visual, un solo input de cantidad, tras el reescaneo", () => {
    const { container, rerender } = renderSection([baseLine({ quantity: 1 })]);
    expect(container.querySelectorAll(".sf-product").length).toBe(1);

    rerender(sectionElement([baseLine({ quantity: 2 })]));

    expect(container.querySelectorAll(".sf-product").length).toBe(1);
    expect(container.querySelectorAll(".sf-product__qty-input").length).toBe(1);
  });

  it("editar la cantidad manualmente (blur) dispara onUpdate con el nuevo valor", () => {
    const onUpdateLine = vi.fn();
    render(sectionElement([baseLine({ quantity: 1 })], onUpdateLine));

    const qtyInput = screen.getByDisplayValue("1.0000");
    fireEvent.change(qtyInput, { target: { value: "3" } });
    fireEvent.blur(qtyInput);

    expect(onUpdateLine).toHaveBeenCalledWith(1, "quantity", 3);
  });

  it("al cambiar el descuento vía props, el subtotal (base con descuento) se recalcula", () => {
    const { rerender, container } = renderSection([
      baseLine({ quantity: 1, unitPrice: 100, discountPct: 0 }),
    ]);
    // Sin descuento: base = 100.00
    expect(totalAmountText(container)).toBe("$100.00");

    rerender(
      sectionElement([baseLine({ quantity: 1, unitPrice: 100, discountPct: 10 })]),
    );

    // Con 10% de descuento: base (y total, con IVA 0%) = 90.00 — recalculado, no acumulado
    // por fuera de la fórmula.
    expect(totalAmountText(container)).toBe("$90.00");
  });

  it("al cambiar el precio facturado vía props, el subtotal se recalcula", () => {
    const { rerender, container } = renderSection([
      baseLine({ quantity: 2, unitPrice: 10 }),
    ]);
    expect(totalAmountText(container)).toBe("$20.00");

    rerender(sectionElement([baseLine({ quantity: 2, unitPrice: 15 })]));

    expect(totalAmountText(container)).toBe("$30.00");
  });

  it("la advertencia de stock aparece cuando el reescaneo hace que la cantidad supere el disponible", () => {
    const { rerender } = renderSection([
      baseLine({ quantity: 1, _tracksStock: true, _stockQty: 1 }),
    ]);
    expect(screen.queryByText(/Cantidad excede stock/i)).toBeNull();

    rerender(
      sectionElement([baseLine({ quantity: 2, _tracksStock: true, _stockQty: 1 })]),
    );

    expect(screen.getByText(/Cantidad excede stock/i)).not.toBeNull();
    expect(screen.getByDisplayValue("2.0000")).not.toBeNull();
  });
});
