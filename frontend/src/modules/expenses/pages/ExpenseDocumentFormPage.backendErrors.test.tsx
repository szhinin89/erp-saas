// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AxiosError, AxiosHeaders } from "axios";
import { I18nProvider } from "../../../i18n/i18n";
import { ExpenseDocumentFormPage } from "./ExpenseDocumentFormPage";
import { expenseDocumentService } from "../api/expenseDocumentService";
import { expenseCategoryService } from "../api/expenseCategoryService";
import { accountLookupFacade } from "../../accounting/facades/accountLookupFacade";
import { paymentTermLookupFacade } from "../../masterData/facades/paymentTermLookupFacade";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import type {
  ExpenseDocumentDetailDto,
  RetentionEligibilityResult,
} from "../api/expenseDocumentService";
import type { ExpenseCategoryTreeNodeDto } from "../api/expenseCategoryService";
import type { AccountDto } from "../../accounting/facades/accountLookupFacade";

/**
 * EXPENSE-UX-01 (B2) — al guardar, los errores 422 del backend con campo/línea se muestran en el
 * campo y la línea correspondientes; lo que no se puede asociar sigue saliendo como mensaje general.
 */

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

function treeNode(
  id: string,
  level: "Type" | "Category" | "Subcategory",
  children: ExpenseCategoryTreeNodeDto[] = [],
  accountId: string | null = null,
): ExpenseCategoryTreeNodeDto {
  return {
    id,
    companyId: "company-1",
    parentId: null,
    code: id.toUpperCase(),
    name: `Nombre ${id}`,
    description: null,
    level,
    accountingAccountId: accountId,
    // EXPENSE-BACKEND-01: la cuenta destino viene en el árbol, no del plan de cuentas.
    accountingAccountCode: accountId ? "5.1.1" : null,
    accountingAccountName: accountId ? "Servicios" : null,
    isActive: true,
    children,
  };
}

const TREE = [
  treeNode("t1", "Type", [treeNode("c1", "Category", [treeNode("sub-1", "Subcategory", [], "acc-1")])]),
];

const DRAFT: ExpenseDocumentDetailDto = {
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

const NOT_ELIGIBLE: RetentionEligibilityResult = {
  canRetainVat: false,
  canRetainIncome: false,
  isSupplierExempt: false,
  hasRetainableBase: true,
  missingRetentionCode: false,
  isSupplierRequiredToKeepAccounting: false,
  candidates: [],
  reasons: [],
  isEligible: false,
};

function validation422(errors: unknown) {
  return new AxiosError("Request failed", "ERR_BAD_REQUEST", undefined, undefined, {
    status: 422,
    statusText: "Unprocessable Entity",
    headers: {},
    config: { headers: new AxiosHeaders() },
    data: { code: "VALIDATION_ERROR", data: { errors } },
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(accountLookupFacade.listAccounts).mockResolvedValue([
    { id: "acc-1", code: "5.1.1", name: "Servicios" } as AccountDto,
  ]);
  vi.mocked(paymentTermLookupFacade.list).mockResolvedValue([]);
  vi.mocked(expenseCategoryService.getTree).mockResolvedValue(TREE);
  vi.mocked(sriLookupFacade.taxSupportCodes).mockResolvedValue([]);
  vi.mocked(sriLookupFacade.vatRates).mockResolvedValue([
    { code: "2", name: "15% IVA", percentage: 15 },
  ]);
  vi.mocked(expenseDocumentService.getById).mockResolvedValue(DRAFT);
  vi.mocked(expenseDocumentService.getRetentionEligibility).mockResolvedValue(NOT_ELIGIBLE);
  vi.mocked(expenseDocumentService.getExpenseRetention).mockResolvedValue(null);
});

afterEach(cleanup);

async function openAndSave(error: unknown) {
  vi.mocked(expenseDocumentService.update).mockRejectedValue(error);
  render(
    <I18nProvider>
      <MemoryRouter>
        <ExpenseDocumentFormPage />
      </MemoryRouter>
    </I18nProvider>,
  );
  await waitFor(() => expect(screen.getByText("Proveedor Uno S.A.")).toBeTruthy());
  const save = screen.getByRole("button", { name: "Guardar borrador" }) as HTMLButtonElement;
  await waitFor(() => expect(save.disabled).toBe(false));
  fireEvent.click(save);
  await waitFor(() => expect(expenseDocumentService.update).toHaveBeenCalled());
  await waitFor(() => expect(vi.mocked(message.error)).toHaveBeenCalled());
}

describe("ExpenseDocumentFormPage — cuenta destino sin accounting.view (EXPENSE-BACKEND-01)", () => {
  it("abre y permite guardar sin consultar el plan de cuentas; la cuenta sale del árbol de categorías", async () => {
    vi.mocked(expenseDocumentService.update).mockResolvedValue(DRAFT);
    render(
      <I18nProvider>
        <MemoryRouter>
          <ExpenseDocumentFormPage />
        </MemoryRouter>
      </I18nProvider>,
    );
    await waitFor(() => expect(screen.getByText("Proveedor Uno S.A.")).toBeTruthy());
    const save = screen.getByRole("button", { name: "Guardar borrador" }) as HTMLButtonElement;
    await waitFor(() => expect(save.disabled).toBe(false));

    expect(screen.getByText("5.1.1 - Servicios")).toBeTruthy();
    expect(accountLookupFacade.listAccounts).not.toHaveBeenCalled();

    fireEvent.click(save);
    await waitFor(() => expect(expenseDocumentService.update).toHaveBeenCalled());
  });

  it("una subcategoría sin cuenta resuelta no habilita el guardado (validación contable vigente)", async () => {
    vi.mocked(expenseCategoryService.getTree).mockResolvedValue([
      treeNode("t1", "Type", [
        treeNode("c1", "Category", [
          { ...treeNode("sub-1", "Subcategory", [], "acc-1"), accountingAccountCode: null, accountingAccountName: null },
        ]),
      ]),
    ]);
    render(
      <I18nProvider>
        <MemoryRouter>
          <ExpenseDocumentFormPage />
        </MemoryRouter>
      </I18nProvider>,
    );
    await waitFor(() => expect(screen.getByText("Proveedor Uno S.A.")).toBeTruthy());

    await waitFor(() =>
      expect(screen.getByText("No hay subcategorías de gasto listas para usar.")).toBeTruthy(),
    );
    expect((screen.getByRole("button", { name: "Guardar borrador" }) as HTMLButtonElement).disabled).toBe(true);
  });
});

describe("ExpenseDocumentFormPage — errores del backend al guardar (B2)", () => {
  it("muestra errores de campo y de línea en su sitio y un mensaje que remite a los campos marcados", async () => {
    await openAndSave(
      validation422({
        documentNumber: ["Ya existe un gasto con este numero."],
        "lines[0].Quantity": ["La cantidad debe ser mayor a cero."],
      }),
    );

    expect(screen.getByText("Ya existe un gasto con este numero.")).toBeTruthy();
    expect(screen.getByText("La cantidad debe ser mayor a cero.")).toBeTruthy();
    expect(screen.getByLabelText(/^Cantidad/).getAttribute("aria-invalid")).toBe("true");
    expect(vi.mocked(message.error)).toHaveBeenCalledWith(
      "Revise los campos marcados en el formulario.",
    );
  });

  it("un error de regla de línea sin propiedad sale como mensaje general con el número de línea", async () => {
    await openAndSave(
      validation422({ "lines[0]": ["El descuento no puede superar el subtotal de la linea."] }),
    );

    expect(vi.mocked(message.error)).toHaveBeenCalledWith(
      "Línea 1: El descuento no puede superar el subtotal de la linea.",
    );
  });

  it("un error de negocio sin campo (lista plana) conserva el mensaje general del servidor", async () => {
    await openAndSave(validation422(["La subcategoria 'Nombre sub-1' esta inactiva."]));

    expect(vi.mocked(message.error)).toHaveBeenCalledWith(
      "La subcategoria 'Nombre sub-1' esta inactiva.",
    );
    expect(screen.getByLabelText(/^Cantidad/).getAttribute("aria-invalid")).not.toBe("true");
  });

  it("un fallo sin mapa de validación usa el mensaje general de respaldo", async () => {
    await openAndSave(new Error("boom"));

    expect(vi.mocked(message.error)).toHaveBeenCalledWith("boom");
  });
});
