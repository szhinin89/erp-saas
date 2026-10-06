// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

const search = vi.hoisted(() => vi.fn());
vi.mock("../facades/itemLookupFacade", () => ({ itemLookupFacade: { search } }));

import { I18nProvider } from "../../../i18n/i18n";
import { ItemLookupPicker } from "./ItemLookupPicker";
import type { ItemDto } from "../../../types/items";

const ITEMS = [
  { id: "1", sku: "SKU-1", shortName: "Arroz 1kg", description: "Arroz blanco", participatesInInventory: true },
  { id: "2", sku: "SKU-2", shortName: "Azúcar 1kg", description: "Azúcar", participatesInInventory: false },
] as unknown as ItemDto[];

function renderPicker(props: Partial<Parameters<typeof ItemLookupPicker>[0]> = {}) {
  const onSelect = vi.fn();
  render(
    <I18nProvider>
      <ItemLookupPicker
        placeholder="Buscar producto"
        emptyText={(q) => `Sin resultados para ${q}`}
        onSelect={onSelect}
        {...props}
      />
    </I18nProvider>,
  );
  return { onSelect, input: screen.getByLabelText("Buscar producto") as HTMLInputElement };
}

/** ZH-PRODUCT-SELECTOR-SSOT-01 — picker manual de Item del owner items. */
describe("ItemLookupPicker", () => {
  beforeEach(() => {
    search.mockReset();
    search.mockResolvedValue({ items: ITEMS, totalCount: 2, pageNumber: 1, pageSize: 12 });
  });
  afterEach(cleanup);

  it("muestra SKU y nombre de cada resultado y entrega el ítem al hacer clic, limpiando la búsqueda", async () => {
    const { onSelect, input } = renderPicker();
    fireEvent.change(input, { target: { value: "arroz" } });
    fireEvent.click(await screen.findByText("Arroz 1kg"));
    expect(onSelect).toHaveBeenCalledWith(ITEMS[0]);
    expect(input.value).toBe("");
    expect(screen.queryByText("Azúcar 1kg")).toBeNull();
  });

  it("teclado: ↓ y Enter eligen; Escape cierra sin elegir", async () => {
    const { onSelect, input } = renderPicker();
    fireEvent.change(input, { target: { value: "kg" } });
    await screen.findByText("Azúcar 1kg");
    fireEvent.keyDown(input, { key: "Escape" });
    expect(screen.queryByText("Azúcar 1kg")).toBeNull();
    expect(onSelect).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: "kg1" } });
    await screen.findByText("Azúcar 1kg");
    fireEvent.keyDown(input, { key: "ArrowDown" });
    fireEvent.keyDown(input, { key: "ArrowDown" });
    fireEvent.keyDown(input, { key: "Enter" });
    expect(onSelect).toHaveBeenCalledWith(ITEMS[1]);
  });

  it("participatesInInventory se envía al backend; sin él, la búsqueda general no lo incluye", async () => {
    const { input } = renderPicker({ participatesInInventory: true });
    fireEvent.change(input, { target: { value: "kg" } });
    await screen.findByText("Arroz 1kg");
    expect(search).toHaveBeenCalledWith({ search: "kg", isActive: true, pageSize: 12, participatesInInventory: true });
    cleanup();

    search.mockClear();
    const plain = renderPicker();
    fireEvent.change(plain.input, { target: { value: "kg" } });
    await screen.findByText("Arroz 1kg");
    expect(search).toHaveBeenCalledWith({ search: "kg", isActive: true, pageSize: 12 });
  });

  it("sin resultados: texto del consumidor con la búsqueda", async () => {
    search.mockResolvedValue({ items: [], totalCount: 0, pageNumber: 1, pageSize: 12 });
    const { input } = renderPicker();
    fireEvent.change(input, { target: { value: "zzz" } });
    expect(await screen.findByText("Sin resultados para zzz")).toBeTruthy();
  });

  it("error de búsqueda: alerta visible, sin resultados", async () => {
    search.mockRejectedValue(new Error("network"));
    const { input } = renderPicker();
    fireEvent.change(input, { target: { value: "arroz" } });
    expect(await screen.findByRole("alert")).toBeTruthy();
    expect(screen.queryByText("Arroz 1kg")).toBeNull();
  });

  it("deshabilitado: no se puede escribir", () => {
    const { input } = renderPicker({ disabled: true });
    expect(input.disabled).toBe(true);
  });

  it("menos de 2 caracteres: no abre ni consulta", async () => {
    const { input } = renderPicker();
    fireEvent.change(input, { target: { value: "a" } });
    await new Promise((r) => setTimeout(r, 400));
    expect(search).not.toHaveBeenCalled();
    await waitFor(() => expect(screen.queryByText("Buscando...")).toBeNull());
  });
});
