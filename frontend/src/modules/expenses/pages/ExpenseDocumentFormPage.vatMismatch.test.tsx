// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { ExpenseDocumentFormPage } from "./ExpenseDocumentFormPage";
import type { ExpenseDocumentHeaderState } from "../components/ExpenseDocumentHeader";
import type { ExpenseDraftLineState } from "../components/ExpenseDocumentLinesEditor";

/**
 * EXPENSES-VAT-CODE-PERCENTAGE-VISIBILITY-01 — un gasto creado desde recepcion XML debe
 * comparar el total calculado en pantalla contra el total recibido en el XML y bloquear el
 * guardado cuando no cuadran (bug real: la UI permitia elegir "20 - IVA 5%" para una factura
 * cuyo XML traia 15%, guardando un total distinto al de la factura real).
 */

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
vi.mock("../../items/facades/sriLookupFacade", () => ({
  sriLookupFacade: {
    docTypes: async () => [{ code: "01", name: "Factura", shortName: "FAC", isElectronic: true }],
    taxSupportCodes: async () => [],
    vatRates: async () => [
      { code: "4", name: "15% IVA (tarifa general vigente)", percentage: 15 },
      { code: "5", name: "5% IVA", percentage: 5 },
    ],
  },
}));
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
  }) => (
    <>
      <button onClick={() => onChange(lines.map(line => ({
        ...line, expenseSubcategoryId: "subcategory", unitPrice: "90", vatCode: "5",
      })))}>Seleccionar IVA 5%</button>
      <button onClick={() => onChange(lines.map(line => ({
        ...line, expenseSubcategoryId: "subcategory", unitPrice: "90", vatCode: "4",
      })))}>Seleccionar IVA 15%</button>
    </>
  ),
}));

// XML recibido: subtotal 90.00, IVA 13.50, total 103.50 (caso reportado en el bug).
const source = {
  receptionDocumentId: "reception-1", accessKey: "1".repeat(49),
  supplierId: "supplier-1", supplierName: "Proveedor", supplierTaxId: "1790012345001",
  issueDate: "2026-09-01", documentType: "01", documentNumber: "001-001-000000001",
  authorizationNumber: "1".repeat(49), authorizationDate: null,
  subtotal: 90, vatAmount: 13.5, total: 103.5,
};

function show() {
  return render(<I18nProvider><MemoryRouter initialEntries={["/expenses/documents/new?fromReceptionId=reception-1"]}>
    <ExpenseDocumentFormPage />
  </MemoryRouter></I18nProvider>);
}

beforeEach(() => { vi.clearAllMocks(); mocks.preview.mockResolvedValue(source); mocks.create.mockResolvedValue({ id: "expense-1" }); });
afterEach(cleanup);

it("blocks saving when the code 5% total (94.50) does not match the XML total (103.50)", async () => {
  show();
  await waitFor(() => expect(mocks.preview).toHaveBeenCalled());
  fireEvent.click(screen.getByRole("button", { name: "Seleccionar IVA 5%" }));

  await screen.findByText(/El total calculado no cuadra con el XML recibido/);
  const saveButton = screen.getByRole("button", { name: "Guardar borrador" }) as HTMLButtonElement;
  expect(saveButton.disabled).toBe(true);

  fireEvent.click(saveButton);
  expect(mocks.create).not.toHaveBeenCalled();
});

it("allows saving when the code 15% total (103.50) matches the XML total", async () => {
  show();
  await waitFor(() => expect(mocks.preview).toHaveBeenCalled());
  fireEvent.click(screen.getByRole("button", { name: "Seleccionar IVA 15%" }));

  await waitFor(() => expect(
    screen.queryByText(/El total calculado no cuadra con el XML recibido/),
  ).toBeNull());
  const saveButton = screen.getByRole("button", { name: "Guardar borrador" }) as HTMLButtonElement;
  expect(saveButton.disabled).toBe(false);

  fireEvent.click(saveButton);
  await waitFor(() => expect(mocks.create).toHaveBeenCalled());
});
