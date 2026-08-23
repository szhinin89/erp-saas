// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { SalesInvoiceDetailsSection } from "./SalesInvoiceDetailsSection";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";
import type { WarehouseDto } from "../../inventory/types";

// SALES-RETAIL-READY-01-FIX06 — jerarquía visual de la línea ya agregada a la factura: producto,
// cantidad, total línea (dato más fuerte del bloque derecho), stock/bodega, precio facturado,
// descuento, base sin IVA e IVA. La imagen del usuario se usó solo como referencia de jerarquía.

afterEach(() => {
  cleanup();
});

const WAREHOUSES: WarehouseDto[] = [
  { id: "wh-1", name: "Bodega Principal" } as WarehouseDto,
];

// Mismos números que el ejemplo real de la tarea: 2 × $26.00, IVA 15% → base $52.00, IVA $7.80,
// total línea $59.80.
function baseLine(overrides: Partial<SalesLineFormValues> = {}): SalesLineFormValues {
  return {
    _key: 1,
    itemId: "item-1",
    warehouseId: "wh-1",
    description: "15865 — CLUB 850CC RB CAJA X12",
    quantity: 2,
    unitPrice: 26,
    vatCode: "10",
    discountPct: 0,
    iceCode: undefined,
    _sku: "15865",
    _name: "CLUB 850CC RB CAJA X12",
    _pvp: 26,
    _cost: 24.3041,
    _stockQty: 5,
    _stockWarehouse: "Bodega Principal",
    _tracksStock: true,
    ...overrides,
  };
}

// Con IVA 0% (como en los tests de edición de abajo), base y total coinciden en dólares — se lee
// el total por su clase específica en vez de un texto suelto para no depender de esa coincidencia.
function totalAmountText(container: HTMLElement): string | null {
  return container.querySelector(".sf-product__total-amount")?.textContent ?? null;
}

// DS-LINE-ATOM-MIGRATION-01: ZHMoneyValue divide "$" y el monto en dos <span> hijos
// (.zh-money-value__symbol / __amount), así que el texto completo ya no vive en un único nodo de
// texto directo — getByText("$26.00") deja de encontrar coincidencia (getNodeText de Testing
// Library solo concatena text nodes directos, no de descendientes). Se lee el texto agregado
// (textContent, que sí junta todos los descendientes) por selector de clase en su lugar.
function moneyText(container: HTMLElement, selector: string): string | null {
  return container.querySelector(selector)?.textContent ?? null;
}

function renderSection(
  lines: SalesLineFormValues[],
  overrides: Partial<{
    readOnly: boolean;
    onUpdateLine: (key: number, field: string, value: unknown) => void;
    onRemoveLine: (key: number) => void;
  }> = {},
) {
  const onUpdateLine = overrides.onUpdateLine ?? vi.fn();
  const onRemoveLine = overrides.onRemoveLine ?? vi.fn();
  const utils = render(
    <MemoryRouter>
      <SalesInvoiceDetailsSection
        lines={lines}
        readOnly={overrides.readOnly ?? false}
        disabled={false}
        onRemoveLine={onRemoveLine}
        onUpdateLine={onUpdateLine}
        onAddItemLine={vi.fn()}
        onUpdateLineWarehouse={vi.fn()}
        warehouses={WAREHOUSES}
        selectedWarehouseId="wh-1"
        onWarehouseChange={vi.fn()}
        vatRates={{ "10": 15 }}
      />
    </MemoryRouter>,
  );
  return { onUpdateLine, onRemoveLine, ...utils };
}

describe("SalesInvoiceDetailsSection — ficha de línea de venta retail (FIX06)", () => {
  it("muestra el producto y el SKU", () => {
    const { container } = renderSection([baseLine()]);
    expect(container.querySelector(".sf-product__code")?.textContent).toBe(
      "15865",
    );
    expect(container.querySelector(".sf-product__name")?.textContent).toBe(
      "CLUB 850CC RB CAJA X12",
    );
  });

  it("muestra la cantidad", () => {
    renderSection([baseLine({ quantity: 2 })]);
    expect(screen.getByText("Cantidad")).not.toBeNull();
    expect(screen.getByDisplayValue("2.0000")).not.toBeNull();
  });

  it("muestra el precio lista", () => {
    const { container } = renderSection([baseLine({ _pvp: 26 })]);
    expect(screen.getByText("Precio lista")).not.toBeNull();
    expect(moneyText(container, ".sf-product__pricelist-value")).toBe("$26.00");
  });

  it("muestra el descuento (Dto. %)", () => {
    renderSection([baseLine()]);
    expect(screen.getByText("Dto. %")).not.toBeNull();
    expect(screen.getByDisplayValue("0.00")).not.toBeNull();
  });

  it("muestra el precio facturado", () => {
    renderSection([baseLine({ unitPrice: 26 })]);
    expect(screen.getByText("Precio facturado sin IVA")).not.toBeNull();
    expect(screen.getByDisplayValue("26.00")).not.toBeNull();
  });

  it("muestra stock y bodega", () => {
    // Se consulta la etiqueta específica de la ficha de línea (.sf-product__stock-label) por
    // robustez, aunque desde FIX06B ya no exista un encabezado de tabla global con el mismo texto.
    const { container } = renderSection([baseLine()]);
    const labels = Array.from(
      container.querySelectorAll(".sf-product__stock-label"),
    ).map((el) => el.textContent);
    expect(labels).toEqual(["Stock", "Ubicación"]);
    expect(screen.getByText("5")).not.toBeNull();
  });

  it("muestra el enlace Ver stock global cuando el ítem controla inventario", () => {
    renderSection([baseLine({ itemId: "item-1", _tracksStock: true })]);
    const link = screen.getByText(/ver stock global/i).closest("a");
    expect(link).not.toBeNull();
    expect(link?.getAttribute("href")).toBe("/inventory/kardex?productId=item-1");
    expect(link?.getAttribute("target")).toBe("_blank");
  });

  it("muestra la base sin IVA", () => {
    const { container } = renderSection([baseLine()]);
    expect(screen.getByText("Base sin IVA")).not.toBeNull();
    const [baseValue] = container.querySelectorAll(".sf-product__subtotal-value");
    expect(baseValue?.textContent).toBe("$52.00");
  });

  it("muestra el IVA con tasa", () => {
    const { container } = renderSection([baseLine()]);
    expect(screen.getByText("IVA (15%)")).not.toBeNull();
    const [, ivaValue] = container.querySelectorAll(".sf-product__subtotal-value");
    expect(ivaValue?.textContent).toBe("$7.80");
  });

  it("muestra el total de línea", () => {
    const { container } = renderSection([baseLine()]);
    expect(screen.getByText("Total línea")).not.toBeNull();
    expect(totalAmountText(container)).toBe("$59.80");
  });

  it("el total de línea es visualmente el dato principal del bloque derecho", () => {
    const { container } = renderSection([baseLine()]);
    const totalValue = container.querySelector(".sf-product__total-amount");
    const baseValue = container.querySelector(".sf-product__subtotal-value");
    expect(totalValue?.textContent).toBe("$59.80");
    expect(baseValue?.textContent).toBe("$52.00");
    // El total tiene su propia clase de énfasis; base/IVA comparten una clase más discreta.
    expect(totalValue?.className).toContain("sf-product__total-amount");
    expect(baseValue?.className).toContain("sf-product__subtotal-value");
    expect(totalValue?.className).not.toBe(baseValue?.className);
  });

  it("no muestra Costo por defecto", () => {
    renderSection([baseLine({ _cost: 24.3041 })]);
    expect(screen.queryByText(/costo/i)).toBeNull();
    expect(screen.queryByText("$24.30")).toBeNull();
  });

  it("no muestra Margen por defecto", () => {
    renderSection([baseLine()]);
    expect(screen.queryByText(/margen/i)).toBeNull();
  });

  it("no muestra Últ. venta por defecto", () => {
    renderSection([baseLine()]);
    expect(screen.queryByText(/últ\.?\s*venta/i)).toBeNull();
  });

  it("editar cantidad manualmente recalcula el total (vía onUpdate)", () => {
    const onUpdateLine = vi.fn();
    renderSection([baseLine({ quantity: 2 })], { onUpdateLine });
    const qtyInput = screen.getByDisplayValue("2.0000");
    fireEvent.change(qtyInput, { target: { value: "3" } });
    fireEvent.blur(qtyInput);
    expect(onUpdateLine).toHaveBeenCalledWith(1, "quantity", 3);
  });

  it("editar el descuento recalcula la base mostrada", () => {
    const { rerender, container } = renderSection([
      baseLine({ discountPct: 0, unitPrice: 100, quantity: 1, vatCode: "0" }),
    ]);
    expect(totalAmountText(container)).toBe("$100.00"); // base sin descuento (IVA 0%)

    rerender(
      <MemoryRouter>
        <SalesInvoiceDetailsSection
          lines={[
            baseLine({ discountPct: 10, unitPrice: 100, quantity: 1, vatCode: "0" }),
          ]}
          readOnly={false}
          disabled={false}
          onRemoveLine={vi.fn()}
          onUpdateLine={vi.fn()}
          onAddItemLine={vi.fn()}
          onUpdateLineWarehouse={vi.fn()}
          warehouses={WAREHOUSES}
          selectedWarehouseId="wh-1"
          onWarehouseChange={vi.fn()}
          vatRates={{ "10": 15, "0": 0 }}
        />
      </MemoryRouter>,
    );

    expect(totalAmountText(container)).toBe("$90.00"); // con 10% de descuento
  });

  it("editar el precio facturado recalcula el total", () => {
    const { rerender, container } = renderSection([
      baseLine({ unitPrice: 10, quantity: 2, discountPct: 0, vatCode: "0" }),
    ]);
    expect(totalAmountText(container)).toBe("$20.00");

    rerender(
      <MemoryRouter>
        <SalesInvoiceDetailsSection
          lines={[
            baseLine({ unitPrice: 15, quantity: 2, discountPct: 0, vatCode: "0" }),
          ]}
          readOnly={false}
          disabled={false}
          onRemoveLine={vi.fn()}
          onUpdateLine={vi.fn()}
          onAddItemLine={vi.fn()}
          onUpdateLineWarehouse={vi.fn()}
          warehouses={WAREHOUSES}
          selectedWarehouseId="wh-1"
          onWarehouseChange={vi.fn()}
          vatRates={{ "10": 15, "0": 0 }}
        />
      </MemoryRouter>,
    );

    expect(totalAmountText(container)).toBe("$30.00");
    expect(totalAmountText(container)).not.toBe("$20.00");
  });

  it("el reescaneo (quantity cambia vía props) mantiene la cantidad visible sincronizada con el total", () => {
    const { rerender, container } = renderSection([
      baseLine({ quantity: 1, unitPrice: 26 }),
    ]);
    expect(screen.getByDisplayValue("1.0000")).not.toBeNull();

    rerender(
      <MemoryRouter>
        <SalesInvoiceDetailsSection
          lines={[baseLine({ quantity: 2, unitPrice: 26 })]}
          readOnly={false}
          disabled={false}
          onRemoveLine={vi.fn()}
          onUpdateLine={vi.fn()}
          onAddItemLine={vi.fn()}
          onUpdateLineWarehouse={vi.fn()}
          warehouses={WAREHOUSES}
          selectedWarehouseId="wh-1"
          onWarehouseChange={vi.fn()}
          vatRates={{ "10": 15 }}
        />
      </MemoryRouter>,
    );

    expect(screen.queryByDisplayValue("1.0000")).toBeNull();
    expect(screen.getByDisplayValue("2.0000")).not.toBeNull();
    expect(totalAmountText(container)).toBe("$59.80");
  });

  it("la advertencia de stock sigue funcionando cuando la cantidad supera el disponible", () => {
    renderSection([baseLine({ quantity: 10, _stockQty: 5, _tracksStock: true })]);
    expect(screen.getByText(/cantidad excede stock/i)).not.toBeNull();
    expect(screen.getByText(/supera el disponible/i)).not.toBeNull();
  });

  it("el botón eliminar sigue funcionando", () => {
    const onRemoveLine = vi.fn();
    renderSection([baseLine()], { onRemoveLine });
    fireEvent.click(screen.getByTitle("Eliminar producto de la factura"));
    expect(onRemoveLine).toHaveBeenCalledWith(1);
  });

  it("no introduce estilos inline", () => {
    const { container } = renderSection([baseLine()]);
    expect(container.querySelectorAll("[style]").length).toBe(0);
  });
});
