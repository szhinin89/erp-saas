// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import { replaceBreakdownRows } from "../utils/purchaseXmlBreakdown";
import { balanced, component, promo, xml } from "../utils/purchaseXmlBreakdown.fixture";
import { PurchaseLineBreakdownModal } from "./PurchaseLineBreakdownModal";

const mocks = vi.hoisted(() => ({ getItemContext: vi.fn(), list: vi.fn(), getById: vi.fn() }));
vi.mock("../api/purchaseService", () => ({ purchaseService: mocks }));
vi.mock("./ProductPicker", () => ({
  ProductPicker: ({ onSelect }: { onSelect: (p: { id: string; label: string }) => void }) =>
    <button type="button" onClick={() => onSelect({ id: "item-c", label: "Producto C" })}>pick</button>,
}));
vi.mock("../../../i18n/i18n", () => ({ useI18n: () => ({ t: (key: string, fallback?: unknown) => typeof fallback === "string" ? fallback : key }) }));

const context = {
  itemId: "item-c", baseUomCode: "UNIT", participatesInInventory: true,
  packagingLevels: [{ id: "box", name: "Caja x12", baseQuantity: 12, uomCode: "BOX", isBaseUnit: false, isPurchaseDefault: true }],
};

function renderModal(lines = replaceBreakdownRows([promo()], xml.purchaseReceptionLineId, balanced(), 10, 6)) {
  const onApply = vi.fn().mockReturnValue(true);
  const onCancel = vi.fn();
  render(<PurchaseLineBreakdownModal source={lines[0]} lines={lines} supplierId="sup-1" warehouseId="wh-1"
    onCancel={onCancel} onApply={onApply} />);
  return { onApply, onCancel };
}
const applyButton = () => screen.getByRole("button", { name: "Aplicar desglose" });

beforeEach(() => {
  setPrecisionPolicyForTests(TEST_PRECISION_POLICY);
  Object.values(mocks).forEach(m => m.mockReset());
});
afterEach(cleanup);

describe("PurchaseLineBreakdownModal", () => {
  it("titles the split with the XML code, a short subtitle and keeps the original description in the summary", () => {
    renderModal();
    expect(screen.getByRole("heading", { name: "Desglosar productos · 13563" })).toBeTruthy();
    expect(screen.getByText("Reemplace esta línea XML por los productos reales recibidos. Los importes deben cuadrar.")).toBeTruthy();
    const summary = screen.getByLabelText("Original");
    expect(summary.textContent).toMatch(/COMBO PROMO.*Subtotal bruto.*Descuento.*Impuestos.*Total/);
    expect(document.body.textContent).not.toMatch(/IVA 4(?!\d)|ICE 3011/);
  });

  it("shows compact rows with presentation, quantity and cost; taxes only on demand", () => {
    renderModal();
    expect(screen.getAllByRole("columnheader").map(h => h.textContent)).toEqual(["Producto", "Presentación", "Cantidad", "Costo", "Impuestos", ""]);
    expect(screen.getByLabelText("Presentación · item-a")).toBeTruthy();
    expect(screen.getByLabelText("Cantidad · item-a")).toBeTruthy();
    expect(screen.getByLabelText("Costo · item-a")).toBeTruthy();
    expect(screen.queryByLabelText("IVA 15% · Importe · item-a")).toBeNull();
    const toggle = screen.getByRole("button", { name: "Impuestos y bases · item-a" });
    expect(toggle.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(toggle);
    expect(toggle.getAttribute("aria-expanded")).toBe("true");
    expect(screen.getByLabelText("IVA 15% · Importe · item-a")).toBeTruthy();
    expect(screen.getByLabelText("Descuento · item-a")).toBeTruthy();
  });

  it("reopens saved components balanced and applies them from the single footer", async () => {
    const { onApply, onCancel } = renderModal();
    expect(screen.queryByRole("table", { name: "Diferencia" })).toBeNull();
    expect(screen.getByRole("button", { name: "Conciliación" }).getAttribute("aria-expanded")).toBe("false");
    expect((applyButton() as HTMLButtonElement).disabled).toBe(false);
    fireEvent.click(applyButton());
    await waitFor(() => expect(onApply).toHaveBeenCalledTimes(1));
    expect(onApply.mock.calls[0][0].map((l: { itemId: string }) => l.itemId)).toEqual(["item-a", "item-b"]);
    expect(onCancel).toHaveBeenCalled();
  });

  it("blocks applying on a tax difference and shows the reconciliation detail on demand", () => {
    renderModal();
    fireEvent.click(screen.getByRole("button", { name: "Impuestos y bases · item-b" }));
    const vat = screen.getByLabelText("IVA 15% · Importe · item-b") as HTMLInputElement;
    fireEvent.focus(vat);
    fireEvent.change(vat, { target: { value: "0.51" } });
    fireEvent.blur(vat);
    expect((applyButton() as HTMLButtonElement).disabled).toBe(true);
    expect(applyButton().getAttribute("title")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Ver motivo: Aplicar desglose" }));
    // P2: the count lives only in the reconciliation control; the reason names the step once.
    expect(screen.getByText("Revise la conciliación: cada concepto e impuesto debe cuadrar.")).toBeTruthy();
    expect(screen.queryByText(/Conceptos con diferencias/)).toBeNull();
    const detail = screen.getByRole("button", { name: "Conciliación (2)" });
    expect(document.getElementById("pbd-reconciliation")).toBeNull();
    expect(detail.className).toContain("zh-btn--disclosure");
    // ZH-DS-BUTTON-SIZING-01: el pie usa un solo tamaño (estándar) para todas sus acciones.
    const footerButtons = Array.from(detail.closest(".zh-modal-footer")!.querySelectorAll("button"));
    expect(footerButtons.map(b => b.textContent)).toEqual(["compare_arrowsConciliación (2)expand_more", "Cancelar", "Aplicar desglose", "Ver motivoexpand_more"]);
    footerButtons.forEach(b => expect(b.className).not.toMatch(/zh-btn--(sm|xs|md)\b/));
    fireEvent.click(detail);
    expect(detail.getAttribute("aria-expanded")).toBe("true");
    expect(document.getElementById("pbd-reconciliation")!.textContent).toContain("IVA 15% · Importe");
  });

  it("keeps the product search on top and adds products with their purchase packaging", async () => {
    mocks.getItemContext.mockResolvedValue(context);
    renderModal([promo()]);
    expect(screen.getByRole("search", { name: "Agregar producto" }).textContent).toContain("pick");
    fireEvent.click(screen.getByRole("button", { name: "Ver motivo: Aplicar desglose" }));
    expect(screen.getByText(/Agregue al menos dos productos\./)).toBeTruthy();
    fireEvent.click(screen.getByText("pick"));
    await waitFor(() => expect(screen.getByLabelText("Presentación · Producto C")).toBeTruthy());
    expect(mocks.getItemContext).toHaveBeenCalledWith("item-c", "wh-1", "sup-1");
    expect((screen.getByLabelText("Presentación · Producto C") as HTMLSelectElement).value).toBe("box");
    expect((screen.getByLabelText("Costo · Producto C") as HTMLInputElement).value).toMatch(/^0(\.0+)?$/);
  });
});

it("P0 - keeps operation failures inside the split modal", async () => {
  mocks.getItemContext.mockRejectedValue(new Error("network"));
  renderModal([promo()]);
  fireEvent.click(screen.getByText("pick"));
  await waitFor(() => expect(screen.getByRole("alert").textContent).toContain("No se pudo cargar"));
  expect(screen.getByRole("dialog").contains(screen.getByRole("alert"))).toBe(true);
});
it("P0 - links invalid quantities and amounts to their fields", () => {
  const lines = replaceBreakdownRows([promo()], xml.purchaseReceptionLineId, balanced(), 10, 6);
  lines[0].quantity = 0;
  lines[0].xmlAllocation!.subtotal = -1;
  renderModal(lines);
  for (const name of ["Cantidad", "Costo"]) {
    const input = screen.getByLabelText(`${name} \u00b7 item-a`);
    expect(input.getAttribute("aria-invalid")).toBe("true");
    expect(document.getElementById(input.getAttribute("aria-describedby")!)?.textContent).toBeTruthy();
  }
  expect((applyButton() as HTMLButtonElement).disabled).toBe(true);
});

it("P2 - a zero total pending is not shown as balanced while a concept or tax differs", () => {
  // Subtotal +0.50 on item-a and VAT -0.50 on item-b: the line total still matches the XML.
  const lines = [
    component("item-a", 2, 8.5, 1.5, [6.5, 0.98], [6.5, 0.26], { _key: 10 }),
    component("item-b", 1, 4, 0.5, [3.5, 0.02], [3.5, 0.14], { _key: 11 }),
  ];
  renderModal(lines);
  const status = document.querySelector(".pbd-footer-status")!;
  expect(status.textContent).toContain("Conceptos sin cuadrar");
  expect(status.textContent).not.toMatch(/Pendiente|Cuadrado/);
  expect(screen.getByRole("button", { name: "Conciliación (2)" })).toBeTruthy();
  expect((applyButton() as HTMLButtonElement).disabled).toBe(true);
});
it("P2 - a balanced split reads as balanced instead of a zero pending amount", () => {
  renderModal();
  const status = document.querySelector(".pbd-footer-status")!;
  expect(status.textContent).toContain("Cuadrado");
  expect(status.textContent).not.toContain("Pendiente");
  expect(screen.queryByRole("button", { name: /Ver motivo/ })).toBeNull();
});
