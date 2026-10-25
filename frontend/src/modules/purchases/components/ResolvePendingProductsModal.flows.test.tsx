// @vitest-environment jsdom
// Caracterización de los flujos de «Resolver productos pendientes»: consultas por nombre de campo
// (independientes del layout) para fijar el mismo comportamiento antes y después del rediseño.
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { ResolvePendingProductsModal } from "./ResolvePendingProductsModal";
import type { PendingProductSource } from "../utils/pendingProductsResolution";

const mocks = vi.hoisted(() => ({ resolve: vi.fn(), match: vi.fn(), getById: vi.fn() }));
vi.mock("../api/purchaseReceptionService", () => ({ purchaseReceptionService: { resolveLines: mocks.resolve, getLineMatch: mocks.match } }));
vi.mock("../../items/facades/itemLookupFacade", () => ({ itemLookupFacade: { getById: mocks.getById, refreshCodeConflicts: vi.fn() } }));
vi.mock("../../../components/items/useItemCodeConflicts", () => ({ useItemCodeConflicts: () => ({ conflicts: [], failed: false, verified: true, pending: false }) }));
vi.mock("./ProductPicker", () => ({ ProductPicker: ({ onSelect }: { onSelect: (p: { id: string; label: string }) => void }) =>
  <button type="button" onClick={() => onSelect({ id: "item-1", label: "SKU1 — Producto existente" })}>pick-product</button> }));
vi.mock("../../../i18n/i18n", () => ({ useI18n: () => ({ t: (key: string, fallback?: unknown) => typeof fallback === "string" ? fallback : key }) }));
vi.mock("../../../components/items/ItemEditorModal/useItemCreationCatalogs", () => ({ useItemCreationCatalogs: () => ({
  barcodeTypeOptions: [{ code: "EAN13", name: "EAN-13" }], itemTypeOptions: [{ id: "type", name: "Type" }],
  brandOptions: [{ id: "brand", name: "Brand" }], categoryOptions: [{ id: "category", name: "Category" }],
  uomOptions: [{ code: "19", name: "Unidad" }, { code: "02", name: "Caja" }], vatRateOptions: VAT_RATES,
  vatRateByCode: new Map(VAT_RATES.map((v) => [v.code, v])), iceRateOptions: [],
}) }));
const VAT_RATES = vi.hoisted(() => [{ code: "0", name: "IVA 0", percentage: 0 }, { code: "4", name: "IVA 15", percentage: 15 }]);

const src = (i: number, extra: Partial<PendingProductSource> = {}): PendingProductSource => ({ rowId: `r${i}`, lineKey: i, supplierCode: `C${i}`,
  supplierAuxCode: "", xmlDescription: `Producto XML ${i}`, quantity: 2, unitPrice: 3, discountPct: 0, vatCode: "0", iceCode: "", matchStatus: "PENDING", ...extra });
const props = (sources: PendingProductSource[]) => ({ open: true, sources, supplierName: "Proveedor", onClose: vi.fn(), onResolved: vi.fn() });
const field = (i: number, name: string) => document.querySelector(`[name="rows.${i}.${name}"]`) as HTMLInputElement;
const change = (i: number, name: string, value: string) => fireEvent.change(field(i, name), { target: { value } });
const fillCreate = (i: number) => {
  for (const [name, value] of Object.entries({ itemTypeId: "type", brandId: "brand", categoryNodeId: "category", defaultUomCode: "19", salePriceGross: "5" })) change(i, name, value);
};
const apply = () => fireEvent.click(screen.getByRole("button", { name: "purchases.pendingProducts.apply" }));
const applied = { applied: true, errors: [], lines: [], itemsCreated: 1, linesLinked: 1, equivalencesLearned: 0, linesAutoMatched: 0 };
const levels = [
  { id: "lvl-unit", name: "Unidad", baseQuantity: 1, isBaseUnit: true, isPurchaseDefault: false, isActive: true },
  { id: "lvl-box", name: "Caja x12", baseQuantity: 12, isBaseUnit: false, isPurchaseDefault: true, isActive: true },
];

beforeEach(() => {
  mocks.resolve.mockReset().mockResolvedValue(applied);
  mocks.match.mockReset();
  mocks.getById.mockReset().mockResolvedValue({ packagingLevels: levels });
});
afterEach(cleanup);

describe("pending products flows (characterization)", () => {
  it("links an existing product with its purchase presentation", async () => {
    const p = props([src(0)]);
    render(<ResolvePendingProductsModal {...p} />);
    change(0, "mode", "link");
    fireEvent.click(await screen.findByText("pick-product"));
    await waitFor(() => expect(field(0, "linkPackagingLevelId").querySelector('option[value="lvl-box"]')).toBeTruthy());
    expect(screen.getAllByText("SKU1 — Producto existente").length).toBeGreaterThan(0);
    apply();
    await waitFor(() => expect(p.onResolved).toHaveBeenCalledOnce());
    expect(mocks.resolve.mock.calls[0][0]).toEqual({ newItems: [], lines: [{ purchaseReceptionLineId: "r0", itemId: "item-1", packagingLevelId: "lvl-box" }] });
  });

  it("proposes the matching engine's best suggestion as a link", async () => {
    mocks.match.mockResolvedValue({ lineId: "r0", suggestions: [{ itemId: "item-9", sku: "S9", shortName: "Sugerido" }] });
    render(<ResolvePendingProductsModal {...props([src(0, { matchStatus: "NEEDS_REVIEW" })])} />);
    await waitFor(() => expect(field(0, "mode").value).toBe("link"));
    expect(screen.getAllByText("S9 — Sugerido").length).toBeGreaterThan(0);
    await waitFor(() => expect(mocks.getById).toHaveBeenCalledWith("item-9"));
  });

  it("groups a second XML code as another presentation of a new product", async () => {
    const p = props([src(0), src(1)]);
    render(<ResolvePendingProductsModal {...p} />);
    fillCreate(0);
    change(1, "mode", "presentation");
    await waitFor(() => expect(field(1, "parentRowId")).toBeTruthy());
    change(1, "parentRowId", "r0");
    change(1, "presentationFactor", "12");
    await waitFor(() => expect(field(1, "presentationName")).toBeTruthy());
    change(1, "presentationName", "Caja x12");
    change(1, "presentationUomCode", "02");
    apply();
    await waitFor(() => expect(p.onResolved).toHaveBeenCalledOnce());
    const request = mocks.resolve.mock.calls[0][0];
    expect(request.newItems).toHaveLength(1);
    expect(request.lines[1]).toMatchObject({ purchaseReceptionLineId: "r1", newItemKey: "r0", presentationFactor: 12, presentationName: "Caja x12", presentationUomCode: "02" });
  });

  it("applies bulk values only to the selected rows and keeps each row editable", async () => {
    render(<ResolvePendingProductsModal {...props([src(0), src(1)])} />);
    fireEvent.click(screen.getByRole("checkbox", { name: "C1" }));
    const bulk = (label: string, value: string) => fireEvent.change(screen.getAllByLabelText(label)[0], { target: { value } });
    bulk("Tipo", "type");
    bulk("Marca", "brand");
    bulk("Categoría", "category");
    bulk("Unidad base de inventario", "19");
    bulk("Tarifa IVA venta", "4");
    fireEvent.click(screen.getByRole("button", { name: "Aplicar a seleccionados" }));
    await waitFor(() => expect(field(0, "brandId").value).toBe("brand"));
    expect([field(0, "itemTypeId").value, field(0, "categoryNodeId").value, field(0, "defaultUomCode").value, field(0, "saleVatCode").value]).toEqual(["type", "category", "19", "4"]);
    expect([field(1, "brandId").value, field(1, "saleVatCode").value]).toEqual(["", "0"]);
    change(0, "brandId", "");
    expect(field(0, "brandId").value).toBe("");
  });

  it("suggests the final price from a margin over price for the selected rows", async () => {
    render(<ResolvePendingProductsModal {...props([src(0), src(1)])} />);
    fireEvent.click(screen.getByRole("checkbox", { name: "C1" }));
    const margin = screen.getByLabelText("Margen sobre precio (%)");
    fireEvent.change(margin, { target: { value: "20" } });
    fireEvent.blur(margin);
    fireEvent.click(screen.getByRole("button", { name: "Sugerir PVP final" }));
    // Costo 3 por unidad, margen 20 % sobre precio, IVA 0 → 3.75.
    await waitFor(() => expect(Number(field(0, "salePriceGross").value)).toBe(3.75));
    expect(field(1, "salePriceGross").value).toBe("");
  });

  it("shows the existing field errors and does not submit an incomplete selection", async () => {
    render(<ResolvePendingProductsModal {...props([src(0)])} />);
    change(0, "shortName", "");
    apply();
    await waitFor(() => expect(screen.getByText("La marca es obligatoria.")).toBeTruthy());
    for (const message of ["El nombre comercial es obligatorio.", "El tipo de ítem es obligatorio.", "La categoría es obligatoria.", "La unidad base de inventario es obligatoria.", "Ingrese el PVP final (mayor a cero)."])
      expect(screen.getByText(message)).toBeTruthy();
    expect(mocks.resolve).not.toHaveBeenCalled();
  });

  it("resolves a partial selection: unselected rows are neither validated nor sent", async () => {
    const p = props([src(0), src(1), src(2)]);
    render(<ResolvePendingProductsModal {...p} />);
    fireEvent.click(screen.getByRole("checkbox", { name: "C1" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "C2" }));
    fillCreate(0);
    expect(screen.getByRole("button", { name: "purchases.pendingProducts.apply" })).toBeTruthy();
    apply();
    await waitFor(() => expect(p.onResolved).toHaveBeenCalledOnce());
    const request = mocks.resolve.mock.calls[0][0];
    expect(request.lines.map((l: { purchaseReceptionLineId: string }) => l.purchaseReceptionLineId)).toEqual(["r0"]);
    expect(request.newItems[0]).toMatchObject({ key: "r0", sku: "C0", baseSalePrice: 5 });
  });

  it("handles a 23-product batch with absent zero barcodes and sends every completed row", async () => {
    const sources = Array.from({ length: 23 }, (_, i) => src(i, { supplierAuxCode: i % 2 ? " 0 " : "0" }));
    const p = props(sources);
    render(<ResolvePendingProductsModal {...p} />);
    // Lote real: asignación masiva y margen sobre los 23 seleccionados (pocos renders del formulario
    // de 23 filas). Rellenar fila a fila ya lo cubren las pruebas de una o dos filas.
    // Consultas acotadas a la barra masiva: recorrer las 23 filas por etiqueta/rol cuesta ~0,5 s cada vez.
    const bar = within(document.querySelector(".prl-bulk") as HTMLElement);
    const bulk = (label: string, value: string) => fireEvent.change(bar.getByLabelText(label), { target: { value } });
    bulk("Tipo", "type");
    bulk("Marca", "brand");
    bulk("Categoría", "category");
    bulk("Unidad base de inventario", "19");
    fireEvent.click(bar.getByRole("button", { name: "Aplicar a seleccionados" }));
    const margin = bar.getByLabelText("Margen sobre precio (%)");
    fireEvent.change(margin, { target: { value: "20" } });
    fireEvent.blur(margin);
    fireEvent.click(bar.getByRole("button", { name: "Sugerir PVP final" }));
    const rowValues = (name: string) =>
      [...document.querySelectorAll<HTMLInputElement>(`[name^="rows."][name$=".${name}"]`)].map((el) => el.value);
    await waitFor(() => expect(rowValues("salePriceGross")).toEqual(Array(23).fill("3.75")));
    expect(rowValues("barcode")).toEqual(Array(23).fill(""));
    expect(rowValues("brandId")).toEqual(Array(23).fill("brand"));
    apply();
    await waitFor(() => expect(p.onResolved).toHaveBeenCalledOnce());
    const request = mocks.resolve.mock.calls[0][0];
    expect(request.newItems).toHaveLength(23);
    expect(request.lines).toHaveLength(23);
    expect(request.newItems.every((n: { baseSalePrice: number; brandId: string }) => n.baseSalePrice === 3.75 && n.brandId === "brand")).toBe(true);
    expect(request.lines.every((l: { barcode: string | null; barcodeType: string | null }) => l.barcode === null && l.barcodeType === null)).toBe(true);
  });
});
