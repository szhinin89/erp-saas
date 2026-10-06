// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";

const search = vi.hoisted(() => vi.fn());
vi.mock("../facades/itemLookupFacade", () => ({ itemLookupFacade: { search } }));

import { ITEM_LOOKUP_DEBOUNCE_MS, useItemLookupSearch } from "./useItemLookupSearch";
import type { ItemDto } from "../../../types/items";

const item = (id: string, sku: string, participatesInInventory = true) =>
  ({ id, sku, shortName: `Producto ${sku}`, description: "", participatesInInventory, defaultUomCode: "UN" }) as unknown as ItemDto;

const page = (items: ItemDto[]) => ({ items, totalCount: items.length, pageNumber: 1, pageSize: 12 });

/**
 * ZH-PRODUCT-SELECTOR-SSOT-01 — búsqueda canónica de Items de la selección manual: la usan el
 * picker del owner (inventario, precios) y Kardex. El ranking es del backend; aquí solo mecánica.
 */
describe("useItemLookupSearch", () => {
  beforeEach(() => {
    search.mockReset();
    search.mockResolvedValue(page([item("1", "SKU-1")]));
  });
  afterEach(() => vi.useRealTimers());

  it("no consulta con menos de 2 caracteres", async () => {
    const { result } = renderHook(() => useItemLookupSearch());
    act(() => result.current.setQuery("a"));
    await new Promise((r) => setTimeout(r, ITEM_LOOKUP_DEBOUNCE_MS + 50));
    expect(search).not.toHaveBeenCalled();
    expect(result.current.active).toBe(false);
    expect(result.current.results).toEqual([]);
  });

  it("debounce: una sola consulta con el último texto, recortado, solo activos y con el pageSize pedido", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const { result } = renderHook(() => useItemLookupSearch({ pageSize: 10 }));
    act(() => result.current.setQuery("ar"));
    act(() => result.current.setQuery("arr"));
    act(() => result.current.setQuery("  arroz "));
    expect(result.current.loading).toBe(true);
    await act(async () => {
      vi.advanceTimersByTime(ITEM_LOOKUP_DEBOUNCE_MS);
    });
    await waitFor(() => expect(result.current.results).toHaveLength(1));
    expect(search).toHaveBeenCalledTimes(1);
    expect(search).toHaveBeenCalledWith({ search: "arroz", isActive: true, pageSize: 10 });
    expect(result.current.loading).toBe(false);
  });

  it("por SKU o nombre: el texto se envía tal cual al backend (sin ranking en React)", async () => {
    search.mockResolvedValue(page([item("2", "SKU-2"), item("1", "SKU-1")]));
    const { result } = renderHook(() => useItemLookupSearch());
    act(() => result.current.setQuery("SKU"));
    await waitFor(() => expect(result.current.results).toHaveLength(2));
    expect(result.current.results.map((i) => i.id)).toEqual(["2", "1"]);
  });

  it("participatesInInventory se pide al backend y la página se usa tal cual (sin filtrar en React)", async () => {
    search.mockResolvedValue(page([item("1", "A", true), item("2", "B", true)]));
    const { result } = renderHook(() => useItemLookupSearch({ participatesInInventory: true }));
    act(() => result.current.setQuery("ab"));
    await waitFor(() => expect(result.current.results).toHaveLength(2));
    expect(search).toHaveBeenCalledWith({ search: "ab", isActive: true, pageSize: 12, participatesInInventory: true });
  });

  it("sin participatesInInventory la consulta no lleva el filtro (búsqueda general sin cambios)", async () => {
    const { result } = renderHook(() => useItemLookupSearch());
    act(() => result.current.setQuery("ab"));
    await waitFor(() => expect(search).toHaveBeenCalledTimes(1));
    expect(search.mock.calls[0][0]).toEqual({ search: "ab", isActive: true, pageSize: 12 });
    expect("participatesInInventory" in search.mock.calls[0][0]).toBe(false);
  });

  it("descarta una respuesta vieja que llega después de una búsqueda más nueva", async () => {
    let resolveOld: (v: unknown) => void = () => {};
    search
      .mockImplementationOnce(() => new Promise((r) => (resolveOld = r)))
      .mockResolvedValueOnce(page([item("new", "NEW")]));
    const { result } = renderHook(() => useItemLookupSearch());
    act(() => result.current.setQuery("old"));
    await waitFor(() => expect(search).toHaveBeenCalledTimes(1));
    act(() => result.current.setQuery("new"));
    await waitFor(() => expect(result.current.results.map((i) => i.id)).toEqual(["new"]));
    await act(async () => resolveOld(page([item("old", "OLD")])));
    expect(result.current.results.map((i) => i.id)).toEqual(["new"]);
  });

  it("error: lista vacía y mensaje", async () => {
    search.mockRejectedValue(new Error("boom"));
    const { result } = renderHook(() => useItemLookupSearch());
    act(() => result.current.setQuery("arroz"));
    await waitFor(() => expect(result.current.error).not.toBe(""));
    expect(result.current.results).toEqual([]);
    expect(result.current.loading).toBe(false);
  });

  it("reset limpia texto y resultados; enabled=false no consulta", async () => {
    const { result, rerender } = renderHook(({ enabled }) => useItemLookupSearch({ enabled }), {
      initialProps: { enabled: true },
    });
    act(() => result.current.setQuery("arroz"));
    await waitFor(() => expect(result.current.results).toHaveLength(1));
    act(() => result.current.reset());
    expect(result.current.query).toBe("");
    expect(result.current.results).toEqual([]);

    search.mockClear();
    rerender({ enabled: false });
    act(() => result.current.setQuery("azucar"));
    await new Promise((r) => setTimeout(r, ITEM_LOOKUP_DEBOUNCE_MS + 50));
    expect(search).not.toHaveBeenCalled();
  });
});
