// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { PaymentTermsPage } from "./PaymentTermsPage";
import { paymentTermService, type PaymentTermDto } from "../api/paymentTermService";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-CLEANUP-07 — residuo encontrado en el barrido: handleToggle tenía
 * catch vacío (`catch { /* *\/ }`) y no confirmaba antes de desactivar/activar un plazo de pago
 * (afecta operaciones a crédito). Se agrega confirmación previa + formatApiRequestError.
 */

vi.mock("../api/paymentTermService", () => ({
  paymentTermService: {
    list: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    enable: vi.fn(),
    disable: vi.fn(),
  },
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const ACTIVE_PT: PaymentTermDto = {
  id: "pt-1",
  code: "30D",
  name: "30 días",
  installments: 1,
  daysBetweenInstallments: 0,
  totalDays: 30,
  summary: "30 días",
  isActive: true,
};

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(paymentTermService.list).mockResolvedValue([ACTIVE_PT]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("PaymentTermsPage — activar/desactivar: confirmación y feedback (antes: catch vacío)", () => {
  it("pide confirmación antes de desactivar", async () => {
    vi.mocked(paymentTermService.disable).mockResolvedValue(true);
    render(<PaymentTermsPage />);
    await waitFor(() => expect(screen.getByText("30D")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Desactivar"));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(paymentTermService.disable).toHaveBeenCalledWith("pt-1");
    });
  });

  it("si se cancela, no llama a paymentTermService.disable", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    render(<PaymentTermsPage />);
    await waitFor(() => expect(screen.getByText("30D")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Desactivar"));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(paymentTermService.disable).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success", async () => {
    vi.mocked(paymentTermService.disable).mockResolvedValue(true);
    render(<PaymentTermsPage />);
    await waitFor(() => expect(screen.getByText("30D")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Desactivar"));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Plazo de pago desactivado correctamente."),
    );
  });

  it("si falla, ya no queda silencioso: muestra el mensaje real del backend", async () => {
    vi.mocked(paymentTermService.disable).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El plazo de pago está en uso." } },
      },
    });
    render(<PaymentTermsPage />);
    await waitFor(() => expect(screen.getByText("30D")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Desactivar"));

    await waitFor(() =>
      expect(message.error).toHaveBeenCalledWith("El plazo de pago está en uso."),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(paymentTermService.disable).mockResolvedValue(true);

    render(<PaymentTermsPage />);
    await waitFor(() => expect(screen.getByText("30D")).toBeTruthy());
    fireEvent.click(screen.getByTitle("Desactivar"));
    await waitFor(() => expect(paymentTermService.disable).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
