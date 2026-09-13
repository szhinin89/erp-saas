// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { SalesInvoiceDetailsSection } from "./SalesInvoiceDetailsSection";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";
import type { WarehouseDto } from "../../inventory/types";

// SALES-INVOICE-LINES-GRID-UX-01B: las líneas ya agregadas a la factura pasan a ser una grilla
// horizontal con cabecera de columnas visible — mismo patrón visual que el buscador de productos
// (SalesItemSearchResultsGrid): Línea | Producto | Precio lista | Descuento | Precio facturado |
// Stock / Ubicación | Cantidad | Total. La cabecera y cada fila comparten el mismo
// grid-template-columns (--sfl-cols, sales-product-card.css) — se prueba a través de
// SalesInvoiceDetailsSection (integración real), no de SalesInvoiceLinesGrid aislado, para
// confirmar que queda correctamente conectada.

afterEach(() => {
  cleanup();
});

const WAREHOUSES: WarehouseDto[] = [
  { id: "wh-1", name: "Bodega Principal" } as WarehouseDto,
];

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
    _stockQty: 5,
    _stockWarehouse: "Bodega Principal",
    _tracksStock: true,
    ...overrides,
  };
}

function moneyText(container: HTMLElement, selector: string): string | null {
  return container.querySelector(selector)?.textContent ?? null;
}

function renderSection(
  lines: SalesLineFormValues[],
  overrides: Partial<{
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
        readOnly={false}
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

describe("SalesInvoiceLinesGrid — cabecera de columnas (SALES-INVOICE-LINES-GRID-UX-01B)", () => {
  it("renderiza la cabecera con las 8 columnas requeridas", () => {
    const { container } = renderSection([baseLine()]);
    const header = container.querySelector(".sfl-header");
    expect(header).not.toBeNull();
    const cells = Array.from(header!.querySelectorAll(".sfl-header__cell")).map(
      (el) => el.textContent,
    );
    expect(cells).toEqual([
      "Línea",
      "Producto",
      "Precio lista",
      "Descuento",
      "Precio facturado",
      "Stock / Ubicación",
      "Cantidad",
      "Total",
    ]);
  });

  it("no renderiza la cabecera cuando no hay líneas (estado vacío)", () => {
    const { container } = renderSection([]);
    expect(container.querySelector(".sfl-header")).toBeNull();
    expect(screen.getByText(/busca un producto arriba/i)).not.toBeNull();
  });

  it("la cabecera y la fila comparten el mismo grid-template-columns y column-gap (var(--sfl-cols) / var(--sfl-col-gap))", () => {
    const { container } = renderSection([baseLine()]);
    const header = container.querySelector(".sfl-header") as HTMLElement;
    const row = container.querySelector(".sf-product") as HTMLElement;
    expect(getComputedStyle(header).gridTemplateColumns).not.toBe("");
    expect(getComputedStyle(header).getPropertyValue("grid-template-columns")).toBe(
      getComputedStyle(row).getPropertyValue("grid-template-columns"),
    );
    // SALES-INVOICE-LINES-GRID-ROW-SEPARATORS-ALIGNMENT-01F: la separación entre columnas es
    // solo espacio (column-gap), no bordes verticales — cabecera y fila deben usar el mismo
    // valor para no desalinearse.
    expect(getComputedStyle(header).getPropertyValue("column-gap")).toBe(
      getComputedStyle(row).getPropertyValue("column-gap"),
    );
  });

  it("cabecera y filas viven dentro de un único contenedor de tabla (.sfl-table)", () => {
    const { container } = renderSection([baseLine(), baseLine({ _key: 2 })]);
    const table = container.querySelector(".sfl-table");
    expect(table).not.toBeNull();
    expect(table?.querySelector(".sfl-header")).not.toBeNull();
    expect(table?.querySelectorAll(".sf-product-card").length).toBe(2);
  });

  it("cada fila expone las 8 columnas alineadas con la cabecera (línea, producto, precio lista, descuento, precio facturado, stock, cantidad, total)", () => {
    const { container } = renderSection([baseLine()]);
    const row = container.querySelector(".sf-product") as HTMLElement;
    expect(row.children.length).toBe(8);
    expect(row.querySelector(".sf-product__line-cell")).not.toBeNull();
    expect(row.querySelector(".sf-product__info")).not.toBeNull();
    expect(row.querySelector(".sf-product__pricelist")).not.toBeNull();
    expect(row.querySelector(".sf-product__discount")).not.toBeNull();
    expect(row.querySelector(".sf-product__price-block")).not.toBeNull();
    expect(row.querySelector(".sf-product__stock-box")).not.toBeNull();
    expect(row.querySelector(".sf-product__qty")).not.toBeNull();
    expect(row.querySelector(".sf-product__subtotal")).not.toBeNull();
  });

  it("Producto: muestra código y nombre alineados bajo la columna Producto", () => {
    const { container } = renderSection([baseLine()]);
    const productCell = container.querySelector(".sf-product__info");
    expect(productCell?.querySelector(".sf-product__code")?.textContent).toBe(
      "15865",
    );
    expect(productCell?.querySelector(".sf-product__name")?.textContent).toBe(
      "CLUB 850CC RB CAJA X12",
    );
  });

  it("Precio lista: el valor aparece bajo la columna Precio lista", () => {
    const { container } = renderSection([baseLine({ _pvp: 26 })]);
    const cell = container.querySelector(".sf-product__pricelist");
    expect(moneyText(cell as HTMLElement, ".sf-product__pricelist-value")).toBe(
      "$26.00",
    );
  });

  it("Descuento: el input % editable aparece bajo la columna Descuento", () => {
    const { container } = renderSection([baseLine({ discountPct: 5 })]);
    const cell = container.querySelector(".sf-product__discount");
    expect(cell?.querySelector("input")).not.toBeNull();
    expect((cell?.querySelector("input") as HTMLInputElement).value).toBe(
      "5.00",
    );
  });

  it("Precio facturado: el input editable aparece bajo la columna Precio facturado", () => {
    const { container } = renderSection([baseLine({ unitPrice: 26 })]);
    const cell = container.querySelector(".sf-product__price-block");
    expect((cell?.querySelector("input") as HTMLInputElement)?.value).toBe(
      "26.00",
    );
  });

  it("Stock / Ubicación: stock, badge, bodega y 'Ver stock global' aparecen bajo esa columna", () => {
    const { container } = renderSection([baseLine()]);
    const cell = container.querySelector(".sf-product__stock-box");
    expect(cell?.textContent).toContain("5");
    expect(cell?.querySelector(".zh-badge, [class*='badge']")).not.toBeNull();
    expect(cell?.textContent).toMatch(/ver stock global/i);
  });

  it("Cantidad: el input editable aparece bajo la columna Cantidad", () => {
    const { container } = renderSection([baseLine({ quantity: 3 })]);
    const cell = container.querySelector(".sf-product__qty");
    expect((cell?.querySelector("input") as HTMLInputElement)?.value).toBe(
      "3.0000",
    );
  });

  it("Total: base sin IVA, IVA y total línea aparecen bajo la columna Total", () => {
    const { container } = renderSection([baseLine()]);
    const cell = container.querySelector(".sf-product__subtotal");
    expect(cell?.querySelector(".sf-product__total-amount")).not.toBeNull();
    const values = cell?.querySelectorAll(".sf-product__subtotal-value");
    expect(values?.length).toBe(2); // Base sin IVA + IVA
  });

  it("el botón eliminar sigue funcionando", () => {
    const onRemoveLine = vi.fn();
    renderSection([baseLine()], { onRemoveLine });
    fireEvent.click(screen.getByTitle("Eliminar producto de la factura"));
    expect(onRemoveLine).toHaveBeenCalledWith(1);
  });

  it("cambiar la cantidad sigue disparando onUpdateLine", () => {
    const onUpdateLine = vi.fn();
    renderSection([baseLine({ quantity: 2 })], { onUpdateLine });
    const qtyInput = screen.getByDisplayValue("2.0000");
    fireEvent.change(qtyInput, { target: { value: "5" } });
    fireEvent.blur(qtyInput);
    expect(onUpdateLine).toHaveBeenCalledWith(1, "quantity", 5);
  });

  it("cambiar el precio facturado sigue disparando onUpdateLine", () => {
    const onUpdateLine = vi.fn();
    renderSection([baseLine({ unitPrice: 26 })], { onUpdateLine });
    const priceInput = screen.getByDisplayValue("26.00");
    fireEvent.change(priceInput, { target: { value: "30" } });
    fireEvent.blur(priceInput);
    expect(onUpdateLine).toHaveBeenCalledWith(1, "unitPrice", 30);
  });

  it("no muestra 'regla general' ni 'excepción' como texto principal", () => {
    renderSection([
      baseLine({
        _priceListName: "Lista General",
        _discountDescription: "Descuento 5% (regla general)",
      }),
    ]);
    expect(screen.queryByText(/regla general/i)).toBeNull();
    expect(screen.queryByText(/excepción/i)).toBeNull();
    expect(screen.getByText("-5%")).not.toBeNull();
  });

  it("no introduce estilos inline", () => {
    const { container } = renderSection([baseLine()]);
    expect(container.querySelectorAll("[style]").length).toBe(0);
  });

  // SALES-INVOICE-LINES-GRID-HOVER-FOCUS-01D: la fila da feedback de hover/focus solo por CSS
  // (:hover / :focus-within en .sf-product-card, ver sales-product-card.css) — nunca se convirtió
  // en <button> ni ganó tabIndex/role de fila clickeable, justamente para no interferir con los
  // inputs/selects/link que ya vive adentro.
  it("la fila sigue siendo un <div> sin tabIndex ni rol de fila seleccionable (no se convirtió en botón)", () => {
    const { container } = renderSection([baseLine()]);
    const row = container.querySelector(".sf-product-card") as HTMLElement;
    expect(row.tagName).toBe("DIV");
    expect(row.getAttribute("tabindex")).toBeNull();
    expect(row.getAttribute("role")).toBeNull();
  });

  it("el foco dentro de la fila (input de cantidad) no dispara ninguna acción por sí solo — solo :focus-within visual", () => {
    const onUpdateLine = vi.fn();
    const onRemoveLine = vi.fn();
    renderSection([baseLine()], { onUpdateLine, onRemoveLine });
    const qtyInput = screen.getByDisplayValue("2.0000");
    fireEvent.focus(qtyInput);
    expect(onUpdateLine).not.toHaveBeenCalled();
    expect(onRemoveLine).not.toHaveBeenCalled();
  });
});
