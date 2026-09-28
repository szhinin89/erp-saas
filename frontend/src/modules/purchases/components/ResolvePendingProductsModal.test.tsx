// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ResolvePendingProductsModal } from "./ResolvePendingProductsModal";
import type { PendingProductSource } from "../utils/pendingProductsResolution";

const mocks = vi.hoisted(() => ({ resolve: vi.fn(), barcodeTypes: [] as { code: string; name: string }[] }));
vi.mock("../api/purchaseReceptionService", () => ({ purchaseReceptionService: { resolveLines: mocks.resolve, getLineMatch: vi.fn() } }));
vi.mock("./ProductPicker", () => ({ ProductPicker: () => null }));
vi.mock("../../../i18n/i18n", () => ({ useI18n: () => ({ t: (key: string, fallback?: unknown) => typeof fallback === "string" ? fallback : key }) }));
vi.mock("../../../components/items/ItemEditorModal/useItemCreationCatalogs", () => ({ useItemCreationCatalogs: () => ({
  barcodeTypeOptions: mocks.barcodeTypes, itemTypeOptions: [{ id: "type", name: "Type" }],
  brandOptions: [{ id: "brand", name: "Brand" }], categoryOptions: [{ id: "category", name: "Category" }],
  uomOptions: [{ code: "19", name: "Unidad" }], vatRateOptions: [{ code: "0", name: "IVA 0" }],
}) }));

const source: PendingProductSource = { rowId: "a", lineKey: 1, supplierCode: "A", supplierAuxCode: "", xmlDescription: "Original XML", quantity: 2, unitPrice: 3, discountPct: 0, vatCode: "0", iceCode: "", matchStatus: "PENDING" };
const props = () => ({ open: true, sources: [source], supplierName: "Proveedor", onClose: vi.fn(), onResolved: vi.fn() });
const input = (name: string) => document.querySelector(`[name="rows.0.${name}"]`) as HTMLInputElement;
const change = (name: string, value: string) => fireEvent.change(input(name), { target: { value } });
beforeEach(() => { mocks.resolve.mockReset(); mocks.barcodeTypes = [{ code: "Internal", name: "Interno" }]; });
afterEach(cleanup);

describe("bulk resolution modal", () => {
  it("preserves edited ERP fields when catalogs arrive or sources are rerendered", async () => {
    mocks.barcodeTypes = [];
    const p = props();
    const view = render(<ResolvePendingProductsModal {...p} />);
    change("description", "Descripción ERP editada");
    change("shortName", "Nombre ERP");
    mocks.barcodeTypes = [{ code: "Internal", name: "Interno" }];
    view.rerender(<ResolvePendingProductsModal {...p} sources={[{ ...source }]} />);
    await waitFor(() => expect(input("barcodeType").value).toBe("Internal"));
    expect(input("description").value).toBe("Descripción ERP editada");
    expect(input("shortName").value).toBe("Nombre ERP");
    expect(screen.getByText("Original XML")).toBeTruthy();
  });

  it("keeps the batch and row errors on rejection, then submits corrected data", async () => {
    const p = props();
    mocks.resolve.mockResolvedValueOnce({ applied: false, errors: [{ purchaseReceptionLineId: "a", newItemKey: null, message: "SKU duplicado" }] })
      .mockResolvedValueOnce({ applied: true, errors: [], lines: [], itemsCreated: 1, linesLinked: 1, equivalencesLearned: 1, linesAutoMatched: 0 });
    render(<ResolvePendingProductsModal {...p} />);
    for (const [name, value] of Object.entries({ itemTypeId: "type", brandId: "brand", categoryNodeId: "category", defaultUomCode: "19", salePrice: "5", description: "Descripción ERP" })) change(name, value);
    fireEvent.click(screen.getByRole("button", { name: "purchases.pendingProducts.apply" }));
    await waitFor(() => expect(screen.getByText("SKU duplicado")).toBeTruthy());
    expect(p.onResolved).not.toHaveBeenCalled();
    expect(input("description").value).toBe("Descripción ERP");
    change("sku", "CORREGIDO");
    fireEvent.click(screen.getByRole("button", { name: "purchases.pendingProducts.apply" }));
    await waitFor(() => expect(p.onResolved).toHaveBeenCalledOnce());
    expect(mocks.resolve.mock.calls[1][0].newItems[0]).toMatchObject({ sku: "CORREGIDO", description: "Descripción ERP", baseSalePrice: 5 });
  });

  it("renders 100 pending lines and selects/deselects the whole batch", () => {
    render(<ResolvePendingProductsModal {...props()} sources={Array.from({ length: 100 }, (_, i) => ({ ...source, rowId: `${i}`, lineKey: i, supplierCode: `CODE-${i}` }))} />);
    expect(screen.getAllByRole("row")).toHaveLength(101);
    fireEvent.click(screen.getByRole("checkbox", { name: "Seleccionar todas" }));
    expect((screen.getByRole("button", { name: "purchases.pendingProducts.apply" }) as HTMLButtonElement).disabled).toBe(true);
  }, 15000);
});
