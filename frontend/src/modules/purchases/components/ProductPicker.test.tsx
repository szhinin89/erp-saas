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

describe("ProductPicker — modo flotante (desglose XML)", () => {
  const ITEMS = [
    { ...ITEM, defaultUomAbbrev: "CJ" },
    { ...ITEM, id: "item-2", sku: "SKU-002", shortName: "Producto Dos", description: "Producto dos nombre completo", defaultUomAbbrev: "" },
  ];

  it("flota anclado al buscador como listbox accesible y muestra SKU, nombre completo y presentación", async () => {
    vi.mocked(itemLookupFacade.search).mockResolvedValue({ items: ITEMS, total: 2 } as never);
    render(<I18nProvider><ProductPicker floating onSelect={vi.fn()} /></I18nProvider>);
    await search("Producto");

    const input = screen.getByRole("combobox");
    const listbox = screen.getByRole("listbox");
    expect(input.getAttribute("aria-expanded")).toBe("true");
    expect(input.getAttribute("aria-controls")).toBe(listbox.id);
    expect(listbox.className).toContain("zh-picker__dropdown--floating");
    expect(listbox.style.width).not.toBe("");
    expect(listbox.style.maxHeight).not.toBe("");
    const options = screen.getAllByRole("option");
    expect(options).toHaveLength(2);
    expect(options[0].textContent).toContain("SKU-001");
    expect(options[0].textContent).toContain("Descripción del producto uno");
    expect(options[0].textContent).toContain("Presentación: CJ");
    expect(options[1].textContent).not.toContain("Presentación");
  });

  it("navega con teclado (activedescendant) y selecciona con Enter", async () => {
    vi.mocked(itemLookupFacade.search).mockResolvedValue({ items: ITEMS, total: 2 } as never);
    vi.mocked(itemLookupFacade.getById).mockResolvedValue({ ...ITEMS[1] } as never);
    const onSelect = vi.fn();
    render(<I18nProvider><ProductPicker floating onSelect={onSelect} /></I18nProvider>);
    await search("Producto");

    const input = screen.getByRole("combobox");
    fireEvent.keyDown(input, { key: "ArrowDown" });
    fireEvent.keyDown(input, { key: "ArrowDown" });
    const active = input.getAttribute("aria-activedescendant")!;
    expect(document.getElementById(active)!.getAttribute("aria-selected")).toBe("true");
    expect(document.getElementById(active)!.textContent).toContain("SKU-002");
    fireEvent.keyDown(input, { key: "Enter" });
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ id: "item-2" })));
    expect(screen.queryByRole("listbox")).toBeNull();
  });
});

describe("ProductPicker — reinicio del foco al llegar resultados (PURCHASES-PRODUCT-PICKER-RACE-01)", () => {
  const ITEMS = [
    { ...ITEM },
    { ...ITEM, id: "item-2", sku: "SKU-002", shortName: "Producto Dos", description: "Producto dos" },
  ];
  const OTHER = [
    { ...ITEM, id: "item-3", sku: "SKU-003", shortName: "Producto Tres", description: "Producto tres" },
    { ...ITEM, id: "item-4", sku: "SKU-004", shortName: "Producto Cuatro", description: "Producto cuatro" },
  ];

  /** Escribe `query`, espera la búsqueda pendiente y, al resolverla, pulsa ArrowDown en la primera
   * microtarea posterior a que React inserte las opciones en el DOM (antes de cualquier efecto diferido). */
  async function typeAndArrowDownOnArrival(input: HTMLElement, query: string, items: unknown[]) {
    let resolveSearch!: (value: unknown) => void;
    const calls = vi.mocked(itemLookupFacade.search).mock.calls.length;
    vi.mocked(itemLookupFacade.search).mockReturnValueOnce(
      new Promise((resolve) => { resolveSearch = resolve; }) as never,
    );
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: query } });
    await waitFor(() => expect(itemLookupFacade.search).toHaveBeenCalledTimes(calls + 1));
    const firstSku = (items[0] as { sku: string }).sku;
    const pressed = new Promise<void>((done) => {
      const observer = new MutationObserver(() => {
        if (!screen.queryByText(firstSku)) return;
        observer.disconnect();
        fireEvent.keyDown(input, { key: "ArrowDown" });
        done();
      });
      observer.observe(document.body, { childList: true, subtree: true, characterData: true });
    });
    resolveSearch({ items, total: items.length });
    await pressed;
  }

  function activeOption(input: HTMLElement) {
    const id = input.getAttribute("aria-activedescendant");
    return id ? document.getElementById(id) : null;
  }

  for (let run = 1; run <= 5; run++) {
    it(`conserva el ArrowDown pulsado justo al llegar los resultados (repetición ${run})`, async () => {
      render(<I18nProvider><ProductPicker floating onSelect={vi.fn()} /></I18nProvider>);
      const input = screen.getByRole("combobox");
      await typeAndArrowDownOnArrival(input, "Producto", ITEMS);

      await waitFor(() => expect(activeOption(input)?.textContent).toContain("SKU-001"));
      expect(activeOption(input)!.getAttribute("aria-selected")).toBe("true");
      fireEvent.keyDown(input, { key: "ArrowDown" });
      expect(activeOption(input)!.textContent).toContain("SKU-002");
    });
  }

  it("en búsquedas sucesivas no arrastra la selección anterior a los resultados nuevos", async () => {
    render(<I18nProvider><ProductPicker floating onSelect={vi.fn()} /></I18nProvider>);
    const input = screen.getByRole("combobox");
    await typeAndArrowDownOnArrival(input, "Producto", ITEMS);
    await waitFor(() => expect(activeOption(input)?.textContent).toContain("SKU-001"));
    fireEvent.keyDown(input, { key: "ArrowDown" });
    expect(activeOption(input)!.textContent).toContain("SKU-002");

    // Nueva búsqueda: mientras carga no hay opción activa; al llegar, el primer ArrowDown parte de cero.
    let resolveSecond!: (value: unknown) => void;
    vi.mocked(itemLookupFacade.search).mockReturnValueOnce(
      new Promise((resolve) => { resolveSecond = resolve; }) as never,
    );
    fireEvent.change(input, { target: { value: "Otro producto" } });
    expect(input.getAttribute("aria-activedescendant")).toBeNull();
    expect(screen.queryAllByRole("option")).toHaveLength(0);
    resolveSecond({ items: OTHER, total: OTHER.length });
    await screen.findByText("SKU-003");
    expect(input.getAttribute("aria-activedescendant")).toBeNull();
    expect(screen.getAllByRole("option").some((o) => o.getAttribute("aria-selected") === "true")).toBe(false);
    fireEvent.keyDown(input, { key: "ArrowDown" });
    expect(activeOption(input)!.textContent).toContain("SKU-003");
  });
});

describe("P0 - search failures and retry", () => {
  it("resets keyboard selection when the query changes and permits Escape with no results", async () => {
    vi.mocked(itemLookupFacade.search).mockResolvedValueOnce({ items: [ITEM], total: 1 } as never)
      .mockResolvedValueOnce({ items: [], total: 0 } as never);
    renderPicker();
    await search("Producto");
    const input = screen.getByRole("combobox");
    fireEvent.keyDown(input, { key: "ArrowDown" });
    expect(input.getAttribute("aria-activedescendant")).toBeTruthy();
    fireEvent.change(input, { target: { value: "another" } });
    expect(input.getAttribute("aria-activedescendant")).toBeNull();
    await waitFor(() => expect(screen.getByRole("status").textContent).toContain("another"));
    fireEvent.keyDown(input, { key: "Escape" });
    expect(input.getAttribute("aria-expanded")).toBe("false");
  });
  it("shows loading, then a real failure instead of no results; retries the same query", async () => {
    vi.mocked(itemLookupFacade.search).mockRejectedValueOnce(new Error("network"))
      .mockResolvedValueOnce({ items: [], total: 0 } as never);
    renderPicker();
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "missing" } });
    expect(screen.getByRole("status").textContent).toContain("Buscando");
    await screen.findByText("Error al consultar productos.");
    expect(screen.queryByText(/No se encontraron/)).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Reintentar" }));
    await waitFor(() => expect(itemLookupFacade.search).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.queryByText("Error al consultar productos.")).toBeNull());
    expect(screen.getByRole("status").textContent).toContain("missing");
    expect(itemLookupFacade.search).toHaveBeenLastCalledWith(expect.objectContaining({ search: "missing" }));
  });
});
