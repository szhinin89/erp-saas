import { beforeEach, describe, expect, it, vi } from "vitest";

const apiGet = vi.hoisted(() => vi.fn());
vi.mock("../../lib/apiEnvelope", () => ({ apiGet, apiPost: vi.fn(), apiPut: vi.fn(), apiPatch: vi.fn(), apiDelete: vi.fn() }));

import { itemService } from "./itemService";

const ITEMS = "/api/v1/items";

/** ZH-INVENTORY-STOCK-ITEM-LOOKUP-01 — filtro opcional de GET /items. */
describe("itemService.getAll — participatesInInventory", () => {
  beforeEach(() => {
    apiGet.mockReset();
    apiGet.mockResolvedValue({ items: [], totalCount: 0, pageNumber: 1, pageSize: 12 });
  });

  it("sin participatesInInventory la URL es la de siempre (sin el parámetro)", async () => {
    await itemService.getAll({ search: "arroz", isActive: true, pageSize: 12 });
    expect(apiGet).toHaveBeenCalledWith(`${ITEMS}?search=arroz&isActive=true&pageNumber=1&pageSize=12`);
  });

  it("con participatesInInventory=true lo envía para que el backend filtre antes de paginar", async () => {
    await itemService.getAll({ search: "arroz", isActive: true, pageSize: 12, participatesInInventory: true });
    expect(apiGet).toHaveBeenCalledWith(`${ITEMS}?search=arroz&isActive=true&participatesInInventory=true&pageNumber=1&pageSize=12`);
  });
});
