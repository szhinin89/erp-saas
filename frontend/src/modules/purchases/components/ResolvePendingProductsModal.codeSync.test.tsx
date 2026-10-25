// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ResolvePendingProductsModal } from "./ResolvePendingProductsModal";
import type { ItemCodeConflict } from "../../items/facades/itemLookupFacade";

const mocks = vi.hoisted(() => ({ lookup: vi.fn(), resolve: vi.fn() }));
vi.mock("../../items/facades/itemLookupFacade", () => ({
  itemLookupFacade: { codeConflicts: mocks.lookup, refreshCodeConflicts: vi.fn() },
  codeConflictRevision: () => 0, subscribeCodeConflicts: () => () => undefined,
}));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: () => ({ canShow: () => true }) }));
vi.mock("../api/purchaseReceptionService", () => ({ purchaseReceptionService: { resolveLines: mocks.resolve } }));
vi.mock("./ProductPicker", () => ({ ProductPicker: () => null }));
vi.mock("../../../i18n/i18n", () => ({ useI18n: () => ({ t: (key: string, p?: unknown) =>
  typeof p === "string" ? p : p && typeof p === "object" && "count" in p ? `${key}:${p.count}` : key }) }));
vi.mock("../../../components/items/ItemEditorModal/useItemCreationCatalogs", () => ({ useItemCreationCatalogs: () => ({
  barcodeTypeOptions: [], itemTypeOptions: [{ id: "type", name: "Producto" }], brandOptions: [{ id: "brand", name: "Marca" }],
  categoryOptions: [{ id: "category", name: "Categoria" }], uomOptions: [{ code: "19", name: "Unidad" }],
  vatRateOptions: [{ code: "0", name: "IVA 0", percentage: 0 }], vatRateByCode: new Map(), iceRateOptions: [],
}) }));
const deferred = () => { let resolve!: (value: ItemCodeConflict[]) => void; let reject!: (reason: Error) => void;
  const promise = new Promise<ItemCodeConflict[]>((yes, no) => { resolve = yes; reject = no; }); return { promise, resolve, reject }; };
let requests: ReturnType<typeof deferred>[];
beforeEach(() => { requests = []; mocks.resolve.mockReset(); mocks.lookup.mockReset().mockImplementation(() => {
  const request = deferred(); requests.push(request); return request.promise;
}); });
afterEach(cleanup);
const change = (name: string, value: string) => fireEvent.change(document.querySelector(`[name="rows.0.${name}"]`)!, { target: { value } });
const button = () => screen.getByRole("button", { name: "purchases.pendingProducts.apply:1" }) as HTMLButtonElement;
const footer = () => document.querySelector(".pbd-footer-status")!.textContent;
const expectCount = (ready: number, pending: number) => {
  expect(footer()).toContain(`purchases.pendingProducts.footer.ready:${ready}`);
  expect(footer()).toContain(`purchases.pendingProducts.footer.pending:${pending}`);
};
const duplicate = (code: string): ItemCodeConflict[] => [{ kind: "Sku", code, itemId: "owner", itemName: "Propietario", rows: ["1"] }];
function mount() {
  render(<MemoryRouter><ResolvePendingProductsModal open sources={[{ rowId: "r1", lineKey: 1, supplierCode: "VALID",
    supplierAuxCode: "", xmlDescription: "Producto", quantity: 1, unitPrice: 2, discountPct: 0, vatCode: "0", iceCode: "", matchStatus: "PENDING" }]}
    supplierName="Proveedor" onClose={vi.fn()} onResolved={vi.fn()} /></MemoryRouter>);
  for (const [name, value] of Object.entries({ itemTypeId: "type", brandId: "brand", categoryNodeId: "category", defaultUomCode: "19", salePriceGross: "5" })) change(name, value);
}
async function reply(index: number, value: ItemCodeConflict[]) {
  await waitFor(() => expect(requests.length).toBeGreaterThan(index));
  await act(async () => { requests[index].resolve(value); });
}
describe("purchase code diagnostics synchronization", () => {
  it("counts a detected duplicate as pending and disables resolution before any save", async () => {
    mount(); await reply(0, []); expectCount(1, 0);
    change("sku", "DUP"); await reply(1, duplicate("DUP"));
    expect(document.querySelector('[name="rows.0.sku"]')!.getAttribute("aria-invalid")).toBe("true");
    expectCount(0, 1); expect(button().disabled).toBe(true); expect(mocks.resolve).not.toHaveBeenCalled();
  });
  it("moves valid to pending to duplicate to corrected without saving", async () => {
    mount(); expectCount(0, 1); expect(button().disabled).toBe(true);
    await reply(0, []); expectCount(1, 0); expect(button().disabled).toBe(false);
    change("sku", "DUP"); expectCount(0, 1); expect(button().disabled).toBe(true);
    await reply(1, duplicate("DUP")); expectCount(0, 1);
    change("sku", "FIXED"); expectCount(0, 1); expect(button().disabled).toBe(true);
    expect(document.querySelector('[name="rows.0.sku"]')!.getAttribute("aria-invalid")).not.toBe("true");
    await reply(2, []); expectCount(1, 0); expect(button().disabled).toBe(false);
    expect(document.querySelector(".zh-line-issues")).toBeNull(); expect(mocks.resolve).not.toHaveBeenCalled();
  });
  it("ignores stale duplicate and stale success responses in both orders", async () => {
    mount(); await waitFor(() => expect(requests).toHaveLength(1));
    change("sku", "NEW"); await reply(1, []); expectCount(1, 0);
    await reply(0, duplicate("VALID")); expectCount(1, 0); expect(button().disabled).toBe(false);
    change("sku", "OLD-VALID"); await waitFor(() => expect(requests).toHaveLength(3));
    change("sku", "NEW-DUP"); await reply(3, duplicate("NEW-DUP"));
    await reply(2, []); expectCount(0, 1); expect(button().disabled).toBe(true);
    expect(document.querySelector('[name="rows.0.sku"]')!.getAttribute("aria-invalid")).toBe("true");
  });
  it("does not claim uniqueness when the diagnostic request fails", async () => {
    mount(); await waitFor(() => expect(requests).toHaveLength(1));
    await act(async () => requests[0].reject(new Error("offline")));
    expectCount(0, 1); expect(button().disabled).toBe(true); expect(mocks.resolve).not.toHaveBeenCalled();
  });
});
