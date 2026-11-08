// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseReceptionActionsCell } from "./PurchaseReceptionActionsCell";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";

// PURCHASES-RECEPTION-WRITE-PERMISSIONS-01 (H3) y PURCHASES-RECEPTION-SUPPLIER-RULE-01 (H4).
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

const base: PurchaseReceptionItem = {
  documentId: "doc-1", sourceDocType: "INVOICE", documentStatus: "VERIFIED",
  supplierRuc: "1790012345001", supplierName: "Proveedor", invoiceNumber: "001-001-000000001",
  modifiedDocumentNumber: null, accessKey: "key", issueDate: "2026-07-01",
  authorizationDate: "2026-07-01", subtotal: 10, vatAmount: 1.5, total: 11.5,
  supplierExists: true, supplierId: "supplier-1", supplierIsActive: true,
  purchaseId: null, purchaseState: "NONE",
  affectedPurchaseExists: false, affectedPurchaseId: null,
  status: "PENDING", processingStatus: "PROCESSED", processingNotes: null, supplierTradeName: null,
};

function show(
  overrides: Partial<PurchaseReceptionItem>,
  permissions: { canProcess?: boolean; canCreateExpense?: boolean } = {},
) {
  render(
    <I18nProvider>
      <PurchaseReceptionActionsCell row={{ ...base, ...overrides }} xmlState={undefined}
        onDownloadXml={vi.fn()} onViewXml={vi.fn()} onProcessCreditNote={vi.fn()}
        resolvingCreditNoteId={null} {...permissions} />
    </I18nProvider>,
  );
}

const button = (name: string) => screen.queryByRole("button", { name });

describe("Recepción — acciones según permisos (H3)", () => {
  it("sin purchases.create no ofrece Consultar XML", () => {
    show({ documentStatus: "IMPORTED" }, { canProcess: false });
    expect(button("Consultar XML")).toBeNull();
  });

  it("sin purchases.create no ofrece Crear compra pero conserva Ver XML", () => {
    show({}, { canProcess: false });
    expect(button("Crear compra")).toBeNull();
    expect(button("Ver XML")).not.toBeNull();
    expect(button("Crear gasto")).not.toBeNull();
  });

  it("sin expenses.documents.create no ofrece Crear gasto", () => {
    show({}, { canCreateExpense: false });
    expect(button("Crear gasto")).toBeNull();
    expect(button("Crear compra")).not.toBeNull();
  });

  it("sin ningún permiso de escritura no muestra pistas de acciones que no puede realizar", () => {
    show({ supplierExists: false, supplierId: null }, { canProcess: false, canCreateExpense: false });
    expect(screen.queryByText("Cree primero el proveedor")).toBeNull();
  });

  it("sin purchases.create no ofrece Procesar NC", () => {
    show(
      { sourceDocType: "CREDIT_NOTE", affectedPurchaseExists: true, affectedPurchaseId: "inv-1" },
      { canProcess: false },
    );
    expect(button("Procesar NC")).toBeNull();
    expect(button("Ver XML")).not.toBeNull();
  });
});

describe("Recepción — proveedor inactivo (H4)", () => {
  it("una NC vinculada a una compra existente se procesa aunque el proveedor esté inactivo", () => {
    show({
      sourceDocType: "CREDIT_NOTE", supplierIsActive: false,
      affectedPurchaseExists: true, affectedPurchaseId: "inv-1",
    });
    expect(button("Procesar NC")).not.toBeNull();
    expect(screen.getByText("Proveedor inactivo")).toBeTruthy();
  });

  it("una NC sin compra afectada sigue pidiendo la factura primero", () => {
    show({ sourceDocType: "CREDIT_NOTE", supplierIsActive: false });
    expect(button("Procesar NC")).toBeNull();
    expect(screen.getByText("Ingrese primero la factura afectada")).toBeTruthy();
  });

  it("una factura nueva con proveedor inactivo no ofrece crear compra ni gasto", () => {
    show({ supplierIsActive: false });
    expect(button("Crear compra")).toBeNull();
    expect(button("Crear gasto")).toBeNull();
    expect(button("Ver proveedor")).not.toBeNull();
  });
});
