// @vitest-environment jsdom
// Diseño compacto: filas resumidas + detalle expandible, estado por fila y contadores del pie.
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { ResolvePendingProductsModal } from "./ResolvePendingProductsModal";
import type { PendingProductSource } from "../utils/pendingProductsResolution";

const mocks = vi.hoisted(() => ({ resolve: vi.fn() }));
vi.mock("../api/purchaseReceptionService", () => ({ purchaseReceptionService: { resolveLines: mocks.resolve, getLineMatch: vi.fn() } }));
vi.mock("../../../components/items/useItemCodeConflicts", () => ({ useItemCodeConflicts: () => ({ conflicts: [], failed: false, verified: true, pending: false }) }));
vi.mock("./ProductPicker", () => ({ ProductPicker: () => null }));
vi.mock("../../../i18n/i18n", () => ({ useI18n: () => ({ t: (key: string, fallback?: unknown) => typeof fallback === "string" ? fallback
  : fallback && typeof fallback === "object" && "count" in fallback ? `${key}:${String(fallback.count)}` : key }) }));
vi.mock("../../../components/items/ItemEditorModal/useItemCreationCatalogs", () => ({ useItemCreationCatalogs: () => ({
  barcodeTypeOptions: [], itemTypeOptions: [{ id: "type", name: "Type" }], brandOptions: [{ id: "brand", name: "Brand" }],
  categoryOptions: [{ id: "category", name: "Category" }], uomOptions: [{ code: "19", name: "Unidad" }], vatRateOptions: VAT_RATES,
  vatRateByCode: new Map(VAT_RATES.map((v) => [v.code, v])), iceRateOptions: [],
}) }));
const VAT_RATES = vi.hoisted(() => [{ code: "0", name: "IVA 0", percentage: 0 }]);

const src = (i: number): PendingProductSource => ({ rowId: `r${i}`, lineKey: i, supplierCode: `C${i}`, supplierAuxCode: "",
  xmlDescription: `Producto XML ${i}`, quantity: 2, unitPrice: 3, discountPct: 0, vatCode: "0", iceCode: "", matchStatus: "PENDING" });
const props = (n: number) => ({ open: true, sources: Array.from({ length: n }, (_, i) => src(i)), supplierName: "Proveedor", onClose: vi.fn(), onResolved: vi.fn() });
const field = (i: number, name: string) => document.querySelector(`[name="rows.${i}.${name}"]`) as HTMLInputElement;
const change = (i: number, name: string, value: string) => fireEvent.change(field(i, name), { target: { value } });
const fillCreate = (i: number) => {
  for (const [name, value] of Object.entries({ itemTypeId: "type", brandId: "brand", categoryNodeId: "category", defaultUomCode: "19", salePriceGross: "5" })) change(i, name, value);
};
const isShown = (el: Element) => el.closest("[hidden]") === null;
const toggle = (code: string) => screen.getByRole("button", { name: `Detalles · ${code}` });
const tagOf = (code: string) => toggle(code).closest("[role=rowgroup]")!.querySelector<HTMLElement>(":scope > .zh-line-issues");
const footer = () => document.querySelector(".zh-modal-footer")!.textContent;

beforeEach(() => {
  mocks.resolve.mockReset();
  // jsdom no implementa CSS.escape (ZHLineIssuesTag lo usa al abrir por teclado), igual que su test.
  vi.stubGlobal("CSS", { escape: (value: string) => value });
});
afterEach(cleanup);

describe("compact pending products rows", () => {
  it("starts collapsed for a batch and expanded for a single line", () => {
    render(<ResolvePendingProductsModal {...props(3)} />);
    expect(isShown(field(0, "brandId"))).toBe(false);
    expect(toggle("C0").getAttribute("aria-expanded")).toBe("false");
    expect(document.getElementById(toggle("C0").getAttribute("aria-controls")!)?.contains(field(0, "brandId"))).toBe(true);
    cleanup();
    render(<ResolvePendingProductsModal {...props(1)} />);
    expect(isShown(field(0, "brandId"))).toBe(true);
    expect(toggle("C0").getAttribute("aria-expanded")).toBe("true");
  });

  it("keeps typed data when a row is collapsed and expanded again", () => {
    render(<ResolvePendingProductsModal {...props(2)} />);
    fireEvent.click(toggle("C1"));
    expect(isShown(field(1, "shortName"))).toBe(true);
    change(1, "shortName", "Nombre editado");
    change(1, "sku", "SKU-EDIT");
    change(1, "brandId", "brand");
    fireEvent.click(toggle("C1"));
    expect(isShown(field(1, "shortName"))).toBe(false);
    fireEvent.click(toggle("C1"));
    expect([field(1, "shortName").value, field(1, "sku").value, field(1, "brandId").value]).toEqual(["Nombre editado", "SKU-EDIT", "brand"]);
    // El resumen de la fila compacta refleja lo editado.
    expect(screen.getAllByText("Nombre editado").length).toBeGreaterThan(0);
  });

  it("expands and collapses every row from the header", () => {
    render(<ResolvePendingProductsModal {...props(3)} />);
    fireEvent.click(screen.getByRole("button", { name: "Expandir todo" }));
    expect([0, 1, 2].every((i) => isShown(field(i, "brandId")))).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Contraer todo" }));
    expect([0, 1, 2].some((i) => isShown(field(i, "brandId")))).toBe(false);
  });

  it("derives selected / ready / pending from the existing validations", () => {
    render(<ResolvePendingProductsModal {...props(3)} />);
    expect(footer()).toContain("purchases.pendingProducts.footer.selected:3");
    expect(footer()).toContain("purchases.pendingProducts.footer.ready:0");
    expect(footer()).toContain("purchases.pendingProducts.footer.pending:3");
    fillCreate(0);
    expect(footer()).toContain("purchases.pendingProducts.footer.ready:1");
    expect(footer()).toContain("purchases.pendingProducts.footer.pending:2");
    fireEvent.click(screen.getByRole("checkbox", { name: "C2" }));
    expect(footer()).toContain("purchases.pendingProducts.footer.selected:2");
    expect(footer()).toContain("purchases.pendingProducts.footer.pending:1");
    // Etiqueta de incidencias solo en filas seleccionadas con datos por completar.
    expect(["C0", "C1", "C2"].map((code) => !!tagOf(code))).toEqual([false, true, false]);
  });

  it("names what is missing in a collapsed row and opens it to complete the data", async () => {
    render(<ResolvePendingProductsModal {...props(2)} />);
    const tag = within(tagOf("C1")!).getByRole("button");
    expect(tag.textContent).toBe("Advertencia: El tipo de ítem es obligatorio. · purchases.lines.issuesMore:4");
    fireEvent.click(tag);
    for (const message of ["El tipo de ítem es obligatorio.", "La marca es obligatoria.", "La categoría es obligatoria.", "La unidad base de inventario es obligatoria.", "Ingrese el PVP final (mayor a cero)."])
      expect(await screen.findByText(message)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Completar datos" }));
    expect(isShown(field(1, "brandId"))).toBe(true);
  });

  it("expands every row with field errors on submit and focuses the first one", async () => {
    render(<ResolvePendingProductsModal {...props(3)} />);
    fillCreate(0);
    fillCreate(2);
    change(2, "brandId", "");
    fireEvent.click(screen.getByRole("button", { name: "purchases.pendingProducts.apply:3" }));
    await waitFor(() => expect(screen.getAllByText("La marca es obligatoria.").length).toBe(2));
    expect(isShown(field(0, "brandId"))).toBe(false);
    expect(isShown(field(1, "brandId")) && isShown(field(2, "brandId"))).toBe(true);
    expect(screen.getAllByText("La marca es obligatoria.").every(isShown)).toBe(true);
    expect(document.activeElement?.getAttribute("name")).toMatch(/^rows\.1\./);
    expect(mocks.resolve).not.toHaveBeenCalled();
  });

  it("expands a row rejected by the server and counts it as pending", async () => {
    mocks.resolve.mockResolvedValueOnce({ applied: false, errors: [{ purchaseReceptionLineId: "r1", newItemKey: null, message: "SKU duplicado" }] });
    render(<ResolvePendingProductsModal {...props(2)} />);
    fillCreate(0);
    fillCreate(1);
    expect(footer()).toContain("purchases.pendingProducts.footer.pending:0");
    fireEvent.click(screen.getByRole("button", { name: "purchases.pendingProducts.apply:2" }));
    await waitFor(() => expect(screen.getByRole("button", { name: /SKU duplicado/ })).toBeTruthy());
    expect(isShown(field(1, "sku"))).toBe(true);
    expect(isShown(field(0, "sku"))).toBe(false);
    expect(footer()).toContain("purchases.pendingProducts.footer.pending:1");
    expect(toggle("C1").closest("[role=row]")!.classList.contains("prl-row--error")).toBe(true);
  });

  it("opens the row when its action changes so the new required field is visible", () => {
    render(<ResolvePendingProductsModal {...props(2)} />);
    fireEvent.change(screen.getByRole("combobox", { name: "Acción · C1" }), { target: { value: "presentation" } });
    expect(isShown(field(1, "parentRowId"))).toBe(true);
    expect(within(field(1, "parentRowId")).getByRole("option", { name: "Producto XML 0" })).toBeTruthy();
  });
});
