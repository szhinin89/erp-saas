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
vi.mock("../../purchases/facades/purchaseReceptionExpenseFacade", () => ({
  purchaseReceptionExpenseFacade: { prepareExpenseDraft: mocks.preview },
}));
vi.mock("../api/expenseDocumentService", () => ({
  expenseDocumentService: { create: mocks.create },
}));
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ has: () => true }),
}));
vi.mock("../../accounting/facades/accountLookupFacade", () => ({
  accountLookupFacade: { listAccounts: async () => [{ id: "account" }] },
}));
vi.mock("../../masterData/facades/paymentTermLookupFacade", () => ({ paymentTermLookupFacade: { list: async () => [] } }));
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
    { id: "subcategory", isActive: true, accountingAccountId: "account", accountingAccountCode: "5.1.1", accountingAccountName: "Servicios" },
  ] }] }] },
}));
const toast = vi.hoisted(() => ({ warning: vi.fn() }));
vi.mock("../../../lib/messages", () => ({ message: { success: vi.fn(), error: vi.fn(), warning: toast.warning } }));
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

  // EXPENSE-UI-02: indicador compacto persistente + un solo toast oficial; sin aviso grande.
  await screen.findByRole("button", { name: "Total distinto al XML" });
  expect(toast.warning).toHaveBeenCalledTimes(1);
  expect(toast.warning).toHaveBeenCalledWith("El total no coincide con el XML.");
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
    screen.queryByRole("button", { name: "Total distinto al XML" }),
  ).toBeNull());
  // Al abrir (conceptos sin completar) y al cuadrar no hay toast: nunca hubo importes distintos.
  expect(toast.warning).not.toHaveBeenCalled();
  const saveButton = screen.getByRole("button", { name: "Guardar borrador" }) as HTMLButtonElement;
  expect(saveButton.disabled).toBe(false);

  fireEvent.click(saveButton);
  await waitFor(() => expect(mocks.create).toHaveBeenCalled());
});

it("EXPENSE-UI-02: la diferencia se consulta, no repite el toast y se retira al cuadrar; reaparece con aviso nuevo", async () => {
  show();
  await waitFor(() => expect(mocks.preview).toHaveBeenCalled());
  // Al abrir desde XML con conceptos sin completar: indicador visible, pero todavía sin toast.
  await screen.findByRole("button", { name: "Total distinto al XML" });
  expect(toast.warning).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Seleccionar IVA 5%" }));
  const badge = await screen.findByRole("button", { name: "Total distinto al XML" });
  expect(toast.warning).toHaveBeenCalledTimes(1);

  // Más ediciones con la diferencia vigente: el indicador persiste y el toast no se repite.
  fireEvent.click(screen.getByRole("button", { name: "Seleccionar IVA 5%" }));
  expect(screen.getByRole("button", { name: "Total distinto al XML" })).toBe(badge);
  expect(toast.warning).toHaveBeenCalledTimes(1);

  // Consulta de detalle: total XML, IVA XML y total calculado.
  fireEvent.click(badge);
  const detail = await screen.findByText(/Total XML recibido/);
  expect(detail.textContent).toContain("103.50");
  expect(detail.textContent).toContain("13.50");
  expect(detail.textContent).toContain("94.50");

  // Al cuadrar, el indicador desaparece solo y no hay aviso nuevo.
  fireEvent.click(screen.getByRole("button", { name: "Seleccionar IVA 15%" }));
  await waitFor(() => expect(screen.queryByRole("button", { name: "Total distinto al XML" })).toBeNull());
  expect(toast.warning).toHaveBeenCalledTimes(1);

  // Si la diferencia reaparece, es una aparición nueva → un aviso nuevo.
  fireEvent.click(screen.getByRole("button", { name: "Seleccionar IVA 5%" }));
  await screen.findByRole("button", { name: "Total distinto al XML" });
  expect(toast.warning).toHaveBeenCalledTimes(2);
  // La advertencia no añade bloqueos: el guardado sigue bloqueado SOLO por la regla previa.
  expect((screen.getByRole("button", { name: "Guardar borrador" }) as HTMLButtonElement).disabled).toBe(true);
});
