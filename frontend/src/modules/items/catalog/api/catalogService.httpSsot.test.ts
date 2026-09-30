import { beforeEach, describe, expect, it, vi } from "vitest";

const apiGetMock = vi.fn();

vi.mock("../../../lib/apiEnvelope", () => ({
  apiGet: (...args: unknown[]) => apiGetMock(...args),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiPatch: vi.fn(),
}));

import {
  barcodeTypeService,
  brandService,
  sriLookupService,
} from "./catalogService";
import { categoryNodeService } from "./categoryNodeService";
import { sriLookupFacade } from "../../facades/sriLookupFacade";

/**
 * ZH-FRONTEND-HTTP-CLIENT-SSOT-01 — clientes canónicos de items/catalog que reemplazan los GET
 * inline de ItemFormTabs, useItemCreationCatalogs, VariantsSection y masterData/useSri*Types:
 * mismo endpoint y mismos parámetros efectivos que las llamadas eliminadas.
 */
describe("items/catalog — clientes canónicos por endpoint", () => {
  beforeEach(() => {
    apiGetMock.mockReset();
    apiGetMock.mockResolvedValue([]);
  });

  it("barcodeTypeService.list → GET /api/v1/catalog/barcode-types (antes inline en 3 componentes)", async () => {
    await barcodeTypeService.list();
    expect(apiGetMock).toHaveBeenCalledWith("/api/v1/catalog/barcode-types");
  });

  it("brandService.list() sin filtro → GET /api/v1/catalog/brands (mismo request que el inline)", async () => {
    await brandService.list();
    expect(apiGetMock).toHaveBeenCalledWith("/api/v1/catalog/brands");
  });

  it("categoryNodeService.getTree() → includeInactive=true (default del backend, igual que el GET sin query)", async () => {
    apiGetMock.mockResolvedValue({ nodes: [], maxDepth: 3 });
    await categoryNodeService.getTree();
    expect(apiGetMock).toHaveBeenCalledWith("/api/v1/catalog/category-nodes?includeInactive=true");
  });

  it("SRI UOM / IVA / ICE → /api/v1/catalog/sri-uom | sri-vat-rates | sri-ice-rates", async () => {
    await sriLookupService.uoms();
    await sriLookupService.vatRates();
    await sriLookupService.iceRates();
    expect(apiGetMock.mock.calls.map((c) => c[0])).toEqual([
      "/api/v1/catalog/sri-uom",
      "/api/v1/catalog/sri-vat-rates",
      "/api/v1/catalog/sri-ice-rates",
    ]);
  });

  it("tipos de proveedor e identificación SRI: mismos endpoints que masterData usaba inline", async () => {
    await sriLookupFacade.supplierTypes();
    await sriLookupFacade.idTypes();
    await sriLookupFacade.idTypes("customer");
    expect(apiGetMock.mock.calls.map((c) => c[0])).toEqual([
      "/api/v1/catalog/sri-supplier-types",
      "/api/v1/catalog/sri-id-types",
      "/api/v1/catalog/sri-id-types/by-usage/customer",
    ]);
  });

  it("sriLookupFacade delega en el service canónico (misma función, sin wrapper)", () => {
    expect(sriLookupFacade.supplierTypes).toBe(sriLookupService.supplierTypes);
    expect(sriLookupFacade.idTypes).toBe(sriLookupService.idTypes);
  });

  it("un error HTTP se propaga sin transformar (el consumidor decide el fallback)", async () => {
    const error = new Error("network");
    apiGetMock.mockRejectedValue(error);
    await expect(barcodeTypeService.list()).rejects.toBe(error);
  });
});
