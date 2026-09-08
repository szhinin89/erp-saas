// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseReceptionActionsCell } from "./PurchaseReceptionActionsCell";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";

afterEach(cleanup);
const row: PurchaseReceptionItem = {
  documentId: "nc-1", sourceDocType: "CREDIT_NOTE", documentStatus: "IMPORTED",
  supplierRuc: "1790012345001", supplierName: "Proveedor", invoiceNumber: "001-001-000000001",
  modifiedDocumentNumber: "001-001-000000002", accessKey: "key", issueDate: "2026-07-01",
  authorizationDate: "2026-07-01", subtotal: 10, vatAmount: 1.5, total: 11.5,
  supplierExists: true, supplierId: "supplier-1", supplierIsActive: true,
  purchaseExists: false, purchaseId: null, affectedPurchaseExists: true, affectedPurchaseId: "invoice-1",
  status: "PENDING", processingStatus: "PENDING", processingNotes: null, supplierTradeName: null,
};
function show(overrides: Partial<PurchaseReceptionItem> = {}) {
  const download = vi.fn();
  const view = vi.fn();
  render(<I18nProvider><PurchaseReceptionActionsCell row={{ ...row, ...overrides }}
    xmlState={undefined} onDownloadXml={download} onViewXml={view} /></I18nProvider>);
  return { download, view };
}
describe("Purchase reception document actions", () => {
  it("consults XML for an imported credit note and keeps its own processing action", () => {
    const { download } = show();
    fireEvent.click(screen.getByRole("button", { name: "Consultar XML" }));
    expect(download).toHaveBeenCalledWith("nc-1");
    expect(screen.getByRole("button", { name: "Procesar NC" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Crear compra" })).toBeNull();
  });
  it("consults XML even before the affected invoice exists", () => {
    show({ affectedPurchaseExists: false, affectedPurchaseId: null });
    expect(screen.getByRole("button", { name: "Consultar XML" })).toBeTruthy();
  });
  it.each(["VERIFIED", "PROCESSED"] as const)("views saved credit note XML in %s", (documentStatus) => {
    const { view } = show({ documentStatus });
    fireEvent.click(screen.getByRole("button", { name: "Ver XML" }));
    expect(view).toHaveBeenCalledWith("nc-1");
    expect(screen.queryByRole("button", { name: "Consultar XML" })).toBeNull();
  });
  it("keeps invoice consultation and creation", () => {
    const { download } = show({ sourceDocType: "INVOICE" });
    fireEvent.click(screen.getByRole("button", { name: "Consultar XML" }));
    expect(download).toHaveBeenCalledWith("nc-1");
    cleanup();
    show({ sourceDocType: "INVOICE", documentStatus: "VERIFIED" });
    expect(screen.getByRole("button", { name: "Crear compra" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Procesar NC" })).toBeNull();
  });
});
