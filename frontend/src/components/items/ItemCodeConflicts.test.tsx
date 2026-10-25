// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ItemCodeConflicts } from "./ItemCodeConflicts";

const mocks = vi.hoisted(() => ({ conflicts: vi.fn(), canView: true }));
vi.mock("../../modules/items/facades/itemLookupFacade", () => ({
  itemLookupFacade: { codeConflicts: mocks.conflicts },
  codeConflictRevision: () => 0,
  subscribeCodeConflicts: () => () => undefined,
}));
vi.mock("../../access/usePermissionsUi", () => ({ usePermissionsUi: () => ({ canShow: () => mocks.canView }) }));
vi.mock("../../i18n/i18n", () => ({ useI18n: () => ({
  t: (key: string, params?: Record<string, string>) => params?.rows ? `${key}:${params.rows}` : key,
}) }));

const renderConflicts = (codes: Parameters<typeof ItemCodeConflicts>[0]["codes"]) =>
  render(<MemoryRouter><ItemCodeConflicts codes={codes} /></MemoryRouter>);

beforeEach(() => { mocks.conflicts.mockReset(); mocks.canView = true; });
afterEach(cleanup);

describe("ItemCodeConflicts", () => {
  it("shows the code, the existing product and opens it in another tab", async () => {
    mocks.conflicts.mockResolvedValue([{ kind: "Barcode", code: "7861234567898", itemId: "item-1", itemName: "Arroz 1 kg", rows: ["2"] }]);
    renderConflicts([{ kind: "Barcode", code: "7861234567898", row: "2", targetKey: "a" }]);
    const link = await screen.findByRole("link", { name: "items.codeConflicts.view" });
    expect(link.getAttribute("href")).toBe("/products/items?view=item-1");
    expect(link.getAttribute("target")).toBe("_blank");
    expect(link.getAttribute("rel")).toContain("noopener");
    expect(screen.getByText("items.codeConflicts.kind.Barcode: 7861234567898")).toBeTruthy();
    expect(screen.getByText(/Arroz 1 kg · items.codeConflicts.rows:2/)).toBeTruthy();
  });

  it("reports batch duplicates without a link", async () => {
    mocks.conflicts.mockResolvedValue([{ kind: "Sku", code: "SKU-1", itemId: null, itemName: null, rows: ["1", "3"] }]);
    renderConflicts([
      { kind: "Sku", code: "SKU-1", row: "1", targetKey: "a" },
      { kind: "Sku", code: "sku-1", row: "3", targetKey: "b" },
    ]);
    expect(await screen.findByText(/items.codeConflicts.batch:1, 3/)).toBeTruthy();
    expect(screen.queryByRole("link")).toBeNull();
  });

  it("skips incomplete candidates and users without item view permission", async () => {
    renderConflicts([{ kind: "SupplierCode", code: "07", row: "1", targetKey: "a" }, { kind: "Barcode", code: "  ", row: "1", targetKey: "b" }]);
    mocks.canView = false;
    cleanup();
    renderConflicts([{ kind: "Sku", code: "SKU-1", row: "1", targetKey: "a" }]);
    await new Promise((resolve) => setTimeout(resolve, 400));
    expect(mocks.conflicts).not.toHaveBeenCalled();
  });

  it("sends only complete candidates to the official Items contract", async () => {
    mocks.conflicts.mockResolvedValue([]);
    renderConflicts([
      { kind: "SupplierCode", code: "07", row: "1", targetKey: "a", supplierId: "sup-1" },
      { kind: "SupplierCode", code: "08", row: "2", targetKey: "b" },
    ]);
    await waitFor(() => expect(mocks.conflicts).toHaveBeenCalledOnce());
    expect(mocks.conflicts.mock.calls[0][0]).toEqual([{ kind: "SupplierCode", code: "07", row: "1", targetKey: "a", supplierId: "sup-1" }]);
  });
});
