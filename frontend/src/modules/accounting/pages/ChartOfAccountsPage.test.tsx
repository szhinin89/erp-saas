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
