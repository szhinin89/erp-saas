// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent, within } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { DistributeCostModal } from "./DistributeCostModal";
import type { PurchaseCostDistributionDto, PurchaseInvoiceDto, PurchaseItemContextDto } from "../api/purchaseService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";

/** PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — el modal solo arma la solicitud canónica; no prorratea. */
const line = (id: string, description: string, overrides: Record<string, unknown> = {}) => ({
  id, description, itemId: `item-${id}`, quantity: 2, quantityInBaseUom: 2, taxableBase: 50,
  purchaseReceptionLineId: null, xmlAllocatedSubtotal: null, ...overrides,
});
const lines = [
  line("p1", "Producto Uno"),
  line("p2", "Producto Dos"),
  line("manual", "Sin ítem", { itemId: null }),
  line("tr", "Transporte XML", { purchaseReceptionLineId: "rx-1", taxableBase: 10 }),
];
const formLine = (lineId: string, participatesInInventory: boolean) =>
  ({ _key: 1, lineId, context: { participatesInInventory } as PurchaseItemContextDto }) as PurchaseLineFormValues;
const formLines = [formLine("p1", true), formLine("p2", true), formLine("tr", false)];
const existing: PurchaseCostDistributionDto = {
  id: "d-1", costType: "Freight", sourceLineId: null, amount: 12, revision: 3,
  allocations: [{ lineId: "p1", amount: 6.123456 }, { lineId: "p2", amount: 5.876544 }],
};

function renderModal(distributions: PurchaseCostDistributionDto[] = []) {
  const onSave = vi.fn().mockResolvedValue(true);
  const onRemove = vi.fn().mockResolvedValue(true);
  const invoice = { id: "purchase-1", lines, costDistributions: distributions } as unknown as PurchaseInvoiceDto;
  const utils = render(
    <I18nProvider>
      <DistributeCostModal open busy={false} invoice={invoice} formLines={formLines}
        onCancel={vi.fn()} onSave={onSave} onRemove={onRemove} />
    </I18nProvider>,
  );
  return { ...utils, onSave, onRemove };
}

const editorRows = (container: HTMLElement) =>
  Array.from(container.querySelectorAll("section:last-of-type tbody tr")).map((tr) => tr.querySelector(".pdc-table__desc")?.textContent);

afterEach(() => cleanup());

describe("DistributeCostModal", () => {
  it("lista las distribuciones con las asignaciones del servidor y permite retirarlas", () => {
    const { onRemove } = renderModal([existing]);
    const row = screen.getByText("Costo externo").closest("tr")!;
    expect(within(row).getByText("Producto Uno")).toBeTruthy();
    expect(row.textContent).toContain("6.123456");
    fireEvent.click(within(row).getByText("Retirar"));
    expect(onRemove).toHaveBeenCalledWith(existing);
  });

  it("costo externo: exige importe, solo ofrece ítems y envía una distribución nueva (revisión 0)", () => {
    const { container, onSave } = renderModal();
    fireEvent.click(screen.getByText("Nueva distribución"));
    expect(editorRows(container)).toEqual(["Producto Uno", "Producto Dos", "Transporte XML"]);
    expect((screen.getByText("Aplicar") as HTMLButtonElement).disabled).toBe(true);

    fireEvent.blur(container.querySelector(".pdc-toolbar input") as HTMLInputElement, { target: { value: "20" } });
    fireEvent.click(screen.getByLabelText("Incluir: Producto Uno"));
    fireEvent.click(screen.getByText("Aplicar"));

    const draft = onSave.mock.calls[0][0];
    expect(draft).toMatchObject({ costType: "Freight", expectedRevision: 0, amount: 20, targetLineIds: ["p1"], sourceLineId: null });
    expect(draft.id).toEqual(expect.any(String));
  });

  it("editar reemplaza la misma distribución con su revisión vigente", () => {
    const { onSave } = renderModal([existing]);
    fireEvent.click(screen.getByText("Editar"));
    fireEvent.click(screen.getByLabelText("Incluir: Producto Dos"));
    fireEvent.click(screen.getByText("Aplicar"));
    expect(onSave).toHaveBeenCalledWith({ id: "d-1", costType: "Freight", expectedRevision: 3, amount: 12,
      targetLineIds: ["p1"], sourceLineId: null });
  });

  it("transporte del XML: origen Service de recepción, destinos solo Product y sin importe propio", () => {
    const { container, onSave } = renderModal();
    fireEvent.click(screen.getByText("Nueva distribución"));
    fireEvent.change(screen.getByDisplayValue("Costo externo"), { target: { value: "xml" } });
    expect(editorRows(container)).toEqual(["Producto Uno", "Producto Dos"]);
    fireEvent.click(screen.getByLabelText("Incluir: Producto Uno"));
    fireEvent.click(screen.getByLabelText("Incluir: Producto Dos"));
    fireEvent.click(screen.getByText("Aplicar"));
    expect(onSave.mock.calls[0][0]).toMatchObject({ amount: null, sourceLineId: "tr", targetLineIds: ["p1", "p2"] });
  });
});
