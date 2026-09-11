// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";

const api = vi.hoisted(() => ({
  invoice: vi.fn(),
  summaries: vi.fn(),
  returnable: vi.fn(),
  reception: vi.fn(),
  create: vi.fn(),
  list: vi.fn(),
}));
vi.mock("../api/purchaseService", () => ({
  purchaseService: { getById: api.invoice, getTaxSummaries: api.summaries, list: api.list },
}));
vi.mock("../api/purchaseReturnService", () => ({ purchaseReturnService: { getReturnableLines: api.returnable } }));
vi.mock("../api/purchaseReceptionService", () => ({ purchaseReceptionService: { getXmlView: api.reception } }));
vi.mock("../api/purchaseCreditNoteService", () => ({ purchaseCreditNoteService: { createDraft: api.create } }));
vi.mock("../../../lib/messages", () => ({ message: { success: vi.fn(), error: vi.fn() } }));
vi.mock("../../masterData/api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn(),
    searchBusinessPartners: vi.fn().mockResolvedValue([
      {
        id: "supplier-1",
        identificationNumber: "1791415132001",
        tradeName: "",
        legalName: "Proveedor Uno S.A.",
        isActive: true,
      },
    ]),
  },
  RoleTypeEnum: { Supplier: "Supplier" },
}));
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
  api.list.mockResolvedValue({
    items: [{ id: "invoice-1", invoiceNumber: "001-001-000000001", issueDate: "2026-09-09",
      supplierId: "supplier-1", status: "Confirmed", lineCount: 1, createdAt: "2026-09-09" }],
    total: 1, page: 1, pageSize: 10,
  });
});
afterEach(() => { cleanup(); vi.clearAllMocks(); });

function renderPage() {
  render(<I18nProvider><MemoryRouter initialEntries={["/new?invoiceId=invoice-1&receptionDocumentId=reception-1"]}>
    <Routes><Route path="/new" element={<PurchaseCreditNoteFormPage />} />
      <Route path="/purchases/returns/return-1" element={<div>Devolución creada</div>} />
      <Route path="/purchases/credit-notes" element={<div>Listado NC</div>} /></Routes>
  </MemoryRouter></I18nProvider>);
}

function renderManualPage() {
  render(<I18nProvider><MemoryRouter initialEntries={["/new"]}>
    <Routes><Route path="/new" element={<PurchaseCreditNoteFormPage />} />
      <Route path="/purchases/returns/return-1" element={<div>Devolución creada</div>} />
      <Route path="/purchases/credit-notes" element={<div>Listado NC</div>} /></Routes>
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

// PURCHASE-CREDIT-NOTE-ENTRY-SCREEN-DUAL-MODE-01 — misma ruta/pantalla, sin `?invoiceId=`
// (abierta desde "Compras → Notas de Crédito de Compra → Nueva"): modo manual.
describe("PurchaseCreditNoteFormPage — modo manual (sin parámetros)", () => {
  it("abre vacío: no pide factura por URL, no muestra error, muestra el selector de proveedor", async () => {
    renderManualPage();

    await screen.findByText("Seleccione el proveedor y la factura de compra afectada");
    expect(screen.queryByText("No se pudo iniciar la nota de crédito")).toBeNull();
    expect(screen.queryByText("Falta la factura afectada.")).toBeNull();
    expect(api.invoice).not.toHaveBeenCalled();
  });

  it("permite seleccionar proveedor y luego una factura Confirmed de ese proveedor", async () => {
    renderManualPage();

    const supplierInput = await screen.findByPlaceholderText(
      "Buscar por RUC, razón social o nombre...",
    );
    fireEvent.focus(supplierInput);
    fireEvent.change(supplierInput, { target: { value: "Proveedor Uno" } });
    fireEvent.click(await screen.findByText("Proveedor Uno S.A."));

    const invoiceInput = await screen.findByPlaceholderText(
      "Buscar factura confirmada por número...",
    );
    fireEvent.focus(invoiceInput);
    fireEvent.change(invoiceInput, { target: { value: "001" } });

    await waitFor(() =>
      expect(api.list).toHaveBeenCalledWith("001", "Confirmed", 1, 10, "supplier-1"),
    );
    fireEvent.click(await screen.findByText("001-001-000000001"));

    await screen.findByText("Tipo de nota de crédito");
    expect(api.invoice).toHaveBeenCalledWith("invoice-1");
  });

  it("modo manual carga las líneas retornables de la factura elegida", async () => {
    renderManualPage();

    fireEvent.focus(
      await screen.findByPlaceholderText("Buscar por RUC, razón social o nombre..."),
    );
    fireEvent.change(
      screen.getByPlaceholderText("Buscar por RUC, razón social o nombre..."),
      { target: { value: "Proveedor Uno" } },
    );
    fireEvent.click(await screen.findByText("Proveedor Uno S.A."));
    fireEvent.focus(await screen.findByPlaceholderText("Buscar factura confirmada por número..."));
    fireEvent.click(await screen.findByText("001-001-000000001"));
    await screen.findByText("Tipo de nota de crédito");

    fireEvent.click(await screen.findByLabelText("Devolución de productos"));
    expect(await screen.findByText("Productos a devolver")).toBeTruthy();
    expect(screen.getByLabelText("Cantidad a devolver: Producto A")).toBeTruthy();
    expect(api.returnable).toHaveBeenCalledWith("invoice-1");
  });

  it("modo manual valida cantidades y guarda la NC usando el mismo flujo que el modo XML", async () => {
    renderManualPage();

    fireEvent.focus(
      await screen.findByPlaceholderText("Buscar por RUC, razón social o nombre..."),
    );
    fireEvent.change(
      screen.getByPlaceholderText("Buscar por RUC, razón social o nombre..."),
      { target: { value: "Proveedor Uno" } },
    );
    fireEvent.click(await screen.findByText("Proveedor Uno S.A."));
    fireEvent.focus(await screen.findByPlaceholderText("Buscar factura confirmada por número..."));
    fireEvent.click(await screen.findByText("001-001-000000001"));
    await screen.findByText("Tipo de nota de crédito");
    fireEvent.click(await screen.findByLabelText("Devolución de productos"));

    // Modo manual: nadie precargó N.º NC/fecha/motivo (no hay XML) — el usuario los ingresa.
    // Los campos requeridos muestran un "*" agregado a la etiqueta (ZHField), de ahí el match parcial.
    fireEvent.change(screen.getByLabelText(/Número NC/), { target: { value: "001-001-000000050" } });
    fireEvent.change(screen.getByLabelText(/Fecha emisión/), { target: { value: "2026-09-10" } });
    fireEvent.change(screen.getByLabelText(/Motivo/), { target: { value: "Producto dañado" } });

    const input = screen.getByLabelText("Cantidad a devolver: Producto A");

    // Excede lo disponible (remainingQuantity=3) — mismo guard que el modo XML.
    fireEvent.change(input, { target: { value: "5" } });
    fireEvent.blur(input);
    expect(
      (screen.getByRole("button", { name: /Guardar nota de crédito fiscal/i }) as HTMLButtonElement)
        .disabled,
    ).toBe(true);

    fireEvent.change(input, { target: { value: "3" } });
    fireEvent.blur(input);
    fireEvent.click(screen.getByRole("button", { name: /Guardar nota de crédito fiscal/i }));

    await waitFor(() => expect(api.create).toHaveBeenCalledOnce());
    expect(api.create.mock.calls[0][0]).toMatchObject({
      purchaseInvoiceId: "invoice-1",
      receptionDocumentId: null,
      applicationType: "Return",
      creditNoteNumber: "001-001-000000050",
      returnLines: [{ originalInvoiceDetailId: "line-1", quantity: 3 }],
    });
    expect(await screen.findByText("Devolución creada")).toBeTruthy();
  });
});

// PURCHASE-CREDIT-NOTE-FULL-UX-FLOW-01 — "Volver" desde /purchases/credit-notes/new (modo XML o
// modo manual) debe regresar al listado de NC, nunca a Facturas de compra (/purchases).
describe("PurchaseCreditNoteFormPage — botón Volver (PURCHASE-CREDIT-NOTE-FULL-UX-FLOW-01)", () => {
  it("modo XML: Volver desde el formulario fiscal vuelve a /purchases/credit-notes", async () => {
    renderPage();
    fireEvent.click(await screen.findByLabelText("Devolución de productos"));

    fireEvent.click(screen.getByRole("button", { name: "Volver" }));

    expect(await screen.findByText("Listado NC")).toBeTruthy();
  });

  it("modo manual: Volver desde el selector de proveedor/factura vuelve a /purchases/credit-notes", async () => {
    renderManualPage();
    await screen.findByText("Seleccione el proveedor y la factura de compra afectada");

    fireEvent.click(screen.getByRole("button", { name: "Volver" }));

    expect(await screen.findByText("Listado NC")).toBeTruthy();
  });

  it("modo manual: Volver desde el formulario fiscal (tras elegir factura) vuelve a /purchases/credit-notes", async () => {
    renderManualPage();

    fireEvent.focus(
      await screen.findByPlaceholderText("Buscar por RUC, razón social o nombre..."),
    );
    fireEvent.change(
      screen.getByPlaceholderText("Buscar por RUC, razón social o nombre..."),
      { target: { value: "Proveedor Uno" } },
    );
    fireEvent.click(await screen.findByText("Proveedor Uno S.A."));
    fireEvent.focus(await screen.findByPlaceholderText("Buscar factura confirmada por número..."));
    fireEvent.click(await screen.findByText("001-001-000000001"));
    await screen.findByText("Tipo de nota de crédito");
    fireEvent.click(await screen.findByLabelText("Devolución de productos"));

    fireEvent.click(screen.getByRole("button", { name: "Volver" }));

    expect(await screen.findByText("Listado NC")).toBeTruthy();
  });
});
