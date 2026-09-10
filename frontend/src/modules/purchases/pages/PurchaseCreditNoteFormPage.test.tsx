// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";

const api = vi.hoisted(() => ({ invoice: vi.fn(), summaries: vi.fn(), returnable: vi.fn(), reception: vi.fn(), create: vi.fn() }));
vi.mock("../api/purchaseService", () => ({ purchaseService: { getById: api.invoice, getTaxSummaries: api.summaries } }));
vi.mock("../api/purchaseReturnService", () => ({ purchaseReturnService: { getReturnableLines: api.returnable } }));
vi.mock("../api/purchaseReceptionService", () => ({ purchaseReceptionService: { getXmlView: api.reception } }));
vi.mock("../api/purchaseCreditNoteService", () => ({ purchaseCreditNoteService: { createDraft: api.create } }));
vi.mock("../../../lib/messages", () => ({ message: { success: vi.fn(), error: vi.fn() } }));
import { PurchaseCreditNoteFormPage } from "./PurchaseCreditNoteFormPage";

beforeEach(() => {
  api.invoice.mockResolvedValue({ id: "invoice-1", supplierName: "Proveedor", supplierTaxId: "123",
    invoiceNumber: "001-001-000000001", issueDate: "2026-09-09", grandTotal: 1150,
    lines: [{ id: "line-1", description: "Producto A", itemId: "item-1", quantity: 10, unitPrice: 100,
      discountAmount: 0, taxableBase: 1000, vatAmount: 150, iceAmount: 0, irbpnrAmount: 0,
      taxInclusiveTotal: 1150, uomCode: "UNIT", snapshotWarehouseCode: "B01", taxes: [] }] });
  api.summaries.mockResolvedValue([]);
  api.returnable.mockResolvedValue([{ invoiceDetailId: "line-1", itemId: "item-1", description: "Producto A",
    originalQuantity: 10, returnedQuantity: 7, remainingQuantity: 3, warehouseId: "warehouse-1" }]);
  api.reception.mockResolvedValue({ documentType: "CREDIT_NOTE", documentNumber: "001-001-000000099",
    accessKey: "123", issueDate: "2026-09-09", modificationReason: "Productos dañados", totalAmount: 345 });
  api.create.mockResolvedValue({ id: "note-1", linkedPurchaseReturnId: "return-1" });
});
afterEach(() => { cleanup(); vi.clearAllMocks(); });

function renderPage() {
  render(<I18nProvider><MemoryRouter initialEntries={["/new?invoiceId=invoice-1&receptionDocumentId=reception-1"]}>
    <Routes><Route path="/new" element={<PurchaseCreditNoteFormPage />} />
      <Route path="/purchases/returns/return-1" element={<div>Devolución creada</div>} /></Routes>
  </MemoryRouter></I18nProvider>);
}

it("captura productos antes de guardar y envía solo referencias y cantidades al backend", async () => {
  renderPage();
  fireEvent.click(await screen.findByLabelText("Devolución de productos"));
  expect(screen.getByText("Productos a devolver")).toBeTruthy();
  const input = screen.getByLabelText("Cantidad a devolver: Producto A");
  fireEvent.change(input, { target: { value: "3" } });
  fireEvent.blur(input);
  fireEvent.click(screen.getByRole("button", { name: /Guardar nota de crédito fiscal/i }));
  await waitFor(() => expect(api.create).toHaveBeenCalledOnce());
  expect(api.create.mock.calls[0][0]).toMatchObject({ applicationType: "Return", lines: [], taxSummaryLines: [],
    returnLines: [{ originalInvoiceDetailId: "line-1", quantity: 3 }], receptionDocumentId: "reception-1" });
  expect(await screen.findByText("Devolución creada")).toBeTruthy();
});

it("bloquea un exceso y un importe distinto del XML sin enviar la NC", async () => {
  renderPage();
  fireEvent.click(await screen.findByLabelText("Devolución de productos"));
  const input = screen.getByLabelText("Cantidad a devolver: Producto A");
  for (const value of ["4", "2"]) {
    fireEvent.change(input, { target: { value } });
    fireEvent.blur(input);
    const save = screen.getByRole("button", { name: /Guardar nota de crédito fiscal/i }) as HTMLButtonElement;
    expect(save.disabled).toBe(true);
    fireEvent.click(save);
  }
  expect(api.create).not.toHaveBeenCalled();
});
