// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../../i18n/i18n";
import { KardexPage } from "./KardexPage";

const state = vi.hoisted(() => ({ current: {} as Record<string, unknown> }));
vi.mock("../../../../access/usePermissionsUi", () => ({ usePermissionsUi: () => ({ canShow: () => true }) }));
vi.mock("../hooks/useInventoryInvestigationPage", () => ({ useInventoryInvestigationPage: () => state.current }));
const products = [
  { id: "p-1", sku: "SKU-001", shortName: "Producto de prueba" },
  { id: "p-2", sku: "SKU-002", shortName: "Otro producto" },
];
beforeEach(() => {
  state.current = {
    searchMode: "product", productQuery: "producto", productResults: products,
    productSearching: false, selectedProduct: null, selectedProductId: "",
    selectedWarehouseId: "", warehouses: [], dateFrom: "", dateTo: "", selectedDoc: null,
    movements: [], movementsLoading: false, movementTypeFilter: "", summary: null,
    detailOpen: false, detailLoading: false, detail: null,
    loadWarehouses: vi.fn(), clearSelectedProduct: vi.fn(), searchProducts: vi.fn(),
    selectProduct: vi.fn(), runSearch: vi.fn(), setSearchMode: vi.fn(),
  };
});
afterEach(() => cleanup());
function showPage() {
  return render(<I18nProvider><MemoryRouter><KardexPage /></MemoryRouter></I18nProvider>);
}

it("mantiene la búsqueda existente al editar el producto", () => {
  showPage();
  fireEvent.change(screen.getByPlaceholderText("Buscar por SKU o nombre..."), { target: { value: "SKU-00" } });
  expect(state.current.clearSelectedProduct).toHaveBeenCalledOnce();
  expect(state.current.searchProducts).toHaveBeenCalledWith("SKU-00");
});

it("muestra SKU y nombre diferenciados y selecciona el objeto original", () => {
  showPage();
  fireEvent.click(screen.getByRole("button", { name: /SKU-002\s*Otro producto/ }));
  expect(state.current.selectProduct).toHaveBeenCalledWith(products[1]);
  expect(state.current.runSearch).not.toHaveBeenCalled();
});

it.each([false, true])("con cero resultados conserva filtros y estado de carga %s", (loading) => {
  state.current.productResults = [];
  state.current.productSearching = loading;
  showPage();
  expect(screen.queryByRole("button", { name: /SKU-00/ })).toBeNull();
  expect(screen.getByLabelText("Bodega (opcional)")).toBeTruthy();
  expect(screen.getByLabelText("Desde")).toBeTruthy();
  expect(screen.getByLabelText("Hasta")).toBeTruthy();
  expect((screen.getByRole("button", { name: /Buscar$/ }) as HTMLButtonElement).disabled).toBe(true);
  expect(state.current.searchProducts).not.toHaveBeenCalled();
});

it("conserva los resultados existentes durante una búsqueda en curso", () => {
  state.current.productSearching = true;
  showPage();
  expect(screen.getByRole("button", { name: /SKU-001\s*Producto de prueba/ })).toBeTruthy();
});
