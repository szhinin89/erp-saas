// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { CreditTermsPage } from "./CreditTermsPage";
import { creditTermService, type CreditTermDto } from "../api/creditTermService";
import { message } from "../../../lib/messages";

function renderPage() {
  return render(
    <I18nProvider>
      <CreditTermsPage />
    </I18nProvider>,
  );
}

/**
 * CRITICAL-CONFIRMATIONS-CLEANUP-07 — residuo encontrado en el barrido: handleToggle tenía
 * catch vacio y no confirmaba antes de desactivar/activar un plazo de credito. Se agrega
 * confirmacion previa + formatApiRequestError.
 */

vi.mock("../api/creditTermService", () => ({
  creditTermService: {
    list: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    enable: vi.fn(),
    disable: vi.fn(),
  },
  CREDIT_TERM_MODES: [
    { value: "Installments", label: "Cuotas" },
    { value: "SingleDue", label: "Vencimiento único" },
  ],
  creditTermModeName: (mode: string) => mode,
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const ACTIVE_CT: CreditTermDto = {
  id: "ct-1",
  code: "30D",
  name: "Crédito 30 días",
  mode: "SingleDue",
  totalDays: 30,
  isActive: true,
  installments: [],
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: null,
};

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(creditTermService.list).mockResolvedValue([ACTIVE_CT]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("CreditTermsPage — activar/desactivar: confirmación y feedback (antes: catch vacío)", () => {
  it("pide confirmación antes de desactivar", async () => {
    vi.mocked(creditTermService.disable).mockResolvedValue(true);
    renderPage();
    await waitFor(() => expect(screen.getByText("Crédito 30 días")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(creditTermService.disable).toHaveBeenCalledWith("ct-1");
    });
  });

  it("si se cancela, no llama a creditTermService.disable", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    renderPage();
    await waitFor(() => expect(screen.getByText("Crédito 30 días")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(creditTermService.disable).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success", async () => {
    vi.mocked(creditTermService.disable).mockResolvedValue(true);
    renderPage();
    await waitFor(() => expect(screen.getByText("Crédito 30 días")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Plazo de crédito desactivado correctamente."),
    );
  });

  it("si falla, ya no queda silencioso: muestra el mensaje real del backend", async () => {
    vi.mocked(creditTermService.disable).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El plazo de crédito está en uso." } },
      },
    });
    renderPage();
    await waitFor(() => expect(screen.getByText("Crédito 30 días")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() =>
      expect(message.error).toHaveBeenCalledWith("El plazo de crédito está en uso."),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(creditTermService.disable).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Crédito 30 días")).toBeTruthy());
    fireEvent.click(screen.getByTitle(/Desactivar/));
    await waitFor(() => expect(creditTermService.disable).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
