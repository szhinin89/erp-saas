// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ChartOfAccountsPage } from "./ChartOfAccountsPage";
import { accountingApi } from "../api/accountingApi";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-INVENTORY-ACCOUNTING-05 — "Activar/desactivar cuenta contable":
 * confirma antes de ejecutar, no llama al backend si se cancela, éxito muestra message.success,
 * fallo muestra el mensaje real vía formatApiRequestError. No cambia validaciones/lógica contable.
 */

vi.mock("../api/accountingApi", () => ({
  accountingApi: {
    listAccounts: vi.fn(),
    createAccount: vi.fn(),
    updateAccount: vi.fn(),
    disableAccount: vi.fn(),
    enableAccount: vi.fn(),
  },
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const ACTIVE_ACCOUNT = {
  id: "acc-1",
  code: "1.1.01",
  name: "Caja general",
  parentAccountId: null,
  parentAccountCode: null,
  parentAccountName: null,
  level: 0,
  accountType: "Asset",
  nature: "Debit",
  allowsPosting: true,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: null,
};

function renderPage() {
  return render(
    <I18nProvider>
      <ChartOfAccountsPage />
    </I18nProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(accountingApi.listAccounts).mockResolvedValue([ACTIVE_ACCOUNT]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => cleanup());

describe("ChartOfAccountsPage — activar/desactivar cuenta: confirmación y feedback", () => {
  it("ordena por código contable y renderiza nombres limpios con guías visuales por código", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue([
      {
        ...ACTIVE_ACCOUNT,
        id: "acc-5",
        code: "1.1.01.002",
        name: "L Caja chica",
        level: 1,
      },
      {
        ...ACTIVE_ACCOUNT,
        id: "acc-3",
        code: "1.1.01",
        name: "Efectivo y equivalentes",
        level: 0,
        allowsPosting: false,
      },
      {
        ...ACTIVE_ACCOUNT,
        id: "acc-1",
        code: "1",
        name: "Activo",
        level: 0,
        allowsPosting: false,
      },
      {
        ...ACTIVE_ACCOUNT,
        id: "acc-2",
        code: "1.1",
        name: "Activo corriente",
        level: 0,
        allowsPosting: false,
      },
      {
        ...ACTIVE_ACCOUNT,
        id: "acc-4",
        code: "1.1.01.001",
        name: "Caja General",
        level: 1,
      },
      {
        ...ACTIVE_ACCOUNT,
        id: "acc-6",
        code: "1.1.02",
        name: "│ ├ Bancos",
        level: 0,
        allowsPosting: false,
      },
    ]);

    const { container } = renderPage();
    await waitFor(() => expect(screen.getByText("Caja chica")).toBeTruthy());

    const compactFilterBar = container.querySelector(".coa-list-filters");
    expect(compactFilterBar).toBeTruthy();
    expect(screen.getByPlaceholderText("Buscar por código o nombre...")).toBeTruthy();
    expect(screen.getByText("Todos los tipos")).toBeTruthy();
    expect(screen.getByText("Todos los estados")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Actualizar" })).toBeTruthy();

    const rows = Array.from(container.querySelectorAll("tbody tr"));
    expect(rows.map((row) => row.children[0]?.textContent)).toEqual([
      "1",
      "1.1",
      "1.1.01",
      "1.1.01.001",
      "1.1.01.002",
      "1.1.02",
    ]);
    expect(screen.queryByText("L Caja chica")).toBeNull();
    expect(screen.queryByText(/[\u2502\u2514\u251c]/)).toBeNull();

    const nameCells = rows.map((row) => row.children[1]?.firstElementChild as HTMLElement);
    expect(nameCells.map((cell) => cell.textContent)).toEqual([
      "Activo",
      "Activo corriente",
      "Efectivo y equivalentes",
      "Caja General",
      "Caja chica",
      "Bancos",
    ]);
    expect(nameCells.map((cell) => cell.dataset.depth)).toEqual(["0", "1", "2", "3", "3", "2"]);
    expect(nameCells[0].querySelector(".coa-tree-name__guides")).toBeNull();

    const depthOneGuides = nameCells[1].querySelector(".coa-tree-name__guides");
    const depthThreeGuides = nameCells[3].querySelector(".coa-tree-name__guides");
    expect(depthOneGuides?.getAttribute("aria-hidden")).toBe("true");
    expect(depthOneGuides?.querySelectorAll(".coa-tree-name__guide")).toHaveLength(1);
    expect(depthThreeGuides?.getAttribute("aria-hidden")).toBe("true");
    expect(depthThreeGuides?.querySelectorAll(".coa-tree-name__guide")).toHaveLength(3);
    expect(depthThreeGuides?.textContent).toBe("");

    rows.forEach((row) => {
      expect(row.querySelector('button[aria-label="Editar"]')).toBeTruthy();
      expect(row.querySelector('button[aria-label="Desactivar"]')).toBeTruthy();
    });
  });

  it("pide confirmación antes de desactivar, explicando el impacto en asientos/reglas de posteo", async () => {
    vi.mocked(accountingApi.disableAccount).mockResolvedValue(ACTIVE_ACCOUNT);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(accountingApi.disableAccount).toHaveBeenCalledWith("acc-1");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/no podrá usarse para nuevos asientos/i);
    expect(String(options.message)).toMatch(/no se eliminan/i);
    expect(String(options.message)).toMatch(/reglas contables/i);
  });

  it("si se cancela, no llama a disableAccount", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(accountingApi.disableAccount).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success", async () => {
    vi.mocked(accountingApi.disableAccount).mockResolvedValue(ACTIVE_ACCOUNT);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Cuenta desactivada correctamente."),
    );
  });

  it("si falla, muestra el mensaje real del backend y no muestra éxito", async () => {
    vi.mocked(accountingApi.disableAccount).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "La cuenta tiene reglas de posteo activas." } },
      },
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() =>
      expect(message.error).toHaveBeenCalledWith(
        "La cuenta tiene reglas de posteo activas.",
      ),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("activar explica que vuelve a estar disponible para uso contable", async () => {
    vi.mocked(accountingApi.listAccounts).mockResolvedValue([
      { ...ACTIVE_ACCOUNT, isActive: false },
    ]);
    vi.mocked(accountingApi.enableAccount).mockResolvedValue(ACTIVE_ACCOUNT);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Activar" }));

    await waitFor(() => expect(accountingApi.enableAccount).toHaveBeenCalledWith("acc-1"));
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/volverá a estar disponible para uso contable/i);
    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Cuenta activada correctamente."),
    );
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(accountingApi.disableAccount).mockResolvedValue(ACTIVE_ACCOUNT);

    renderPage();
    await waitFor(() => expect(screen.getByText("Caja general")).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));
    await waitFor(() => expect(accountingApi.disableAccount).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
