// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ResolvePendingProductsModal } from "./ResolvePendingProductsModal";
import type { PendingProductSource } from "../utils/pendingProductsResolution";

const mocks = vi.hoisted(() => ({ resolve: vi.fn(), barcodeTypes: [] as { code: string; name: string }[] }));
vi.mock("../api/purchaseReceptionService", () => ({ purchaseReceptionService: { resolveLines: mocks.resolve, getLineMatch: vi.fn() } }));
vi.mock("./ProductPicker", () => ({ ProductPicker: () => null }));
vi.mock("../../../i18n/i18n", () => ({ useI18n: () => ({ t: (key: string, fallback?: unknown) => typeof fallback === "string" ? fallback
  : fallback && typeof fallback === "object" && "code" in fallback ? `${key}:${String(fallback.code)}` : key }) }));
vi.mock("../../../components/items/ItemEditorModal/useItemCreationCatalogs", () => ({ useItemCreationCatalogs: () => ({
  barcodeTypeOptions: mocks.barcodeTypes, itemTypeOptions: [{ id: "type", name: "Type" }],
  brandOptions: [{ id: "brand", name: "Brand" }], categoryOptions: [{ id: "category", name: "Category" }],
  uomOptions: [{ code: "19", name: "Unidad" }], vatRateOptions: VAT_RATES,
  vatRateByCode: new Map(VAT_RATES.map((v) => [v.code, v])), iceRateOptions: [{ code: "3011", name: "ICE cigarrillos", percentage: 0 }],
}) }));
const VAT_RATES = vi.hoisted(() => [{ code: "0", name: "IVA 0", percentage: 0 }, { code: "4", name: "IVA 15", percentage: 15 }]);

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
    // The supplier code is not a barcode: nothing is copied and no internal type is implied.
    await waitFor(() => expect(screen.getByText("purchases.pendingProducts.provisionalBarcode:A")).toBeTruthy());
    expect(input("barcode").value).toBe("");
    expect(input("barcodeType").value).toBe("");
    expect(input("description").value).toBe("Descripción ERP editada");
    expect(input("shortName").value).toBe("Nombre ERP");
    expect(screen.getByText("Original XML")).toBeTruthy();
  });

  it("keeps the batch and row errors on rejection, then submits corrected data", async () => {
    const p = props();
    mocks.resolve.mockResolvedValueOnce({ applied: false, errors: [{ purchaseReceptionLineId: "a", newItemKey: null, message: "SKU duplicado" }] })
      .mockResolvedValueOnce({ applied: true, errors: [], lines: [], itemsCreated: 1, linesLinked: 1, equivalencesLearned: 1, linesAutoMatched: 0 });
    render(<ResolvePendingProductsModal {...p} />);
    for (const [name, value] of Object.entries({ itemTypeId: "type", brandId: "brand", categoryNodeId: "category", defaultUomCode: "19", salePriceGross: "5", description: "Descripción ERP" })) change(name, value);
    fireEvent.click(screen.getByRole("button", { name: "purchases.pendingProducts.apply" }));
    await waitFor(() => expect(screen.getByText("SKU duplicado")).toBeTruthy());
    expect(p.onResolved).not.toHaveBeenCalled();
    expect(input("description").value).toBe("Descripción ERP");
    change("sku", "CORREGIDO");
    fireEvent.click(screen.getByRole("button", { name: "purchases.pendingProducts.apply" }));
    await waitFor(() => expect(p.onResolved).toHaveBeenCalledOnce());
    expect(mocks.resolve.mock.calls[1][0].newItems[0]).toMatchObject({ sku: "CORREGIDO", description: "Descripción ERP", baseSalePrice: 5 });
    expect(mocks.resolve.mock.calls[1][0].lines[0]).toMatchObject({ barcode: null, barcodeType: null });
  });

  it("puts a real XML barcode on the box presentation when the line is a box", async () => {
    mocks.barcodeTypes = [{ code: "EAN13", name: "EAN-13" }];
    mocks.resolve.mockResolvedValueOnce({ applied: true, errors: [], lines: [], itemsCreated: 1, linesLinked: 1, equivalencesLearned: 1, linesAutoMatched: 0 });
    const p = props();
    render(<ResolvePendingProductsModal {...p} sources={[{ ...source, supplierAuxCode: "7861234567898" }]} />);
    expect(input("barcode").value).toBe("7861234567898");
    for (const [name, value] of Object.entries({ itemTypeId: "type", brandId: "brand", categoryNodeId: "category", defaultUomCode: "19", salePriceGross: "5", presentationFactor: "12" })) change(name, value);
    await waitFor(() => expect(input("barcodeType")).toBeNull());
    change("presentationName", "Caja x12");
    change("presentationUomCode", "19");
    fireEvent.click(screen.getByRole("button", { name: "purchases.pendingProducts.apply" }));
    await waitFor(() => expect(p.onResolved).toHaveBeenCalledOnce());
    expect(mocks.resolve.mock.calls[0][0].lines[0]).toMatchObject({ presentationFactor: 12, barcode: "7861234567898", barcodeType: null });
    expect(screen.getByText("purchases.pendingProducts.provisionalBarcode:A")).toBeTruthy();
  });

  it("takes the final price with VAT, saves it without VAT and shows XML purchase taxes read-only", async () => {
    mocks.resolve.mockResolvedValueOnce({ applied: true, errors: [], lines: [], itemsCreated: 1, linesLinked: 1, equivalencesLearned: 1, linesAutoMatched: 0 });
    const p = props();
    render(<ResolvePendingProductsModal {...p} sources={[{ ...source, vatCode: "4", iceCode: "3011" }]} />);
    expect(input("purchaseVatCode")).toBeNull();
    expect(screen.getAllByText("IVA 15").some((el) => el.tagName === "DD")).toBe(true);
    expect(screen.getByText("ICE cigarrillos")).toBeTruthy();
    expect(screen.getByText("PVP final con IVA (unidad base)")).toBeTruthy();
    for (const [name, value] of Object.entries({ itemTypeId: "type", brandId: "brand", categoryNodeId: "category", defaultUomCode: "19", salePriceGross: "5.75" })) change(name, value);
    await waitFor(() => expect(screen.getByText("Sin IVA (se guarda)")).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "purchases.pendingProducts.apply" }));
    await waitFor(() => expect(p.onResolved).toHaveBeenCalledOnce());
    expect(mocks.resolve.mock.calls[0][0].newItems[0]).toMatchObject({ purchaseVatCode: "4", saleVatCode: "4", exciseTaxCode: "3011", baseSalePrice: 5 });
  });

  it("renders 100 pending lines and selects/deselects the whole batch", () => {
    render(<ResolvePendingProductsModal {...props()} sources={Array.from({ length: 100 }, (_, i) => ({ ...source, rowId: `${i}`, lineKey: i, supplierCode: `CODE-${i}` }))} />);
    expect(screen.getAllByRole("row")).toHaveLength(101);
    fireEvent.click(screen.getByRole("checkbox", { name: "Seleccionar todas" }));
    expect((screen.getByRole("button", { name: "purchases.pendingProducts.apply" }) as HTMLButtonElement).disabled).toBe(true);
  }, 15000);
});
