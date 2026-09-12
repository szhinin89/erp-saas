// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseReturnCreditNoteSection } from "./PurchaseReturnCreditNoteSection";
import type { PurchaseReturnDto } from "../api/purchaseReturnService";

afterEach(() => {
  cleanup();
});

const base: PurchaseReturnDto = {
  id: "return-1",
  purchaseInvoiceId: "purchase-1",
  supplierId: "supplier-1",
  branchId: "branch-1",
  returnNumber: "DEV-000001",
  reason: "Producto dañado",
  status: "Authorized",
  fiscalStatus: "SupplierCreditNoteRegistered",
  supplierCreditNoteDocumentId: "doc-guid-123",
  authorizedSubtotal: 100,
  authorizedVatTotal: 15,
  authorizedIceTotal: 0,
  authorizedDiscountTotal: 0,
  authorizedGrandTotal: 115,
  authorizedAtUtc: "2026-08-01T10:00:00Z",
  cancelledAtUtc: null,
  cancellationReason: null,
  lines: [],
  createdAt: "2026-08-01T10:00:00Z",
  updatedAt: null,
  supplierCreditNoteInvoiceNumber: null,
  supplierCreditNoteAccessKey: null,
  supplierCreditNoteIssueDate: null,
  supplierCreditNoteAuthorizationDate: null,
  supplierCreditNoteTotalAmount: null,
  purchaseInvoiceNumber: null,
  linkedPurchaseCreditNoteId: null,
  linkedPurchaseCreditNoteStatus: null,
};

function show(overrides: Partial<PurchaseReturnDto> = {}) {
  render(
    <MemoryRouter>
      <I18nProvider>
        <PurchaseReturnCreditNoteSection
          purchaseReturn={{ ...base, ...overrides }}
          onLinked={vi.fn()}
        />
      </I18nProvider>
    </MemoryRouter>,
  );
}

// PURCHASE-RETURN-DETAIL-DISPLAY-NAMES-01 — la NC vinculada ya no debe mostrar solo el Id crudo
// del documento ("documento ." cuando el valor venía vacío en la práctica) — debe mostrar el
// número de comprobante y, si existe, la clave de acceso.
describe("PurchaseReturnCreditNoteSection — nombres legibles de la NC vinculada", () => {
  it("muestra número y clave de acceso cuando el backend los resuelve", () => {
    show({
      supplierCreditNoteInvoiceNumber: "001-001-000000099",
      supplierCreditNoteAccessKey: "AK-12345",
    });

    expect(screen.getByText(/Nota de Crédito del proveedor/)).toBeTruthy();
    expect(screen.getByText("001-001-000000099")).toBeTruthy();
    expect(screen.getByText("AK-12345")).toBeTruthy();
    expect(screen.queryByText("doc-guid-123")).toBeNull();
  });

  it("muestra fecha de emisión, total NC y factura afectada cuando el backend los resuelve", () => {
    show({
      supplierCreditNoteInvoiceNumber: "001-001-000000099",
      supplierCreditNoteAccessKey: "AK-12345",
      supplierCreditNoteIssueDate: "2026-08-15",
      supplierCreditNoteTotalAmount: 115,
      purchaseInvoiceNumber: "001-001-000000042",
    });

    expect(screen.getByText("15/08/2026")).toBeTruthy();
    expect(screen.getByText("115.00")).toBeTruthy();
    expect(screen.getByText("001-001-000000042")).toBeTruthy();
    expect(screen.queryByText("purchase-1")).toBeNull();
  });

  it("muestra botón 'Ver NC de compra' cuando hay una PurchaseCreditNote interna vinculada", () => {
    show({
      supplierCreditNoteInvoiceNumber: "001-001-000000099",
      supplierCreditNoteAccessKey: "AK-12345",
      linkedPurchaseCreditNoteId: "credit-note-1",
      linkedPurchaseCreditNoteStatus: "Authorized",
    });

    expect(screen.getByText("Ver NC de compra")).toBeTruthy();
    expect(screen.getByText("Autorizada")).toBeTruthy();
  });

  it("no muestra 'documento .' vacío ni el GUID crudo si el backend no pudo resolver ningún dato", () => {
    show({ supplierCreditNoteInvoiceNumber: null, supplierCreditNoteAccessKey: null });

    expect(screen.getByText("Nota de Crédito vinculada — NC no encontrada.")).toBeTruthy();
    expect(screen.queryByText(/documento \./)).toBeNull();
    expect(screen.queryByText(/doc-guid-123/)).toBeNull();
  });

  it("muestra 'Sin nota de crédito vinculada' si no hay documento ni número resueltos", () => {
    show({
      supplierCreditNoteDocumentId: null,
      supplierCreditNoteInvoiceNumber: null,
      supplierCreditNoteAccessKey: null,
    });

    expect(screen.getByText("Sin nota de crédito vinculada.")).toBeTruthy();
  });

  it("no renderiza nada si la devolución no tiene NC vinculada ni pendiente", () => {
    const { container } = render(
      <MemoryRouter>
        <I18nProvider>
          <PurchaseReturnCreditNoteSection
            purchaseReturn={{ ...base, fiscalStatus: "NotApplicable" }}
            onLinked={vi.fn()}
          />
        </I18nProvider>
      </MemoryRouter>,
    );
    expect(container.textContent).toBe("");
  });
});
