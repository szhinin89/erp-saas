// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseReceptionActionsCell } from "./PurchaseReceptionActionsCell";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";

afterEach(() => { cleanup(); vi.restoreAllMocks(); });
const row: PurchaseReceptionItem = {
  documentId: "nc-1", sourceDocType: "CREDIT_NOTE", documentStatus: "IMPORTED",
  supplierRuc: "1790012345001", supplierName: "Proveedor", invoiceNumber: "001-001-000000001",
  modifiedDocumentNumber: "001-001-000000002", accessKey: "key", issueDate: "2026-07-01",
  authorizationDate: "2026-07-01", subtotal: 10, vatAmount: 1.5, total: 11.5,
  supplierExists: true, supplierId: "supplier-1", supplierIsActive: true,
  purchaseExists: false, purchaseId: null, purchaseState: "NONE",
  affectedPurchaseExists: true, affectedPurchaseId: "invoice-1",
  status: "PENDING", processingStatus: "PENDING", processingNotes: null, supplierTradeName: null,
};
function show(overrides: Partial<PurchaseReceptionItem> = {}) {
  const download = vi.fn();
  const view = vi.fn();
  const processCreditNote = vi.fn();
  render(<I18nProvider><PurchaseReceptionActionsCell row={{ ...row, ...overrides }}
    xmlState={undefined} onDownloadXml={download} onViewXml={view}
    onProcessCreditNote={processCreditNote} resolvingCreditNoteId={null} /></I18nProvider>);
  return { download, view, processCreditNote };
}
describe("Purchase reception document actions", () => {
  it("opens expense creation for a verified invoice", () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    show({ sourceDocType: "INVOICE", documentStatus: "VERIFIED" });
    fireEvent.click(screen.getByRole("button", { name: "Crear gasto" }));
    expect(open).toHaveBeenCalledWith("/expenses/documents/new?fromReceptionId=nc-1", "_blank", "noopener,noreferrer");
  });
  it("does not offer expense creation for credit notes", () => {
    show({ documentStatus: "VERIFIED" });
    expect(screen.queryByRole("button", { name: "Crear gasto" })).toBeNull();
  });
  it("requires the supplier for expense creation", () => {
    show({ sourceDocType: "INVOICE", documentStatus: "VERIFIED", supplierExists: false });
    const button = screen.getByRole("button", { name: "Crear gasto" }) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(button.title).toBe("Cree primero el proveedor");
  });
  // FASE 5 PURCHASE-UX: la compra activa la indica purchaseState (DRAFT/CONFIRMED), no el booleano.
  it.each([
    { expenseExists: true },
    { purchaseExists: true, purchaseId: "p-1", purchaseState: "CONFIRMED" as const },
    { purchaseExists: true, purchaseId: "d-1", purchaseState: "DRAFT" as const },
  ])("blocks both creation actions for consumed invoices %j", (used) => {
    show({ sourceDocType: "INVOICE", documentStatus: "VERIFIED", ...used });
    expect(screen.queryByRole("button", { name: "Crear gasto" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Crear compra" })).toBeNull();
  });
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
  it("blocks reprocessing and offers the existing credit note when already linked", () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    show({ creditNoteExists: true, creditNoteId: "cn-1" });
    expect(screen.queryByRole("button", { name: "Procesar NC" })).toBeNull();
    // FASE 5 RECEPTION-UX — el estado "NC procesada" vive en Registro ERP, no en Acciones.
    expect(screen.queryByText("NC procesada")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Ver NC" }));
    expect(open).toHaveBeenCalledWith(
      "/purchases/credit-notes/cn-1",
      "_blank",
      "noopener,noreferrer",
    );
  });
  it("offers reprocessing and the cancelled credit note history when the only prior NC was cancelled", () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    const { processCreditNote } = show({
      creditNoteExists: false,
      creditNoteId: null,
      cancelledCreditNoteId: "cn-cancelled-1",
    });

    expect(screen.queryByText("NC anulada")).toBeNull();
    expect(screen.queryByRole("button", { name: "Procesar NC" })).toBeNull();

    // PURCHASE-CREDIT-NOTE-AFFECTED-INVOICE-RESOLVES-CANCELLED-01 — el click ya no navega
    // directamente con row.affectedPurchaseId (podría estar obsoleto desde el import original);
    // delega a onProcessCreditNote, que re-resuelve la factura afectada vigente antes de navegar.
    fireEvent.click(screen.getByRole("button", { name: "Reprocesar NC" }));
    expect(processCreditNote).toHaveBeenCalledWith(
      expect.objectContaining({ documentId: "nc-1", cancelledCreditNoteId: "cn-cancelled-1" }),
    );

    fireEvent.click(screen.getByRole("button", { name: "Ver NC anulada" }));
    expect(open).toHaveBeenCalledWith(
      "/purchases/credit-notes/cn-cancelled-1",
      "_blank",
      "noopener,noreferrer",
    );
  });
  it("shows the plain first-time Procesar NC button when there is no prior credit note at all", () => {
    show({ creditNoteExists: false, creditNoteId: null, cancelledCreditNoteId: null });

    expect(screen.getByRole("button", { name: "Procesar NC" })).toBeTruthy();
    expect(screen.queryByText("NC anulada")).toBeNull();
    expect(screen.queryByRole("button", { name: "Ver NC anulada" })).toBeNull();
  });
  it("prioritizes the already-processed state over affectedPurchaseExists", () => {
    show({
      creditNoteExists: true,
      creditNoteId: "cn-1",
      affectedPurchaseExists: false,
      affectedPurchaseId: null,
    });
    expect(screen.getByRole("button", { name: "Ver NC" })).toBeTruthy();
    expect(screen.queryByText("NC pendiente")).toBeNull();
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

  // RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — mismo estándar de NC aplicado a compra/gasto.
  it("offers reprocessing and the cancelled purchase history when the only prior purchase was cancelled", () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    show({
      sourceDocType: "INVOICE",
      documentStatus: "VERIFIED",
      purchaseExists: false,
      purchaseId: null,
      purchaseState: "CANCELLED",
      cancelledPurchaseId: "purchase-cancelled-1",
    });

    expect(screen.queryByText("Compra anulada")).toBeNull();
    expect(screen.queryByRole("button", { name: "Crear compra" })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Reprocesar compra" }));
    expect(open).toHaveBeenCalledWith(
      "/purchases?fromReceptionId=nc-1&accessKey=key",
      "_blank",
      "noopener,noreferrer",
    );

    fireEvent.click(screen.getByRole("button", { name: "Ver compra anulada" }));
    expect(open).toHaveBeenCalledWith(
      "/purchases?invoiceId=purchase-cancelled-1",
      "_blank",
      "noopener,noreferrer",
    );
  });

  it("shows the plain first-time Crear compra button when there is no prior purchase at all", () => {
    show({
      sourceDocType: "INVOICE",
      documentStatus: "VERIFIED",
      purchaseExists: false,
      purchaseId: null,
      cancelledPurchaseId: null,
    });

    expect(screen.getByRole("button", { name: "Crear compra" })).toBeTruthy();
    expect(screen.queryByText("Compra anulada")).toBeNull();
    expect(screen.queryByRole("button", { name: "Ver compra anulada" })).toBeNull();
  });

  it("offers reprocessing and the cancelled expense history when the only prior expense was cancelled", () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    show({
      sourceDocType: "INVOICE",
      documentStatus: "VERIFIED",
      expenseExists: false,
      cancelledExpenseId: "expense-cancelled-1",
    });

    expect(screen.queryByText("Gasto anulado")).toBeNull();
    expect(screen.queryByRole("button", { name: "Crear gasto" })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Reprocesar gasto" }));
    expect(open).toHaveBeenCalledWith(
      "/expenses/documents/new?fromReceptionId=nc-1",
      "_blank",
      "noopener,noreferrer",
    );

    fireEvent.click(screen.getByRole("button", { name: "Ver gasto anulado" }));
    expect(open).toHaveBeenCalledWith(
      "/expenses/documents/expense-cancelled-1",
      "_blank",
      "noopener,noreferrer",
    );
  });

  it("shows the plain first-time Crear gasto button when there is no prior expense at all", () => {
    show({
      sourceDocType: "INVOICE",
      documentStatus: "VERIFIED",
      expenseExists: false,
      cancelledExpenseId: null,
    });

    expect(screen.getByRole("button", { name: "Crear gasto" })).toBeTruthy();
    expect(screen.queryByText("Gasto anulado")).toBeNull();
    expect(screen.queryByRole("button", { name: "Ver gasto anulado" })).toBeNull();
  });
});

describe("FASE 5 PURCHASE-UX — estado real de la compra en Recepción", () => {
  const invoice = { sourceDocType: "INVOICE" as const, documentStatus: "VERIFIED" as const };

  it("gasto activo: solo Ver XML, sin badge de estado ni Crear compra/gasto", () => {
    show({ ...invoice, expenseExists: true });
    expect(screen.queryByText("Gasto registrado")).toBeNull();
    expect(screen.queryByText("Gasto ya ingresado al sistema")).toBeNull();
    expect(screen.getByRole("button", { name: "Ver XML" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Crear compra" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Crear gasto" })).toBeNull();
  });

  it("NONE: Crear compra y Crear gasto permitidos", () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    show({ ...invoice, purchaseState: "NONE" });
    fireEvent.click(screen.getByRole("button", { name: "Crear compra" }));
    expect(open).toHaveBeenCalledWith("/purchases?fromReceptionId=nc-1&accessKey=key", "_blank", "noopener,noreferrer");
    expect(screen.getByRole("button", { name: "Crear gasto" })).toBeTruthy();
    expect(screen.queryByText("Compra anulada")).toBeNull();
  });

  it("DRAFT: Continuar borrador (invoiceId existente); sin Crear compra ni Crear gasto", () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    show({ ...invoice, documentStatus: "PROCESSED", purchaseState: "DRAFT", purchaseExists: true, purchaseId: "draft-1" });
    expect(screen.queryByText("Compra en borrador")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Continuar borrador" }));
    expect(open).toHaveBeenCalledWith("/purchases?invoiceId=draft-1", "_blank", "noopener,noreferrer");
    expect(screen.queryByRole("button", { name: "Crear compra" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Crear gasto" })).toBeNull();
  });

  it("CONFIRMED: Ver compra; sin Crear gasto", () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    show({ ...invoice, documentStatus: "PROCESSED", purchaseState: "CONFIRMED", purchaseExists: true, purchaseId: "p-1" });
    expect(screen.queryByText("Compra confirmada")).toBeNull();
    expect(screen.queryByText("Compra creada")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Ver compra" }));
    expect(open).toHaveBeenCalledWith("/purchases?invoiceId=p-1", "_blank", "noopener,noreferrer");
    expect(screen.queryByRole("button", { name: "Crear gasto" })).toBeNull();
  });

  it("CANCELLED: Reprocesar compra + Ver compra anulada; Crear gasto permitido", () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    show({ ...invoice, purchaseState: "CANCELLED", cancelledPurchaseId: "old-1" });
    expect(screen.queryByText("Compra anulada")).toBeNull();
    expect(screen.getByRole("button", { name: "Reprocesar compra" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Ver compra anulada" }));
    expect(open).toHaveBeenCalledWith("/purchases?invoiceId=old-1", "_blank", "noopener,noreferrer");
    expect(screen.getByRole("button", { name: "Crear gasto" })).toBeTruthy();
  });

  it("DISCARDED: Crear compra + Ver borrador descartado; Crear gasto permitido", () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    show({ ...invoice, purchaseState: "DISCARDED", cancelledPurchaseId: "discarded-1" });
    expect(screen.queryByText("Borrador descartado")).toBeNull();
    expect(screen.queryByRole("button", { name: "Reprocesar compra" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Crear compra" }));
    expect(open).toHaveBeenCalledWith("/purchases?fromReceptionId=nc-1&accessKey=key", "_blank", "noopener,noreferrer");
    fireEvent.click(screen.getByRole("button", { name: "Ver borrador descartado" }));
    expect(open).toHaveBeenCalledWith("/purchases?invoiceId=discarded-1", "_blank", "noopener,noreferrer");
    expect(screen.getByRole("button", { name: "Crear gasto" })).toBeTruthy();
  });
});
