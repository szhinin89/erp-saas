// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { SalesInvoiceDetailsSection } from "./SalesInvoiceDetailsSection";
import type { InvoiceItemSearchResultDto } from "../api/invoiceItemSearchService";

// SALES-RETAIL-READY-01-FIX05 — jerarquía visual de los resultados del buscador POS: nombre,
// código, estado/stock, desglose Precio sin IVA / IVA / Precio final (FIX05A), sin costo, botón
// Agregar. La imagen del usuario se usó solo como referencia de jerarquía — no se copió literal.

const searchMock = vi.fn();
vi.mock("../api/invoiceItemSearchService", () => ({
  invoiceItemSearchService: {
    search: (...args: unknown[]) => searchMock(...args),
  },
}));

afterEach(() => {
  cleanup();
  searchMock.mockReset();
});

// Precio sin IVA / IVA / Precio final ahora renderizan con ZHMoneyValue
// (SALES-DS-MONEY-12): símbolo y monto quedan en <span> hermanos, así que el
// texto completo ("$24.30") ya no vive en un único nodo de texto — se ubica
// por la clase zh-money-value en vez de por texto plano.
function getMoneyValueByText(text: string): HTMLElement {
  return screen.getByText((_, element) => {
    if (!element || !element.classList.contains("zh-money-value")) return false;
    return element.textContent === text;
  });
}

function makeResult(
  overrides: Partial<InvoiceItemSearchResultDto> = {},
): InvoiceItemSearchResultDto {
  return {
    id: "item-1",
    sku: "15865",
    description: "CLUB 850CC RB CAJA X12",
    productFamilyName: null,
    uomAbbrev: "CAJA",
    tracksStock: true,
    warehouseName: "Matriz",
    availableStock: 5,
    averageCost: 18.5,
    salePriceWithoutTax: 24.3,
    finalSalePrice: 27.95,
    vatDisplay: "IVA 15%",
    iceDisplay: "—",
    vatCode: "10",
    iceCode: null,
    baseUomCode: "UNIT",
    packagingLevels: [],
    matchedPackagingLevelId: null,
    ...overrides,
  };
}

function renderSection(onAddItemLine = vi.fn().mockResolvedValue(undefined)) {
  const utils = render(
    <SalesInvoiceDetailsSection
      lines={[]}
      readOnly={false}
      disabled={false}
      onRemoveLine={vi.fn()}
      onUpdateLine={vi.fn()}
      onAddItemLine={onAddItemLine}
      onUpdateLineWarehouse={vi.fn()}
      warehouses={[]}
      selectedWarehouseId=""
      onWarehouseChange={vi.fn()}
      vatRates={{ "10": 15 }}
    />,
  );
  return { onAddItemLine, ...utils };
}

function typeQuery(query: string) {
  const input = screen.getByPlaceholderText(/Escribe el nombre del producto/i);
  fireEvent.change(input, { target: { value: query } });
  return input;
}

describe("SalesInvoiceDetailsSection — resultados del buscador (jerarquía retail)", () => {
  it("muestra el nombre del producto", async () => {
    // El texto de coincidencia queda resaltado en <mark> (highlightMatch), así que el nombre
    // completo no vive en un único nodo de texto — se valida vía el textContent del contenedor.
    searchMock.mockResolvedValue([makeResult()]);
    const { container } = renderSection();
    typeQuery("club");
    await screen.findByText("15865");
    expect(container.querySelector(".sf-result__name")?.textContent).toBe(
      "CLUB 850CC RB CAJA X12",
    );
  });

  it("muestra el código/SKU", async () => {
    searchMock.mockResolvedValue([makeResult()]);
    renderSection();
    typeQuery("club");
    expect(await screen.findByText("15865")).not.toBeNull();
  });

  it("muestra el stock disponible", async () => {
    searchMock.mockResolvedValue([makeResult({ availableStock: 5 })]);
    renderSection();
    typeQuery("club");
    expect(await screen.findByText(/STOCK:/)).not.toBeNull();
    expect(screen.getByText("5.0000")).not.toBeNull();
  });

  it("muestra la etiqueta Precio sin IVA (SALES-RETAIL-READY-01-FIX05A)", async () => {
    searchMock.mockResolvedValue([makeResult()]);
    renderSection();
    typeQuery("club");
    expect(await screen.findByText("Precio sin IVA")).not.toBeNull();
    expect(getMoneyValueByText("$24.30")).not.toBeNull();
  });

  it("muestra la etiqueta IVA (15%)", async () => {
    searchMock.mockResolvedValue([makeResult()]);
    renderSection();
    typeQuery("club");
    expect(await screen.findByText("IVA (15%)")).not.toBeNull();
    expect(getMoneyValueByText("$3.65")).not.toBeNull();
  });

  it("muestra la etiqueta Precio final con el precio final destacado (SALES-RETAIL-READY-01-FIX05A)", async () => {
    searchMock.mockResolvedValue([makeResult()]);
    renderSection();
    typeQuery("club");
    expect(await screen.findByText("Precio final")).not.toBeNull();
    const finalValue = getMoneyValueByText("$27.95");
    expect(finalValue).not.toBeNull();
    expect(finalValue.className).toContain("sf-result__price-val--final");
  });

  it("no muestra Costo en el resultado del buscador", async () => {
    searchMock.mockResolvedValue([makeResult({ averageCost: 18.5 })]);
    renderSection();
    typeQuery("club");
    await screen.findByText("Precio sin IVA");
    expect(screen.queryByText(/costo/i)).toBeNull();
    expect(screen.queryByText("$18.50")).toBeNull();
  });

  it("producto con stock bajo (≤5) muestra STOCK BAJO", async () => {
    searchMock.mockResolvedValue([makeResult({ availableStock: 2 })]);
    renderSection();
    typeQuery("club");
    expect(await screen.findByText(/stock bajo/i)).not.toBeNull();
  });

  it("producto sin stock (dato real 0) muestra el badge SIN STOCK y la cantidad real 0.0000, nunca un guion", async () => {
    searchMock.mockResolvedValue([makeResult({ availableStock: 0 })]);
    renderSection();
    typeQuery("club");
    expect(await screen.findByText(/sin stock/i)).not.toBeNull();
    expect(await screen.findByText(/STOCK:/)).not.toBeNull();
    expect(screen.getByText("0.0000")).not.toBeNull();
  });

  it("producto sin dato de disponibilidad (null, no confirmado en 0) no inventa stock — muestra 'Sin stock' en vez de 'STOCK: — UN' (SALES-RETAIL-READY-01-FIX05A)", async () => {
    searchMock.mockResolvedValue([makeResult({ availableStock: null })]);
    renderSection();
    typeQuery("club");
    await screen.findByText("15865");
    // No debe mostrarse el guion "—" como si fuera un valor de stock.
    expect(screen.queryByText(/STOCK:/)).toBeNull();
    expect(screen.queryByText("—")).toBeNull();
    expect(screen.getAllByText(/sin stock/i).length).toBeGreaterThan(0);
  });

  it("producto sin precio configurado (null) muestra SIN PRECIO / Sin precio configurado, nunca $0.00", async () => {
    searchMock.mockResolvedValue([
      makeResult({ salePriceWithoutTax: null, finalSalePrice: null }),
    ]);
    renderSection();
    typeQuery("club");
    expect(await screen.findByText("Sin precio")).not.toBeNull();
    expect(screen.getByText("Sin precio configurado")).not.toBeNull();
    expect(screen.queryByText("$0.00")).toBeNull();
  });

  it("el botón Agregar ejecuta la misma acción que seleccionar la fila", async () => {
    searchMock.mockResolvedValue([makeResult()]);
    const { onAddItemLine } = renderSection();
    typeQuery("club");
    const addBtn = await screen.findByRole("button", { name: /agregar/i });
    fireEvent.click(addBtn);
    await waitFor(() =>
      expect(onAddItemLine).toHaveBeenCalledWith(
        expect.objectContaining({ id: "item-1" }),
      ),
    );
  });

  it("Enter agrega el producto resuelto por la búsqueda (flujo de escaneo por barcode)", async () => {
    searchMock.mockResolvedValue([makeResult()]);
    const { onAddItemLine } = renderSection();
    const input = typeQuery("15865");
    await screen.findByText("CLUB 850CC RB CAJA X12");
    fireEvent.keyDown(input, { key: "Enter" });
    await waitFor(() => expect(onAddItemLine).toHaveBeenCalled());
  });

  it("la búsqueda por texto sigue enviando el término al servicio existente (sin cambiar ranking/backend)", async () => {
    searchMock.mockResolvedValue([]);
    renderSection();
    typeQuery("club");
    await waitFor(() =>
      expect(searchMock).toHaveBeenCalledWith(
        expect.objectContaining({ q: "club" }),
      ),
    );
  });

  it("no introduce estilos inline en los resultados", async () => {
    searchMock.mockResolvedValue([makeResult()]);
    const { container } = renderSection();
    typeQuery("club");
    await screen.findByText("15865");
    expect(container.querySelectorAll("[style]").length).toBe(0);
  });
});
