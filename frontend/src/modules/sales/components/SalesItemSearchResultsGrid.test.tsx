// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { SalesItemSearchResultsGrid } from "./SalesItemSearchResultsGrid";
import type { InvoiceItemSearchResultDto } from "../api/invoiceItemSearchService";

// SALES-ITEM-SEARCH-RESULTS-GRID-COMPONENT-01: tests unitarios del componente extraído —
// grilla horizontal con encabezado de columnas (Código | Producto | Stock | Precio reg. | Promo
// | Precio final | Agregar). Sin mock de servicio de búsqueda: el componente no hace fetch, solo
// recibe `results` ya resueltos. Los tests de integración con el buscador (debounce/Enter/
// escaneo) quedan en SalesInvoiceDetailsSection.searchResults.test.tsx.

afterEach(() => {
  cleanup();
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

function getMoneyValueByText(text: string): HTMLElement {
  return screen.getByText((_, element) => {
    if (!element || !element.classList.contains("zh-money-value")) return false;
    return element.textContent === text;
  });
}

function renderGrid(
  results: InvoiceItemSearchResultDto[],
  overrides: Partial<{
    searchTerm: string;
    focusIndex: number;
    onAdd: (item: InvoiceItemSearchResultDto) => void;
  }> = {},
) {
  const onAdd = overrides.onAdd ?? vi.fn();
  const utils = render(
    <SalesItemSearchResultsGrid
      results={results}
      searchTerm={overrides.searchTerm ?? "club"}
      focusIndex={overrides.focusIndex ?? -1}
      onAdd={onAdd}
      onHoverIndex={vi.fn()}
      registerResultRef={vi.fn()}
    />,
  );
  return { onAdd, ...utils };
}

describe("SalesItemSearchResultsGrid", () => {
  it("muestra el encabezado de columnas Código/Producto/Stock/Precio reg./Promo/Precio final/Agregar", () => {
    const { container } = renderGrid([makeResult()]);
    const header = container.querySelector(".sf-search-columns-header");
    expect(header).not.toBeNull();
    const headerText = header?.textContent ?? "";
    expect(headerText).toContain("Código");
    expect(headerText).toContain("Producto");
    expect(headerText).toContain("Stock");
    expect(headerText).toContain("Precio reg.");
    expect(headerText).toContain("Promo");
    expect(headerText).toContain("Precio final");
    expect(headerText).toContain("Agregar");
  });

  it("muestra código y nombre del producto, con highlight del término buscado", () => {
    const { container } = renderGrid([makeResult()], { searchTerm: "club" });
    expect(screen.getByText("15865")).not.toBeNull();
    expect(container.querySelector(".sf-result__name")?.textContent).toBe(
      "CLUB 850CC RB CAJA X12",
    );
    const mark = container.querySelector(".sf-search-highlight");
    expect(mark).not.toBeNull();
    expect(mark?.textContent?.toLowerCase()).toBe("club");
  });

  it("producto con descuento muestra Precio reg. (tachado) → Promo -5% → Precio final", () => {
    const { container } = renderGrid([
      makeResult({
        salePriceWithoutTax: 2.2,
        finalSalePrice: 2.2,
        priceListName: "Lista General",
        discountDescription: "Descuento 5% (regla general)",
        discountedSalePriceWithoutTax: 2.09,
        discountedFinalSalePrice: 2.09,
      }),
    ]);

    expect(getMoneyValueByText("$2.20")).toBeTruthy();
    expect(getMoneyValueByText("$2.09")).toBeTruthy();
    expect(screen.getByText("-5%")).toBeTruthy();

    const normalValue = container.querySelector(".sf-result__price-normal");
    expect(normalValue?.className).not.toContain("--flat");
    const finalValue = getMoneyValueByText("$2.09");
    expect(finalValue.className).toContain("sf-result__price-final");
  });

  it("no muestra 'regla general' ni 'excepción' (texto técnico interno) en la fila principal", () => {
    const { container } = renderGrid([
      makeResult({
        discountDescription: "Descuento 5% (regla general)",
        priceListName: "Lista General",
        discountedFinalSalePrice: 26.55,
      }),
    ]);
    expect(screen.queryByText(/regla general/i)).toBeNull();
    expect(screen.queryByText(/excepción/i)).toBeNull();
    expect(screen.queryByText("Lista General")).toBeNull();
    const promoCell = container.querySelector(".sf-result__col-promo");
    expect(promoCell?.getAttribute("title")).toBe(
      "Descuento 5% (regla general) — Lista: Lista General",
    );
  });

  it("producto con recargo muestra +3%, y ajuste no porcentual muestra 'Ajuste aplicado'", () => {
    const { container, unmount } = renderGrid([
      makeResult({
        salePriceWithoutTax: 10,
        discountDescription: "Recargo 3% (regla general)",
        discountedFinalSalePrice: 10.3,
      }),
    ]);
    expect(screen.getByText("+3%")).toBeTruthy();
    unmount();

    renderGrid([
      makeResult({
        salePriceWithoutTax: 10,
        discountDescription: "Precio fijo 8 (excepción)",
        discountedFinalSalePrice: 8,
      }),
    ]);
    expect(screen.getByText("Ajuste aplicado")).toBeTruthy();
    expect(container).toBeDefined();
  });

  it("producto sin descuento muestra Promo '—' (sin inventar porcentaje)", () => {
    const { container } = renderGrid([
      makeResult({ discountDescription: null, priceListName: null }),
    ]);
    expect(container.querySelector(".sf-result__discount-tag")).toBeNull();
    expect(container.querySelector(".sf-result__col-promo")?.textContent).toBe(
      "—",
    );
  });

  it("stock bajo (≤5) sigue visible junto a la cantidad, sin mezclarse con el nombre", () => {
    const { container } = renderGrid([makeResult({ availableStock: 2 })]);
    expect(screen.getByText(/stock bajo/i)).not.toBeNull();
    const stockCell = container.querySelector(".sf-result__col-stock");
    expect(stockCell?.textContent).toMatch(/stock bajo/i);
    expect(stockCell?.textContent).not.toContain("CLUB 850CC RB CAJA X12");
  });

  it("sin stock (0) muestra el badge correspondiente y la cantidad real, nunca un guion", () => {
    const { container } = renderGrid([makeResult({ availableStock: 0 })]);
    expect(screen.getByText(/sin stock/i)).not.toBeNull();
    expect(container.querySelector(".sf-result__stock-qty")?.textContent).toBe(
      "0.0000 CAJA",
    );
  });

  it("sin precio configurado muestra 'Sin precio' / 'Sin precio configurado', nunca $0.00", () => {
    renderGrid([makeResult({ salePriceWithoutTax: null, finalSalePrice: null })]);
    expect(screen.getByText("Sin precio")).not.toBeNull();
    expect(screen.getByText("Sin precio configurado")).not.toBeNull();
    expect(screen.queryByText("$0.00")).toBeNull();
  });

  it("el botón Agregar llama a onAdd con el ítem correspondiente", () => {
    const onAdd = vi.fn();
    renderGrid([makeResult()], { onAdd });
    screen.getByRole("button", { name: /agregar/i }).click();
    expect(onAdd).toHaveBeenCalledWith(expect.objectContaining({ id: "item-1" }));
  });

  it("hacer click en la fila también llama a onAdd (misma acción que el botón)", () => {
    const onAdd = vi.fn();
    const { container } = renderGrid([makeResult()], { onAdd });
    const row = container.querySelector(".sf-result") as HTMLElement;
    row.click();
    expect(onAdd).toHaveBeenCalledWith(expect.objectContaining({ id: "item-1" }));
  });

  it("no introduce estilos inline", () => {
    const { container } = renderGrid([makeResult()]);
    expect(container.querySelectorAll("[style]").length).toBe(0);
  });
});
