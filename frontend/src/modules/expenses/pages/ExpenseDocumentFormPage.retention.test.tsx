// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { ExpenseDocumentFormPage } from "./ExpenseDocumentFormPage";
import { expenseDocumentService } from "../api/expenseDocumentService";
import { expenseCategoryService } from "../api/expenseCategoryService";
import { accountingApi } from "../../accounting/api/accountingApi";
import { paymentTermService } from "../../masterData/api/paymentTermService";
import { emissionPointsService } from "../../emissionPoints/api/emissionPointsService";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import type {
  ExpenseDocumentDetailDto,
  RetentionEligibilityResult,
  RetentionDocumentDto,
} from "../api/expenseDocumentService";

/**
 * RETENTIONS-UI-EXPENSES-01F — cubre la sección de retención integrada dentro del formulario
 * de Gastos: elegibilidad mostrada, captura de intención, envío al confirmar, y consulta de la
 * retención asociada a un gasto ya confirmado.
 */

const routeParams: { id?: string } = { id: "exp-1" };

vi.mock("react-router-dom", async () => {
  const actual =
    await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return {
    ...actual,
    useNavigate: () => vi.fn(),
    useParams: () => routeParams,
  };
});

vi.mock("../api/expenseDocumentService", () => ({
  expenseDocumentService: {
    list: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    confirm: vi.fn(),
    createConfirmedExpense: vi.fn(),
    cancel: vi.fn(),
    getRetentionEligibility: vi.fn(),
    getExpenseRetention: vi.fn(),
  },
}));

vi.mock("../api/expenseCategoryService", () => ({
  expenseCategoryService: {
    getTree: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    activate: vi.fn(),
    deactivate: vi.fn(),
  },
}));

vi.mock("../../accounting/api/accountingApi", () => ({
  accountingApi: { listAccounts: vi.fn() },
}));

vi.mock("../../masterData/api/paymentTermService", () => ({
  paymentTermService: { list: vi.fn() },
}));

vi.mock("../../emissionPoints/api/emissionPointsService", () => ({
  emissionPointsService: {
    list: vi.fn(),
    establishmentLookups: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    disable: vi.fn(),
    enable: vi.fn(),
  },
}));

// RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H: mismo catálogo real (global.sri_tax_support) ya usado
// por Compras — mockeado aquí solo para aislar el test de la llamada de red real.
vi.mock("../../items/facades/sriLookupFacade", () => ({
  sriLookupFacade: {
    taxSupportCodes: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn(), confirm: vi.fn() },
}));

function grantAll() {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
}

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <ExpenseDocumentFormPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

const DRAFT_DOCUMENT: ExpenseDocumentDetailDto = {
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
  paymentTermId: "",
  paymentTermName: "",
  dueDate: null,
  subtotal: 100,
  totalDiscount: 0,
  totalTax: 15,
  grandTotal: 115,
  notes: null,
  taxSupportCode: null,
  status: "Draft",
  lines: [
    {
      id: "line-1",
      expenseSubcategoryId: "sub-1",
      snapshotAccountingAccountId: "acc-1",
      snapshotAccountingAccountCode: "5.1.1",
      snapshotAccountingAccountName: "Servicios",
      description: "Servicio de mantenimiento",
      quantity: 1,
      unitAmount: 100,
      discountAmount: 0,
      vatCode: "2",
      vatRate: 15,
      vatAmount: 15,
      taxInclusiveTotal: 115,
      sortOrder: 1,
      notes: null,
    },
  ],
  cancelReason: null,
  cancelledAt: null,
  cancelledBy: null,
};

const CONFIRMED_DOCUMENT: ExpenseDocumentDetailDto = {
  ...DRAFT_DOCUMENT,
  id: "exp-2",
  status: "Confirmed",
};

const ELIGIBLE_RESULT: RetentionEligibilityResult = {
  canRetainVat: true,
  canRetainIncome: false,
  isSupplierExempt: false,
  hasRetainableBase: true,
  missingRetentionCode: false,
  isSupplierRequiredToKeepAccounting: false,
  suggestedVatRetentionCode: "303",
  suggestedIncomeRetentionCode: null,
  reasons: ["La empresa actual está configurada para retener IVA."],
  isEligible: true,
};

const NOT_CONFIGURED_RESULT: RetentionEligibilityResult = {
  canRetainVat: false,
  canRetainIncome: false,
  isSupplierExempt: false,
  hasRetainableBase: true,
  missingRetentionCode: false,
  isSupplierRequiredToKeepAccounting: false,
  suggestedVatRetentionCode: null,
  suggestedIncomeRetentionCode: null,
  reasons: ["La empresa actual no está configurada para retener IVA."],
  isEligible: false,
};

const SUPPLIER_EXEMPT_RESULT: RetentionEligibilityResult = {
  ...NOT_CONFIGURED_RESULT,
  canRetainVat: false,
  isSupplierExempt: true,
  reasons: ["El proveedor está marcado como exento de retención."],
};

const MISSING_CODE_RESULT: RetentionEligibilityResult = {
  ...NOT_CONFIGURED_RESULT,
  canRetainVat: false,
  missingRetentionCode: true,
  reasons: ["No existe código de retención activo para esta operación."],
};

const EMISSION_POINT = {
  id: "ep-1",
  establishmentId: "est-1",
  establishmentCode: "001",
  establishmentName: "Matriz",
  branchName: null,
  code: "001",
  name: "Punto principal",
  emissionType: "Physical" as const,
  isDefault: true,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
};

const RETENTION_DOC: RetentionDocumentDto = {
  id: "ret-1",
  companyId: "company-1",
  branchId: "branch-1",
  sourceDocumentType: "ExpenseDocument",
  sourceDocumentId: "exp-2",
  subjectBusinessPartnerId: "sup-1",
  emissionPointId: "ep-1",
  retentionNumber: "001-001-000000005",
  issueDate: "2026-09-01",
  status: "Issued",
  totalRetainedVat: 30,
  totalRetainedIncome: 0,
  totalRetained: 30,
  cancelReason: null,
  cancelledAt: null,
  cancelledBy: null,
  lines: [
    {
      id: "ret-line-1",
      taxType: "Vat",
      retentionCode: "303",
      baseAmount: 100,
      retentionRate: 30,
      retainedAmount: 30,
      description: null,
    },
  ],
};

beforeEach(() => {
  vi.clearAllMocks();
  routeParams.id = "exp-1";
  grantAll();
  vi.mocked(accountingApi.listAccounts).mockResolvedValue([]);
  vi.mocked(paymentTermService.list).mockResolvedValue([]);
  vi.mocked(expenseCategoryService.getTree).mockResolvedValue([]);
  vi.mocked(emissionPointsService.list).mockResolvedValue([EMISSION_POINT]);
  vi.mocked(sriLookupFacade.taxSupportCodes).mockResolvedValue([]);
  vi.mocked(expenseDocumentService.getExpenseRetention).mockResolvedValue(null);
});

afterEach(() => {
  cleanup();
});

describe("ExpenseDocumentFormPage — sección de retención", () => {
  it("caso 1 (regresión): sin marcar retención, confirmar no envía `retention` en el body", async () => {
    vi.mocked(expenseDocumentService.getById).mockResolvedValue(DRAFT_DOCUMENT);
    vi.mocked(expenseDocumentService.getRetentionEligibility).mockResolvedValue(ELIGIBLE_RESULT);
    vi.mocked(expenseDocumentService.confirm).mockResolvedValue(DRAFT_DOCUMENT);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno S.A.")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: /confirmar gasto/i }));
    await waitFor(() => expect(screen.getAllByText(/confirmar gasto/i).length).toBeGreaterThan(0));
    fireEvent.click(screen.getAllByRole("button", { name: /confirmar gasto/i })[1]);

    await waitFor(() => expect(expenseDocumentService.confirm).toHaveBeenCalled());
    expect(expenseDocumentService.confirm).toHaveBeenCalledWith("exp-1", undefined);
  });

  it("caso 2: muestra el motivo cuando la empresa no retiene IVA", async () => {
    vi.mocked(expenseDocumentService.getById).mockResolvedValue(DRAFT_DOCUMENT);
    vi.mocked(expenseDocumentService.getRetentionEligibility).mockResolvedValue(
      NOT_CONFIGURED_RESULT,
    );

    renderPage();

    await waitFor(() =>
      expect(
        screen.getByText("La empresa actual no está configurada para retener IVA."),
      ).toBeTruthy(),
    );
  });

  it("caso 3: muestra el motivo cuando el proveedor está exento", async () => {
    vi.mocked(expenseDocumentService.getById).mockResolvedValue(DRAFT_DOCUMENT);
    vi.mocked(expenseDocumentService.getRetentionEligibility).mockResolvedValue(
      SUPPLIER_EXEMPT_RESULT,
    );

    renderPage();

    await waitFor(() =>
      expect(
        screen.getByText("El proveedor está marcado como exento de retención."),
      ).toBeTruthy(),
    );
    expect(screen.getByText("Proveedor exento")).toBeTruthy();
  });

  it("caso 4: muestra el motivo cuando falta código de retención activo", async () => {
    vi.mocked(expenseDocumentService.getById).mockResolvedValue(DRAFT_DOCUMENT);
    vi.mocked(expenseDocumentService.getRetentionEligibility).mockResolvedValue(
      MISSING_CODE_RESULT,
    );

    renderPage();

    await waitFor(() =>
      expect(
        screen.getByText("No existe código de retención activo para esta operación."),
      ).toBeTruthy(),
    );
    expect(screen.getByText("Falta código de retención activo")).toBeTruthy();
  });

  it("caso 5: permite capturar RetentionIntent cuando es elegible", async () => {
    vi.mocked(expenseDocumentService.getById).mockResolvedValue(DRAFT_DOCUMENT);
    vi.mocked(expenseDocumentService.getRetentionEligibility).mockResolvedValue(ELIGIBLE_RESULT);

    renderPage();
    await waitFor(() => {
      const el = screen.getByLabelText(
        "Aplicar retención a este gasto",
      ) as HTMLInputElement;
      expect(el.disabled).toBe(false);
    });

    const toggle = screen.getByLabelText("Aplicar retención a este gasto") as HTMLInputElement;
    expect(toggle.disabled).toBe(false);

    fireEvent.click(toggle);

    await waitFor(() => expect(screen.getByText("Punto de emisión")).toBeTruthy());
    expect(screen.getByText("Fecha de emisión")).toBeTruthy();
    // RETENTIONS-UI-REMOVE-MANUAL-NUMBER-02F: ya no hay un campo editable de número de
    // retención — solo el mensaje informativo de que el servidor lo genera al confirmar.
    expect(
      screen.getByText(
        "El número de retención se generará automáticamente al confirmar este documento.",
      ),
    ).toBeTruthy();
    expect(screen.queryByText("Número de retención")).toBeNull();
    expect(screen.queryByLabelText(/^Número de retención/)).toBeNull();
  });

  it("caso 6 y 7: envía RetentionIntent al confirmar, sin tenantId/companyId/branchId en el body", async () => {
    vi.mocked(expenseDocumentService.getById).mockResolvedValue(DRAFT_DOCUMENT);
    vi.mocked(expenseDocumentService.getRetentionEligibility).mockResolvedValue(ELIGIBLE_RESULT);
    vi.mocked(expenseDocumentService.confirm).mockResolvedValue(DRAFT_DOCUMENT);

    renderPage();
    await waitFor(() => {
      const el = screen.getByLabelText(
        "Aplicar retención a este gasto",
      ) as HTMLInputElement;
      expect(el.disabled).toBe(false);
    });
    fireEvent.click(screen.getByLabelText("Aplicar retención a este gasto"));

    await waitFor(() => expect(screen.getByText("Punto de emisión")).toBeTruthy());

    // Los campos ZHField marcan "required" agregando "*" al texto del <label> (p. ej.
    // "Punto de emisión*") — se localizan por regex sobre el texto exacto del label. Ya no hay
    // un campo "Número de retención" que capturar (RETENTIONS-UI-REMOVE-MANUAL-NUMBER-02F): el
    // backend lo genera siempre a partir de emissionPointId.
    fireEvent.change(screen.getByLabelText(/^Punto de emisión/), {
      target: { value: "ep-1" },
    });
    fireEvent.change(screen.getByLabelText(/^Fecha de emisión/), {
      target: { value: "2026-09-01" },
    });
    fireEvent.change(screen.getByLabelText(/^Código de retención/), {
      target: { value: "303" },
    });
    fireEvent.change(screen.getByLabelText(/^Base/), { target: { value: "100" } });
    fireEvent.change(screen.getByLabelText(/^% Retención/), { target: { value: "30" } });
    fireEvent.change(screen.getByLabelText(/^Valor retenido/), { target: { value: "30" } });

    fireEvent.click(screen.getByRole("button", { name: /confirmar gasto/i }));
    await waitFor(() =>
      expect(screen.getAllByRole("button", { name: /confirmar gasto/i })).toHaveLength(2),
    );
    fireEvent.click(screen.getAllByRole("button", { name: /confirmar gasto/i })[1]);

    await waitFor(() => expect(expenseDocumentService.confirm).toHaveBeenCalled());
    const [calledId, calledIntent] = vi.mocked(expenseDocumentService.confirm).mock.calls[0];
    expect(calledId).toBe("exp-1");
    expect(calledIntent).toBeTruthy();
    expect(calledIntent).toMatchObject({
      appliesRetention: true,
      emissionPointId: "ep-1",
      issueDate: "2026-09-01",
    });
    expect(calledIntent).not.toHaveProperty("tenantId");
    expect(calledIntent).not.toHaveProperty("companyId");
    expect(calledIntent).not.toHaveProperty("branchId");
    // RETENTIONS-UI-REMOVE-MANUAL-NUMBER-02F: el payload nunca envía un número manual — el
    // backend lo genera siempre a partir de emissionPointId.
    expect(calledIntent).not.toHaveProperty("retentionNumber");
    expect(calledIntent?.lines?.[0]).not.toHaveProperty("tenantId");
  });

  it("caso 9: un documento confirmado CON retención muestra el resumen (número/fecha/estado/total/líneas)", async () => {
    routeParams.id = "exp-2";
    vi.mocked(expenseDocumentService.getById).mockResolvedValue(CONFIRMED_DOCUMENT);
    vi.mocked(expenseDocumentService.getExpenseRetention).mockResolvedValue(RETENTION_DOC);

    renderPage();

    await waitFor(() =>
      expect(screen.getByText("001-001-000000005")).toBeTruthy(),
    );
    expect(screen.getByText("Emitida")).toBeTruthy();
    expect(screen.getByText((text) => text.includes("303"))).toBeTruthy();
    expect(message.error).not.toHaveBeenCalled();
  });

  it("caso 10: un documento confirmado SIN retención muestra estado neutro, no un error", async () => {
    routeParams.id = "exp-2";
    vi.mocked(expenseDocumentService.getById).mockResolvedValue(CONFIRMED_DOCUMENT);
    vi.mocked(expenseDocumentService.getExpenseRetention).mockResolvedValue(null);

    renderPage();

    await waitFor(() => expect(screen.getByText("Sin retención asociada.")).toBeTruthy());
    expect(message.error).not.toHaveBeenCalled();
  });

  it("caso 11: los inputs numéricos de la línea de retención usan ZhDecimalInput (zh-numeric-input), no <input type=number> crudo", async () => {
    vi.mocked(expenseDocumentService.getById).mockResolvedValue(DRAFT_DOCUMENT);
    vi.mocked(expenseDocumentService.getRetentionEligibility).mockResolvedValue(ELIGIBLE_RESULT);

    renderPage();
    await waitFor(() => {
      const el = screen.getByLabelText(
        "Aplicar retención a este gasto",
      ) as HTMLInputElement;
      expect(el.disabled).toBe(false);
    });
    fireEvent.click(screen.getByLabelText("Aplicar retención a este gasto"));

    await waitFor(() =>
      expect(document.querySelectorAll("input.zh-numeric-input").length).toBeGreaterThan(0),
    );
    expect(document.querySelectorAll('input[type="number"]').length).toBe(0);
  });
});
