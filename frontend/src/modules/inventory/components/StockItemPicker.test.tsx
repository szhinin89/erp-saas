// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

const search = vi.hoisted(() => vi.fn());
vi.mock("../../items/facades/itemLookupFacade", () => ({ itemLookupFacade: { search } }));

import { I18nProvider } from "../../../i18n/i18n";
import { StockItemPicker } from "./StockItemPicker";

/**
 * ZH-PRODUCT-SELECTOR-SSOT-01 — picker único de los documentos de inventario (reemplaza las dos
 * copias de Ajustes y Transferencias): solo ítems con control de stock y perfil de línea común.
 */
describe("StockItemPicker", () => {
  afterEach(cleanup);

  it("solo ofrece ítems que controlan stock y entrega id, sku, nombre y unidad base", async () => {
    search.mockResolvedValue({
      items: [
        { id: "1", sku: "SKU-1", shortName: "Arroz 1kg", description: "", tracksStock: true, defaultUomCode: "UN" },
        { id: "2", sku: "SRV-1", shortName: "Servicio de flete", description: "", tracksStock: false, defaultUomCode: "UN" },
      ],
      totalCount: 2,
      pageNumber: 1,
      pageSize: 12,
    });
    const onSelect = vi.fn();
    render(
      <I18nProvider>
        <StockItemPicker placeholder="Buscar" emptyText={() => "Nada"} onSelect={onSelect} />
      </I18nProvider>,
    );

    fireEvent.change(screen.getByLabelText("Buscar"), { target: { value: "1kg" } });
    fireEvent.click(await screen.findByText("Arroz 1kg"));

    expect(screen.queryByText("Servicio de flete")).toBeNull();
    expect(search).toHaveBeenCalledWith({ search: "1kg", isActive: true, pageSize: 12 });
    expect(onSelect).toHaveBeenCalledWith({ id: "1", sku: "SKU-1", name: "Arroz 1kg", baseUomCode: "UN" });
  });
});
