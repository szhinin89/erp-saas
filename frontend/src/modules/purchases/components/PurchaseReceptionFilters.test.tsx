// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseReceptionFilters, type PurchaseReceptionFiltersProps } from "./PurchaseReceptionFilters";
import { EMPTY_RECEPTION_FILTERS } from "../utils/receptionFilters";

afterEach(cleanup);

function renderFilters(overrides: Partial<PurchaseReceptionFiltersProps> = {}) {
  const props: PurchaseReceptionFiltersProps = {
    filters: EMPTY_RECEPTION_FILTERS,
    setFilter: vi.fn(),
    advancedFilterCount: 0,
    supplierOptions: [{ ruc: "111", name: "Alfa" }],
    selectedSupplierOptions: [],
    setSupplierFilter: vi.fn(),
    dateSortOrder: "desc",
    setDateSortOrder: vi.fn(),
    clearFilters: vi.fn(),
    shownCount: 3,
    totalCount: 10,
    ...overrides,
  };
  const view = render(
    <I18nProvider>
      <PurchaseReceptionFilters {...props} />
    </I18nProvider>,
  );
  return { props, view };
}

describe("PurchaseReceptionFilters (PURCHASES-RECEPTION-FILTERS-01)", () => {
  it("muestra búsqueda, proveedor, orden, atención y el contador N de M; los avanzados empiezan plegados", () => {
    const { view, props } = renderFilters();
    expect(screen.getByRole("textbox", { name: "Buscar" })).toBeTruthy();
    expect(screen.getByRole("combobox", { name: "Orden por emisión" })).toBeTruthy();
    expect(screen.getByText("Mostrando 3 de 10 comprobantes")).toBeTruthy();
    // Línea 1 compacta: sin rótulos visibles (aria-label + placeholder).
    expect(view.container.querySelector(".zh-filterbar--plain")).toBeTruthy();
    expect(view.container.querySelectorAll(".zh-field-label")).toHaveLength(0);
    // "Solo requieren atención": checkbox del DS + ayuda contextual, sin tarjeta.
    const attention = screen.getByRole("checkbox", { name: "Solo requieren atención" });
    expect(attention.closest(".zh-checkbox-label")).toBeTruthy();
    expect(view.container.querySelector(".zh-toggle")).toBeNull();
    expect(screen.getByRole("button", { name: "Solo requieren atención", expanded: false })).toBeTruthy();
    fireEvent.click(attention);
    expect(props.setFilter).toHaveBeenCalledWith("attentionOnly", true);
    const more = screen.getByRole("button", { name: "Más filtros" });
    expect(more.getAttribute("aria-expanded")).toBe("false");
    expect(screen.queryByText("Situación ERP")).toBeNull();
  });

  it("la ayuda contextual explica el criterio sin ocupar una línea permanente", () => {
    renderFilters();
    expect(screen.queryByText(/proveedor pendiente o inactivo/)).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Solo requieren atención", expanded: false }));
    expect(screen.getByText(/proveedor pendiente o inactivo/)).toBeTruthy();
  });

  it("el botón disclosure despliega los criterios avanzados con controles del DS", () => {
    const { view } = renderFilters();
    fireEvent.click(screen.getByRole("button", { name: "Más filtros" }));
    expect(screen.getByRole("button", { name: "Más filtros" }).getAttribute("aria-expanded")).toBe("true");
    for (const label of ["Situación ERP", "Estado proveedor", "XML", "Recepción", "Lectura", "Tipo"]) {
      expect(screen.getByText(label)).toBeTruthy();
    }
    expect(view.container.querySelector(".pur-reception-filters__advanced")).toBeTruthy();
    // Situación ERP incluye las tres tarjetas (estado único compartido con los KPI).
    for (const option of ["Registrados en ERP", "Por procesar", "Pendientes de proveedor", "Sin registrar", "Historial anulado"]) {
      expect(screen.getByRole("option", { name: option })).toBeTruthy();
    }
    expect(view.container.querySelectorAll('input[type="date"]')).toHaveLength(2);
    expect(view.container.querySelectorAll(".zh-prefixed-input")).toHaveLength(2);
    // Recepción: solo los estados que el backend emite (sin Anulado).
    expect(screen.queryByRole("option", { name: "Anulado" })).toBeNull();
  });

  it("cada control publica su criterio", () => {
    const { props } = renderFilters();
    fireEvent.change(screen.getByRole("textbox", { name: "Buscar" }), { target: { value: "0999" } });
    expect(props.setFilter).toHaveBeenCalledWith("search", "0999");

    fireEvent.click(screen.getByRole("button", { name: "Más filtros" }));
    const situation = screen.getByText("Situación ERP").closest(".zh-field")!.querySelector("select")!;
    fireEvent.change(situation, { target: { value: "cancelledHistory" } });
    expect(props.setFilter).toHaveBeenCalledWith("erpSituation", "cancelledHistory");

    fireEvent.change(screen.getByLabelText("Total mínimo"), { target: { value: "25.5" } });
    expect(props.setFilter).toHaveBeenCalledWith("totalMin", "25.5");

    fireEvent.click(screen.getByRole("button", { name: /Limpiar filtros/ }));
    expect(props.clearFilters).toHaveBeenCalled();
  });

  it("indica cuántos criterios avanzados están activos aunque la zona esté plegada", () => {
    renderFilters({ advancedFilterCount: 2 });
    expect(screen.getByRole("button", { name: "Más filtros (2)" })).toBeTruthy();
  });
});
