// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PaymentMethodsPage } from "./PaymentMethodsPage";
import { paymentMethodService, type PaymentMethodDto } from "../api/paymentMethodService";
import { message } from "../../../lib/messages";

function renderPage() {
  return render(
    <I18nProvider>
      <PaymentMethodsPage />
    </I18nProvider>,
  );
}

/**
 * CRITICAL-CONFIRMATIONS-SENSITIVE-CONFIG-06 — "Activar/desactivar método de pago": el catch
 * vacío original se reemplaza por confirmación previa + formatApiRequestError. Confirma antes de
 * ejecutar, no llama al backend si se cancela, éxito muestra message.success, fallo muestra el
 * mensaje real y ya no queda silencioso.
 */

vi.mock("../api/paymentMethodService", () => ({
  paymentMethodService: {
    list: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    toggle: vi.fn(),
  },
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const ACTIVE_PM: PaymentMethodDto = {
  id: "pm-1",
  code: "EFECTIVO",
  name: "Efectivo",
  isActive: true,
  requiresReference: false,
  isCreditAllowed: false,
  sortOrder: 1,
  detailType: "None",
};

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(paymentMethodService.list).mockResolvedValue([ACTIVE_PM]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("PaymentMethodsPage — activar/desactivar: confirmación y feedback", () => {
  it("pide confirmación antes de desactivar", async () => {
    vi.mocked(paymentMethodService.toggle).mockResolvedValue({ ...ACTIVE_PM, isActive: false });
    renderPage();
    await waitFor(() => expect(screen.getByText("Efectivo")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(paymentMethodService.toggle).toHaveBeenCalledWith("pm-1");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/dejará de estar disponible/i);
  });

  it("si se cancela, no llama a paymentMethodService.toggle", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    renderPage();
    await waitFor(() => expect(screen.getByText("Efectivo")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(paymentMethodService.toggle).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success", async () => {
    vi.mocked(paymentMethodService.toggle).mockResolvedValue({ ...ACTIVE_PM, isActive: false });
    renderPage();
    await waitFor(() => expect(screen.getByText("Efectivo")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Método de pago desactivado correctamente."),
    );
  });

  it("si falla, ya no queda silencioso: muestra el mensaje real del backend", async () => {
    vi.mocked(paymentMethodService.toggle).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El método de pago está en uso en ventas activas." } },
      },
    });
    renderPage();
    await waitFor(() => expect(screen.getByText("Efectivo")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() =>
      expect(message.error).toHaveBeenCalledWith("El método de pago está en uso en ventas activas."),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(paymentMethodService.toggle).mockResolvedValue({ ...ACTIVE_PM, isActive: false });

    renderPage();
    await waitFor(() => expect(screen.getByText("Efectivo")).toBeTruthy());
    fireEvent.click(screen.getByTitle(/Desactivar/));
    await waitFor(() => expect(paymentMethodService.toggle).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
