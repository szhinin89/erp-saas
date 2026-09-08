// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { ExpenseDocumentFormPage } from "./ExpenseDocumentFormPage";
import type { ExpenseDocumentHeaderState } from "../components/ExpenseDocumentHeader";
import type { ExpenseDraftLineState } from "../components/ExpenseDocumentLinesEditor";

const mocks = vi.hoisted(() => ({ preview: vi.fn(), create: vi.fn() }));
vi.mock("../../purchases/api/purchaseReceptionService", () => ({
  purchaseReceptionService: { createExpenseDraft: mocks.preview },
}));
vi.mock("../api/expenseDocumentService", () => ({
  expenseDocumentService: { create: mocks.create },
}));
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ has: () => true }),
}));
vi.mock("../../accounting/api/accountingApi", () => ({
  accountingApi: { listAccounts: async () => [{ id: "account" }] },
}));
vi.mock("../../masterData/api/paymentTermService", () => ({ paymentTermService: { list: async () => [] } }));
vi.mock("../../items/facades/sriLookupFacade", () => ({ sriLookupFacade: { taxSupportCodes: async () => [] } }));
vi.mock("../api/expenseCategoryService", () => ({
  expenseCategoryService: { getTree: async () => [{ children: [{ children: [
    { id: "subcategory", isActive: true, accountingAccountId: "account" },
  ] }] }] },
}));
vi.mock("../../../lib/messages", () => ({ message: { success: vi.fn(), error: vi.fn() } }));
vi.mock("../components/ExpenseRetentionSection", () => ({ ExpenseRetentionSection: () => null }));
vi.mock("../components/ExpenseDocumentHeader", () => ({
  ExpenseDocumentHeader: ({ value }: { value: ExpenseDocumentHeaderState }) =>
    <output data-testid="header">{JSON.stringify(value)}</output>,
}));
vi.mock("../components/ExpenseDocumentLinesEditor", () => ({
  ExpenseDocumentLinesEditor: ({ lines, onChange }: {
    lines: ExpenseDraftLineState[];
    onChange: (lines: ExpenseDraftLineState[]) => void;
  }) => <button onClick={() => onChange(lines.map(line => ({
    ...line, expenseSubcategoryId: "subcategory", unitPrice: "100", vatCode: "2",
  })))}>Completar detalle</button>,
}));

const source = {
  receptionDocumentId: "reception-1", accessKey: "1".repeat(49),
  supplierId: "supplier-1", supplierName: "Proveedor", supplierTaxId: "1790012345001",
  issueDate: "2026-09-01", documentType: "01", documentNumber: "001-001-000000001",
  authorizationNumber: "1".repeat(49), authorizationDate: null,
  subtotal: 100, vatAmount: 15, total: 115,
};
function show(path = "/expenses/documents/new?fromReceptionId=reception-1") {
  return render(<I18nProvider><MemoryRouter initialEntries={[path]}>
    <ExpenseDocumentFormPage />
  </MemoryRouter></I18nProvider>);
}
beforeEach(() => { vi.clearAllMocks(); mocks.preview.mockResolvedValue(source); mocks.create.mockResolvedValue({ id: "expense-1" }); });
afterEach(cleanup);

it("loads the server header and sends the reception identity when saving", async () => {
  show();
  await waitFor(() => expect(screen.getByTestId("header").textContent).toContain(source.documentNumber));
  expect(mocks.preview).toHaveBeenCalledWith("reception-1");
  expect(screen.getByTestId("header").textContent).toContain(source.supplierId);
  expect(screen.getByTestId("header").textContent).toContain(source.issueDate);
  expect(screen.getByText(/Factura recibida/).textContent).toContain("115.00");
  fireEvent.click(screen.getByRole("button", { name: "Completar detalle" }));
  fireEvent.click(screen.getByRole("button", { name: "Guardar borrador" }));
  await waitFor(() => expect(mocks.create).toHaveBeenCalledWith(expect.objectContaining({
    receptionDocumentId: source.receptionDocumentId, accessKey: source.accessKey,
    documentNumber: source.documentNumber, supplierId: source.supplierId,
  })));
});

it("blocks saving when the backend rejects the reception", async () => {
  mocks.preview.mockRejectedValue(new Error("Recepcion usada"));
  show();
  await waitFor(() => expect(mocks.preview).toHaveBeenCalled());
  await screen.findByText("Recepcion usada");
  expect((screen.getByRole("button", { name: "Guardar borrador" }) as HTMLButtonElement).disabled).toBe(true);
  expect(mocks.create).not.toHaveBeenCalled();
});

it("keeps manual creation independent from reception", async () => {
  show("/expenses/documents/new");
  await waitFor(() => expect((screen.getByRole("button", { name: "Guardar borrador" }) as HTMLButtonElement).disabled).toBe(false));
  expect(mocks.preview).not.toHaveBeenCalled();
});
