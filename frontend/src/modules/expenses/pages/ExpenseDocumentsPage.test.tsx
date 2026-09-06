// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { expenseDocumentService } from "../api/expenseDocumentService";
import { message } from "../../../lib/messages";
import { ExpenseDocumentsPage } from "./ExpenseDocumentsPage";

/**
 * ZH-AUTH-BRANCH-CONTEXT-EXPENSES-AUDIT-12 — cubre el comportamiento de la pantalla ante
 * BRANCH_SCOPE_FORBIDDEN: nunca debe quedarse en loading, y debe mostrar un mensaje claro en
 * vez de un error seco. La limpieza real de activeBranchStore (que reabre el selector de
 * sucursal vía useBranchGate/AppLayout) se prueba por separado en
 * modules/lib/api.test.ts — este archivo cubre solo el comportamiento propio de la página.
 */

vi.mock("../api/expenseDocumentService", () => ({
  expenseDocumentService: {
    list: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <ExpenseDocumentsPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

const LIST_ITEM = {
  id: "doc-1",
  companyId: "company-1",
  branchId: "branch-1",
  supplierId: "sup-1",
  supplierName: "Proveedor Uno",
  supplierTaxId: "0999999999001",
  issueDate: "2026-08-01T00:00:00Z",
  accountingDate: "2026-08-01T00:00:00Z",
  documentType: "Factura",
  documentNumber: "001-001-000000123",
  dueDate: null,
  status: "Confirmed" as const,
  lineCount: 2,
  subtotal: 100,
  totalDiscount: 0,
  totalTax: 12,
  grandTotal: 112,
  createdAt: "2026-08-01T00:00:00Z",
};

describe("ExpenseDocumentsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(usePermissionsUi).mockReturnValue({
      has: (key: string) => key === "expenses.documents.view",
      canShow: (key: string) => key === "expenses.documents.view",
      isAdminRole: false,
    });
  });

  afterEach(() => {
    cleanup();
  });

  it("carga y muestra los documentos cuando el backend responde correctamente", async () => {
    vi.mocked(expenseDocumentService.list).mockResolvedValue({
      items: [LIST_ITEM],
      total: 1,
      page: 1,
      pageSize: 25,
    });

    renderPage();

    expect(await screen.findByText("001-001-000000123")).toBeTruthy();
    expect(expenseDocumentService.list).toHaveBeenCalledTimes(1);
  });

  it("no se queda en loading y muestra un mensaje claro si el backend rechaza por BRANCH_SCOPE_FORBIDDEN", async () => {
    vi.mocked(expenseDocumentService.list).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 403,
        data: {
          code: "BRANCH_SCOPE_FORBIDDEN",
          data: { errors: ["No tiene autorización para operar en esta sucursal."] },
          message: {
            user: "Acceso denegado por contexto de sucursal.",
            dev: "Branch scope exception.",
          },
        },
      },
    });

    renderPage();

    expect(
      await screen.findByText("No tiene autorización para operar en esta sucursal."),
    ).toBeTruthy();
    expect(message.error).toHaveBeenCalledTimes(1);
    // No queda spinner/tabla en estado de carga: EmptyState con el mensaje reemplaza la tabla.
    expect(screen.queryByRole("table")).toBeNull();
  });

  it("no dispara un segundo fetch automático tras el error (sin loop)", async () => {
    vi.mocked(expenseDocumentService.list).mockRejectedValue({
      isAxiosError: true,
      response: { status: 403, data: { code: "BRANCH_SCOPE_FORBIDDEN" } },
    });

    renderPage();

    await waitFor(() => expect(expenseDocumentService.list).toHaveBeenCalledTimes(1));
    // Da tiempo a que cualquier efecto adicional (si existiera) dispare un segundo fetch.
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(expenseDocumentService.list).toHaveBeenCalledTimes(1);
  });

  it("no consulta expenses/documents si el usuario no tiene permiso de vista", () => {
    vi.mocked(usePermissionsUi).mockReturnValue({
      has: () => false,
      canShow: () => false,
      isAdminRole: false,
    });

    renderPage();

    expect(expenseDocumentService.list).not.toHaveBeenCalled();
  });
});
