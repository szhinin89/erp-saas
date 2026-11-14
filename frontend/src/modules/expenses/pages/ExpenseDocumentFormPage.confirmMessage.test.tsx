// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { ExpenseDocumentFormPage } from "./ExpenseDocumentFormPage";
import { expenseDocumentService } from "../api/expenseDocumentService";
import { expenseCategoryService } from "../api/expenseCategoryService";
import { accountLookupFacade } from "../../accounting/facades/accountLookupFacade";
import { paymentTermLookupFacade } from "../../masterData/facades/paymentTermLookupFacade";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import type { ExpenseDocumentDetailDto } from "../api/expenseDocumentService";
import esDictionary from "../../../i18n/locales/es.json";
import enDictionary from "../../../i18n/locales/en.json";

/**
 * EXPENSE-UX-01 — el mensaje tras confirmar no afirma que el asiento ya exista: la contabilización
 * se despacha después del commit (ACCOUNTING-CONTINUITY-01) y puede seguir pendiente.
 */

const ES_MESSAGE =
  "Gasto confirmado correctamente. El registro contable se procesa automáticamente.";
const EN_MESSAGE =
  "Expense confirmed successfully. The accounting record is processed automatically.";

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return { ...actual, useNavigate: () => vi.fn(), useParams: () => ({ id: "exp-1" }) };
});
vi.mock("../api/expenseDocumentService", () => ({
  expenseDocumentService: {
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    confirm: vi.fn(),
    cancel: vi.fn(),
    getRetentionEligibility: vi.fn(),
    getExpenseRetention: vi.fn(),
  },
}));
vi.mock("../api/expenseCategoryService", () => ({
  expenseCategoryService: { getTree: vi.fn() },
}));
vi.mock("../../accounting/facades/accountLookupFacade", () => ({
  accountLookupFacade: { listAccounts: vi.fn() },
}));
vi.mock("../../masterData/facades/paymentTermLookupFacade", () => ({
  paymentTermLookupFacade: { list: vi.fn() },
}));
vi.mock("../../emissionPoints/facades/emissionPointLookupFacade", () => ({
  emissionPointLookupFacade: { list: vi.fn().mockResolvedValue([]) },
}));
vi.mock("../../items/facades/sriLookupFacade", () => ({
  sriLookupFacade: {
    docTypes: async () => [{ code: "01", name: "Factura", shortName: "FAC", isElectronic: true }],
    taxSupportCodes: vi.fn(),
    vatRates: vi.fn(),
  },
}));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn(), confirm: vi.fn() },
}));

const DRAFT = {
  id: "exp-1",
  companyId: "company-1",
  branchId: "branch-1",
  supplierId: "sup-1",
  supplierName: "Proveedor Uno S.A.",
  supplierTaxId: "0999999999001",
  issueDate: "2026-09-01",
  accountingDate: "2026-09-01",
  documentType: "01",
  documentNumber: "001-001-000000001",
  authorizationNumber: null,
  authorizationDate: null,
  paymentTermId: "pt-1",
  paymentTermName: "Contado",
  dueDate: null,
  subtotal: 100,
  totalDiscount: 0,
  totalTax: 15,
  grandTotal: 115,
  notes: null,
  taxSupportCode: null,
  status: "Draft",
  lines: [],
  cancelReason: null,
  cancelledAt: null,
  cancelledBy: null,
} as ExpenseDocumentDetailDto;

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(accountLookupFacade.listAccounts).mockResolvedValue([]);
  vi.mocked(paymentTermLookupFacade.list).mockResolvedValue([]);
  vi.mocked(expenseCategoryService.getTree).mockResolvedValue([]);
  vi.mocked(sriLookupFacade.taxSupportCodes).mockResolvedValue([]);
  vi.mocked(sriLookupFacade.vatRates).mockResolvedValue([]);
  vi.mocked(expenseDocumentService.getById).mockResolvedValue(DRAFT);
  vi.mocked(expenseDocumentService.getRetentionEligibility).mockResolvedValue({
    canRetainVat: false,
    canRetainIncome: false,
    isSupplierExempt: false,
    hasRetainableBase: true,
    missingRetentionCode: false,
    isSupplierRequiredToKeepAccounting: false,
    candidates: [],
    reasons: [],
    isEligible: false,
  });
  vi.mocked(expenseDocumentService.getExpenseRetention).mockResolvedValue(null);
});

afterEach(cleanup);

async function openAndConfirm() {
  render(
    <I18nProvider>
      <MemoryRouter>
        <ExpenseDocumentFormPage />
      </MemoryRouter>
    </I18nProvider>,
  );
  await waitFor(() => expect(screen.getByText("Proveedor Uno S.A.")).toBeTruthy());
  fireEvent.click(screen.getByRole("button", { name: "Confirmar gasto" }));
  await waitFor(() =>
    expect(screen.getAllByRole("button", { name: "Confirmar gasto" })).toHaveLength(2),
  );
  fireEvent.click(screen.getAllByRole("button", { name: "Confirmar gasto" })[1]);
}

describe("ExpenseDocumentFormPage — mensaje tras confirmar", () => {
  it("tras una confirmación exitosa muestra el mensaje que no afirma contabilización terminada", async () => {
    vi.mocked(expenseDocumentService.confirm).mockResolvedValue({ ...DRAFT, status: "Confirmed" });
    await openAndConfirm();

    await waitFor(() => expect(message.success).toHaveBeenCalledWith(ES_MESSAGE));
    expect(message.success).toHaveBeenCalledTimes(1);
    expect(ES_MESSAGE).not.toMatch(/asiento|gener/i);
  });

  it("si la confirmación falla no se muestra el mensaje de éxito", async () => {
    vi.mocked(expenseDocumentService.confirm).mockRejectedValue(new Error("fallo"));
    await openAndConfirm();

    await waitFor(() => expect(message.error).toHaveBeenCalledWith("fallo"));
    expect(message.success).not.toHaveBeenCalled();
  });

  it("es y en tienen el mismo mensaje corregido", () => {
    const key = "expenses.form.message.confirmed";
    expect((esDictionary as Record<string, string>)[key]).toBe(ES_MESSAGE);
    expect((enDictionary as Record<string, string>)[key]).toBe(EN_MESSAGE);
  });
});
