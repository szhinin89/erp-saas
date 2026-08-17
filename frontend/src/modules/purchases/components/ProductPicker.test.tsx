// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ProductPicker } from "./ProductPicker";
import { itemLookupFacade } from "../../items/facades/itemLookupFacade";

vi.mock("../../items/facades/itemLookupFacade", () => ({
  itemLookupFacade: {
    search: vi.fn(),
    getById: vi.fn(),
  },
}));

vi.mock("../utils/purchaseItemProfile", () => ({
  buildPurchaseItemProfile: vi.fn((item: { id: string }) => ({
    id: item.id,
    purchaseVatCode: "2",
    currentPvp: 12.5,
    vatRate: "15%",
  })),
}));

const ITEM = {
  id: "item-1",
  sku: "SKU-001",
  shortName: "Producto Uno",
  description: "Descripción del producto uno",
};

function renderPicker(onSelect = vi.fn()) {
  const utils = render(
    <I18nProvider>
      <ProductPicker onSelect={onSelect} />
    </I18nProvider>,
  );
  return { ...utils, onSelect };
}

async function search(query: string) {
  const input = screen.getByPlaceholderText("Buscar por SKU, nombre...");
  fireEvent.focus(input);
  fireEvent.change(input, { target: { value: query } });
  await screen.findByText("Producto Uno");
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("ProductPicker — fila de resultado (ZHPickerResultItem)", () => {
  it("renderiza los resultados de búsqueda usando ZHPickerResultItem (title=sku+nombre, subtitle=descripción)", async () => {
    vi.mocked(itemLookupFacade.search).mockResolvedValue({
      items: [ITEM],
      total: 1,
    } as never);

    renderPicker();
    await search("Producto");

    const row = screen.getByText("SKU-001").closest("button")!;
    expect(row.className.includes("zh-picker-result-item")).toBe(true);
    expect(screen.getByText("Descripción del producto uno")).toBeTruthy();
  });

  it("click en un resultado invoca onSelect igual que antes", async () => {
    vi.mocked(itemLookupFacade.search).mockResolvedValue({
      items: [ITEM],
      total: 1,
    } as never);
    vi.mocked(itemLookupFacade.getById).mockResolvedValue({
      id: "item-1",
      baseSalePrice: 12.5,
    } as never);

    const { onSelect } = renderPicker();
    await search("Producto");

    fireEvent.click(screen.getByText("SKU-001").closest("button")!);

    await waitFor(() => expect(onSelect).toHaveBeenCalledTimes(1));
  });

  it("no hay estilos inline en la fila de resultado", async () => {
    vi.mocked(itemLookupFacade.search).mockResolvedValue({
      items: [ITEM],
      total: 1,
    } as never);

    renderPicker();
    await search("Producto");

    const row = screen.getByText("SKU-001").closest("button")!;
    expect(row.getAttribute("style")).toBeNull();
  });

  it("el precio PVP en cache se muestra con ZHMoneyValue (sin estilos inline)", async () => {
    // Item con id único (no reutilizado por otros tests de este archivo) — el picker
    // cachea perfiles en un Map a nivel de módulo (`profileCache`), así que un id
    // repetido arrastraría estado de otro test.
    const cachedItem = { ...ITEM, id: "item-pvp-1", sku: "SKU-PVP" } as never;
    vi.mocked(itemLookupFacade.search).mockResolvedValue({
      items: [cachedItem],
      total: 1,
    } as never);
    vi.mocked(itemLookupFacade.getById).mockResolvedValue({
      id: "item-pvp-1",
      baseSalePrice: 12.5,
    } as never);

    renderPicker();
    const input = screen.getByPlaceholderText("Buscar por SKU, nombre...");
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "Producto" } });

    // Primer click puebla el cache local del picker (profileCache) y dispara onSelect.
    const firstRow = await screen.findByText("SKU-PVP");
    fireEvent.click(firstRow.closest("button")!);
    await waitFor(() => expect(itemLookupFacade.getById).toHaveBeenCalled());

    // Segunda búsqueda con el mismo ítem: ahora sí debe verse el bloque PVP/IVA (cache hit).
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "Producto" } });
    await screen.findByText("SKU-PVP");

    const money = document.querySelector(
      ".zh-picker__result-extra .zh-money-value",
    );
    expect(money).toBeTruthy();
    expect(money?.textContent).toBe("$12.50");
    expect(money?.getAttribute("style")).toBeNull();
  });
});
