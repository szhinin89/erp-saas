// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useFieldArray, useForm } from "react-hook-form";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseReturnableLinesEditor } from "./PurchaseReturnableLinesEditor";
import type { PurchaseReturnDraftFormValues } from "../schemas/purchaseReturnSchema";
import type { PurchaseLineDto } from "../api/purchaseService";
import { purchaseReturnPreview } from "../utils/purchaseReturnPreview";

const source = { id: "line-1", quantity: 10, unitPrice: 100, discountAmount: 100,
  vatAmount: 148.5, iceAmount: 90, irbpnrAmount: 1, snapshotWarehouseCode: "B01" } as PurchaseLineDto;

function Wrapper() {
  const { control } = useForm<PurchaseReturnDraftFormValues>({ defaultValues: { reason: "", lines: [] } });
  const { fields, append, remove } = useFieldArray({ control, name: "lines" });
  return <I18nProvider><PurchaseReturnableLinesEditor
    returnableLines={[{ invoiceDetailId: "line-1", itemId: "item-1", description: "Producto 1",
      originalQuantity: 10, returnedQuantity: 7, remainingQuantity: 3, warehouseId: "warehouse-1" }]}
    invoiceLines={[source]} selected={fields} append={append} remove={remove} />
    <output data-testid="selected">{JSON.stringify(fields.map(({ originalInvoiceDetailId, quantity }) => ({ originalInvoiceDetailId, quantity })))}</output>
  </I18nProvider>;
}

afterEach(cleanup);
describe("Productos a devolver", () => {
  it("prorratea base neta e impuestos históricos con redondeo por línea", () => {
    expect(purchaseReturnPreview(source, 3)).toEqual({ base: 270, vat: 44.55, ice: 27, irbpnr: .3, total: 341.85 });
  });
  it("muestra disponibles, permite cantidades parciales y actualiza impuestos y total", () => {
    render(<Wrapper />);
    for (const name of ["Cantidad comprada", "Ya devuelto", "Disponible", "Cantidad a devolver", "Precio/costo", "Base", "IVA", "ICE", "IRBPNR", "Total", "Bodega"]) {
      expect(screen.getByRole("columnheader", { name })).toBeTruthy();
    }
    const input = screen.getByLabelText("Cantidad a devolver: Producto 1");
    fireEvent.change(input, { target: { value: "3" } });
    fireEvent.blur(input);
    expect(screen.getByTestId("selected").textContent).toBe('[{"originalInvoiceDetailId":"line-1","quantity":3}]');
    expect(screen.getByText("341.85")).toBeTruthy();
    expect(screen.getByText("B01")).toBeTruthy();
    expect(screen.queryByRole("button", { name: /agregar/i })).toBeNull();
  });
  it("marca exceso y elimina una selección al poner cero", () => {
    render(<Wrapper />);
    const input = screen.getByLabelText("Cantidad a devolver: Producto 1");
    fireEvent.change(input, { target: { value: "4" } });
    fireEvent.blur(input);
    expect(screen.getByRole("alert").textContent).toContain("Excede lo disponible (3)");
    fireEvent.change(input, { target: { value: "0" } });
    fireEvent.blur(input);
    expect(screen.getByTestId("selected").textContent).toBe("[]");
  });
});
