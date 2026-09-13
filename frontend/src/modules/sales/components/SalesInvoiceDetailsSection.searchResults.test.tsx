// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { SalesInvoiceDetailsSection } from "./SalesInvoiceDetailsSection";
import type { InvoiceItemSearchResultDto } from "../api/invoiceItemSearchService";

// SALES-ITEM-SEARCH-RESULTS-GRID-COMPONENT-01: la grilla de resultados (encabezado de columnas,
// precio normal/promo/final, stock, highlight, etc.) se extrajo a SalesItemSearchResultsGrid
// (ver SalesItemSearchResultsGrid.test.tsx para esa cobertura). Este archivo se reduce a la
// integración mínima con el buscador: que SalesInvoiceDetailsSection renderiza la grilla con los
// resultados correctos y que el flujo de búsqueda/selección (click, Enter, escaneo por barcode)
// sigue funcionando igual que antes de la extracción.

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
    priceListName: null,
    discountDescription: null,
    discountedSalePriceWithoutTax: null,
    discountedFinalSalePrice: null,
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

describe("SalesInvoiceDetailsSection — integración con el buscador de productos", () => {
  it("renderiza la grilla de resultados (encabezado + fila) para la búsqueda actual", async () => {
    searchMock.mockResolvedValue([makeResult()]);
    const { container } = renderSection();
    typeQuery("club");
    await screen.findByText("15865");
    expect(container.querySelector(".sf-search-columns-header")).not.toBeNull();
    expect(container.querySelector(".sf-result")).not.toBeNull();
    expect(container.querySelector(".sf-result__name")?.textContent).toBe(
      "CLUB 850CC RB CAJA X12",
    );
  });

  it("el botón Agregar de la grilla ejecuta onAddItemLine", async () => {
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

  it("sin resultados no renderiza la grilla, solo el mensaje de 'sin resultados'", async () => {
    searchMock.mockResolvedValue([]);
    const { container } = renderSection();
    typeQuery("xyz");
    await screen.findByText(/Sin resultados para/i);
    expect(container.querySelector(".sf-search-columns-header")).toBeNull();
  });

  it("no introduce estilos inline en la sección de resultados", async () => {
    searchMock.mockResolvedValue([makeResult()]);
    const { container } = renderSection();
    typeQuery("club");
    await screen.findByText("15865");
    expect(container.querySelectorAll("[style]").length).toBe(0);
  });
});
