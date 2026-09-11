// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
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
};

function show(overrides: Partial<PurchaseReturnDto> = {}) {
  render(
    <I18nProvider>
      <PurchaseReturnCreditNoteSection
        purchaseReturn={{ ...base, ...overrides }}
        onLinked={vi.fn()}
      />
    </I18nProvider>,
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

    expect(screen.getByText(/Nota de Crédito vinculada/)).toBeTruthy();
    expect(screen.getByText("001-001-000000099")).toBeTruthy();
    expect(screen.getByText("AK-12345")).toBeTruthy();
    expect(screen.queryByText("doc-guid-123")).toBeNull();
  });

  it("cae de vuelta al Id crudo del documento si el backend no pudo resolver el número", () => {
    show({ supplierCreditNoteInvoiceNumber: null, supplierCreditNoteAccessKey: null });

    expect(screen.getByText(/documento/)).toBeTruthy();
    expect(screen.getByText(/doc-guid-123/)).toBeTruthy();
  });

  it("no renderiza nada si la devolución no tiene NC vinculada ni pendiente", () => {
    const { container } = render(
      <I18nProvider>
        <PurchaseReturnCreditNoteSection
          purchaseReturn={{ ...base, fiscalStatus: "NotApplicable" }}
          onLinked={vi.fn()}
        />
      </I18nProvider>,
    );
    expect(container.textContent).toBe("");
  });
});
