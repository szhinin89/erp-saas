import { beforeEach, describe, expect, it, vi } from "vitest";
import { branchService, normalizeGeographyList } from "./branchService";
import { geographyLookupFacade } from "../facades/geographyLookupFacade";
import { apiGet } from "../../lib/apiEnvelope";

/**
 * ZH-FRONTEND-GEOGRAPHY-SSOT-01 — contrato del ÚNICO cliente HTTP de geografía del frontend
 * (GET /api/v1/settings/geography/*, owner backend: ERP.Application/Modules/Branches).
 */
vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiPatch: vi.fn(),
}));

const get = vi.mocked(apiGet);
const BASE = "/api/v1/settings/geography";

beforeEach(() => {
  get.mockReset();
  get.mockResolvedValue([{ id: "01", name: "Azuay" }]);
});

describe("branchService — geografía (endpoints y parámetros)", () => {
  it("países", async () => {
    await expect(branchService.countries()).resolves.toEqual([{ id: "01", name: "Azuay" }]);
    expect(get).toHaveBeenCalledWith(`${BASE}/countries`);
  });

  it("provincias por país", async () => {
    await branchService.provinces("EC");
    expect(get).toHaveBeenCalledWith(`${BASE}/provinces?countryId=EC`);
  });

  it("cantones por provincia", async () => {
    await branchService.cantons("01");
    expect(get).toHaveBeenCalledWith(`${BASE}/cantons?provinceId=01`);
  });

  it("parroquias por cantón", async () => {
    await branchService.parishes("0101");
    expect(get).toHaveBeenCalledWith(`${BASE}/parishes?cantonId=0101`);
  });

  it("codifica los parámetros de query", async () => {
    await branchService.cantons("a b&c");
    expect(get).toHaveBeenCalledWith(`${BASE}/cantons?provinceId=a%20b%26c`);
  });

  it("propaga el error HTTP sin tragarlo (el consumidor decide)", async () => {
    const err = new Error("403");
    get.mockRejectedValueOnce(err);
    await expect(branchService.provinces("EC")).rejects.toBe(err);
  });
});

describe("normalizeGeographyList — GeographyItemDto { id, name }", () => {
  it("acepta el DTO canónico y variantes PascalCase / Item1-Item2 / tupla", () => {
    expect(
      normalizeGeographyList([
        { id: "01", name: "Azuay" },
        { Id: "02", Name: "Bolívar" },
        { item1: "03", item2: "Cañar" },
        { Item1: "04", Item2: "Carchi" },
        ["05", "Cotopaxi"],
      ]),
    ).toEqual([
      { id: "01", name: "Azuay" },
      { id: "02", name: "Bolívar" },
      { id: "03", name: "Cañar" },
      { id: "04", name: "Carchi" },
      { id: "05", name: "Cotopaxi" },
    ]);
  });

  it("descarta filas sin id o nulas y devuelve [] si la respuesta no es lista", () => {
    expect(normalizeGeographyList([{ name: "sin id" }, null, 7])).toEqual([]);
    expect(normalizeGeographyList({ data: [] })).toEqual([]);
    expect(normalizeGeographyList(null)).toEqual([]);
  });
});

describe("geographyLookupFacade — contrato público", () => {
  it("delega exactamente en el cliente único (sin segundo camino)", () => {
    expect(geographyLookupFacade.countries).toBe(branchService.countries);
    expect(geographyLookupFacade.provinces).toBe(branchService.provinces);
    expect(geographyLookupFacade.cantons).toBe(branchService.cantons);
    expect(geographyLookupFacade.parishes).toBe(branchService.parishes);
  });
});
