// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";

const mocks = vi.hoisted(() => ({
  brands: vi.fn(),
  tree: vi.fn(),
  uoms: vi.fn(),
  vatRates: vi.fn(),
  iceRates: vi.fn(),
  barcodeTypes: vi.fn(),
}));

vi.mock("../../../modules/items/catalog/api/catalogService", () => ({
  brandService: { list: mocks.brands },
  sriLookupService: { uoms: mocks.uoms, vatRates: mocks.vatRates, iceRates: mocks.iceRates },
  barcodeTypeService: { list: mocks.barcodeTypes },
}));
vi.mock("../../../modules/items/catalog/api/categoryNodeService", () => ({
  categoryNodeService: { getTree: mocks.tree },
}));
vi.mock("../../../modules/items/hooks/useItemTypeOptions", () => ({
  useItemTypeOptions: () => ({ data: [{ id: "t1", name: "Mercadería" }], loading: false, error: null }),
}));

import { toLeafCategoryOptions, useItemCreationCatalogs } from "./useItemCreationCatalogs";
import type { CategoryNodeDto } from "../../../modules/items/catalog/api/categoryNodeService";

function node(id: string, name: string, path: string, parentId: string | null, isActive = true): CategoryNodeDto {
  return {
    id, name, path, parentId, isActive,
    code: id, description: null, level: "x", depth: path.split("/").filter(Boolean).length - 1,
    sortOrder: 0, createdAt: "2026-01-01T00:00:00Z", updatedAt: null,
  };
}

/**
 * ZH-FRONTEND-HTTP-CLIENT-SSOT-01 — única carga de catálogos de Item (ItemFormTabs, ItemEditorForm,
 * creación masiva de Compras): cada catálogo pasa por el service canónico de items/catalog; aquí
 * solo vive la normalización a categorías hoja.
 */
describe("useItemCreationCatalogs", () => {
  beforeEach(() => {
    mocks.brands.mockResolvedValue([{ id: "b1", name: "Marca" }]);
    mocks.tree.mockResolvedValue({
      nodes: [
        node("l1", "Línea", "/l1/", null),
        node("c1", "Categoría", "/l1/c1/", "l1"),
        node("s1", "Sub", "/l1/c1/s1/", "c1"),
        node("x1", "Inactiva", "/l1/x1/", "l1", false),
      ],
      maxDepth: 3,
    });
    mocks.uoms.mockResolvedValue([{ code: "UNI", name: "Unidad", abbrev: "u" }]);
    mocks.vatRates.mockResolvedValue([{ code: "4", name: "IVA 15%", percentage: 15 }]);
    mocks.iceRates.mockResolvedValue([{ code: "3011", name: "ICE", percentage: 10 }]);
    mocks.barcodeTypes.mockResolvedValue([{ code: "EAN13", name: "EAN-13" }]);
  });

  it("carga cada catálogo una vez por su service canónico y expone las opciones", async () => {
    const { result } = renderHook(() => useItemCreationCatalogs());
    await waitFor(() => expect(result.current.ready).toBe(true));
    await waitFor(() => expect(result.current.iceRateOptions).toHaveLength(1));

    expect(mocks.brands).toHaveBeenCalledWith();
    expect(mocks.tree).toHaveBeenCalledWith();
    for (const m of [mocks.brands, mocks.tree, mocks.uoms, mocks.vatRates, mocks.iceRates, mocks.barcodeTypes])
      expect(m).toHaveBeenCalledTimes(1);
    expect(result.current.brandOptions).toEqual([{ id: "b1", name: "Marca" }]);
    expect(result.current.categoryOptions).toEqual([{ id: "s1", name: "Línea > Categoría > Sub" }]);
    expect(result.current.uomOptions[0].code).toBe("UNI");
    expect(result.current.barcodeTypeOptions[0].code).toBe("EAN13");
    expect(result.current.vatRateByCode.get("4")?.percentage).toBe(15);
    expect(result.current.itemTypeOptions).toEqual([{ id: "t1", name: "Mercadería" }]);
  });

  it("un catálogo que falla queda vacío (mismo manejo de error que los GET inline)", async () => {
    mocks.brands.mockRejectedValue(new Error("boom"));
    mocks.barcodeTypes.mockRejectedValue(new Error("boom"));
    const { result } = renderHook(() => useItemCreationCatalogs());
    await waitFor(() => expect(result.current.ready).toBe(true));
    expect(result.current.brandOptions).toEqual([]);
    expect(result.current.barcodeTypeOptions).toEqual([]);
  });

  it("toLeafCategoryOptions: solo hojas activas, con ruta legible desde path", () => {
    const options = toLeafCategoryOptions([
      node("l1", "Línea", "/l1/", null),
      node("c1", "Categoría", "/l1/c1/", "l1"),
      node("c2", "Hoja directa", "/l1/c2/", "l1"),
    ]);
    expect(options).toEqual([
      { id: "c1", name: "Línea > Categoría" },
      { id: "c2", name: "Línea > Hoja directa" },
    ]);
  });
});
