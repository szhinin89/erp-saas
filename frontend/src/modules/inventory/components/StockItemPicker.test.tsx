// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

const search = vi.hoisted(() => vi.fn());
vi.mock("../../items/facades/itemLookupFacade", () => ({ itemLookupFacade: { search } }));

import { I18nProvider } from "../../../i18n/i18n";
import { StockItemPicker } from "./StockItemPicker";

type Row = { id: string; sku: string; shortName: string; description: string; participatesInInventory: boolean; defaultUomCode: string };

/** 12 coincidencias sin stock que ordenan primero (A01..A12) y una con stock (Z01) en la página 2. */
const CATALOG: Row[] = [
  ...Array.from({ length: 12 }, (_, i) => ({
    id: `a${i}`,
    sku: `LKP-A${String(i + 1).padStart(2, "0")}`,
    shortName: `Servicio ${i + 1}`,
    description: "",
    participatesInInventory: false,
    defaultUomCode: "UN",
  })),
  { id: "z1", sku: "LKP-Z01", shortName: "Arroz 1kg", description: "", participatesInInventory: true, defaultUomCode: "UN" },
];

/** Emula GET /items: filtra (participatesInInventory), ordena por SKU y recién entonces pagina. */
function backend(params: { pageSize: number; participatesInInventory?: boolean }) {
  const rows = CATALOG.filter((r) => params.participatesInInventory === undefined || r.participatesInInventory === params.participatesInInventory)
    .sort((a, b) => a.sku.localeCompare(b.sku))
    .slice(0, params.pageSize);
  return Promise.resolve({ items: rows, totalCount: rows.length, pageNumber: 1, pageSize: params.pageSize });
}

function renderPicker(onSelect = vi.fn()) {
  render(
    <I18nProvider>
      <StockItemPicker placeholder="Buscar" emptyText={() => "Nada"} onSelect={onSelect} />
    </I18nProvider>,
  );
  return onSelect;
}

/**
 * ZH-PRODUCT-SELECTOR-SSOT-01 / ZH-INVENTORY-STOCK-ITEM-LOOKUP-01 — picker único de los documentos
 * de inventario: pide al backend solo ítems con control de stock y entrega el perfil de línea común.
 */
describe("StockItemPicker", () => {
  afterEach(() => {
    cleanup();
    search.mockReset();
  });

  it("pide participatesInInventory=true y entrega id, sku, nombre y unidad base", async () => {
    search.mockImplementation(backend);
    const onSelect = renderPicker();

    fireEvent.change(screen.getByLabelText("Buscar"), { target: { value: "1kg" } });
    fireEvent.click(await screen.findByText("Arroz 1kg"));

    expect(search).toHaveBeenCalledWith({ search: "1kg", isActive: true, pageSize: 12, participatesInInventory: true });
    expect(onSelect).toHaveBeenCalledWith({ id: "z1", sku: "LKP-Z01", name: "Arroz 1kg", baseUomCode: "UN" });
  });

  it("encuentra el ítem con stock aunque antes haya más de una página de coincidencias sin stock", async () => {
    search.mockImplementation(backend);
    renderPicker();

    fireEvent.change(screen.getByLabelText("Buscar"), { target: { value: "LKP" } });

    expect(await screen.findByText("Arroz 1kg")).toBeTruthy();
    expect(screen.queryByText("Servicio 1")).toBeNull();
    expect(screen.queryByText("Nada")).toBeNull();
  });
});
