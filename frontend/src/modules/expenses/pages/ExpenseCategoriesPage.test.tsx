// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup } from "@testing-library/react";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { ExpenseCategoriesPage } from "./ExpenseCategoriesPage";
import { expenseCategoryService } from "../api/expenseCategoryService";
import { accountingApi } from "../../accounting/api/accountingApi";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-PURCHASES-EXPENSES-03 — cubre "Activar/desactivar categoría de gastos":
 * confirma antes de ejecutar (mensaje distinto por dirección), no llama al backend si se cancela,
 * éxito muestra message.success, fallo muestra el mensaje real vía formatApiRequestError.
 */

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
  accountingApi: {
    listAccounts: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const ACTIVE_NODE = {
  id: "node-1",
  companyId: "company-1",
  parentId: null,
  level: "Type" as const,
  code: "T01",
  name: "Servicios básicos",
  description: null,
  accountingAccountId: null,
  isActive: true,
  children: [],
};

const INACTIVE_NODE = {
  ...ACTIVE_NODE,
  id: "node-2",
  code: "T02",
  name: "Viáticos",
  isActive: false,
};

function renderPage() {
  return render(<ExpenseCategoriesPage />);
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(accountingApi.listAccounts).mockResolvedValue([]);
  vi.mocked(expenseCategoryService.getTree).mockResolvedValue([
    ACTIVE_NODE,
    INACTIVE_NODE,
  ]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => {
  cleanup();
});

describe("ExpenseCategoriesPage — activar/desactivar: confirmación y feedback", () => {
  it("desactivar pide confirmación explicando que no elimina el histórico, y llama a deactivate al confirmar", async () => {
    vi.mocked(expenseCategoryService.deactivate).mockResolvedValue({ ...ACTIVE_NODE });

    renderPage();
    await waitFor(() => expect(screen.getByText("Servicios básicos")).toBeTruthy());

    fireEvent.click(screen.getByLabelText("Desactivar Servicios básicos"));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(expenseCategoryService.deactivate).toHaveBeenCalledWith("node-1");
    });

    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/dejará de estar disponible/i);
    expect(String(options.message)).toMatch(/no se modifican|no.*elimina|históric/i);
    expect(message.success).toHaveBeenCalledWith("Nodo desactivado correctamente.");
  });

  it("activar pide confirmación explicando que vuelve a estar disponible, y llama a activate al confirmar", async () => {
    vi.mocked(expenseCategoryService.activate).mockResolvedValue({ ...ACTIVE_NODE });

    renderPage();
    await waitFor(() => expect(screen.getByText("Viáticos")).toBeTruthy());

    fireEvent.click(screen.getByLabelText("Activar Viáticos"));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(expenseCategoryService.activate).toHaveBeenCalledWith("node-2");
    });

    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/volverá a estar disponible/i);
    expect(message.success).toHaveBeenCalledWith("Nodo activado correctamente.");
  });

  it("si se cancela la confirmación, no llama a deactivate/activate", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);

    renderPage();
    await waitFor(() => expect(screen.getByText("Servicios básicos")).toBeTruthy());

    fireEvent.click(screen.getByLabelText("Desactivar Servicios básicos"));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(expenseCategoryService.deactivate).not.toHaveBeenCalled();
    expect(expenseCategoryService.activate).not.toHaveBeenCalled();
  });

  it("si el backend falla, muestra el mensaje real y no llama message.success", async () => {
    vi.mocked(expenseCategoryService.deactivate).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El nodo tiene gastos registrados este mes." } },
      },
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Servicios básicos")).toBeTruthy());

    fireEvent.click(screen.getByLabelText("Desactivar Servicios básicos"));

    await waitFor(() => {
      expect(message.error).toHaveBeenCalledWith(
        "El nodo tiene gastos registrados este mes.",
      );
    });
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(expenseCategoryService.deactivate).mockResolvedValue({ ...ACTIVE_NODE });

    renderPage();
    await waitFor(() => expect(screen.getByText("Servicios básicos")).toBeTruthy());
    fireEvent.click(screen.getByLabelText("Desactivar Servicios básicos"));
    await waitFor(() => expect(expenseCategoryService.deactivate).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
