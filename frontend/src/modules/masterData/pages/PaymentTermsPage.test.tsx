// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PaymentTermsPage } from "./PaymentTermsPage";
import { paymentTermService, type PaymentTermDto } from "../api/paymentTermService";
import { message } from "../../../lib/messages";

function renderPage() {
  return render(
    <I18nProvider>
      <PaymentTermsPage />
    </I18nProvider>,
  );
}

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
    renderPage();
    await waitFor(() => expect(screen.getByText("30D")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(paymentTermService.disable).toHaveBeenCalledWith("pt-1");
    });
  });

  it("si se cancela, no llama a paymentTermService.disable", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    renderPage();
    await waitFor(() => expect(screen.getByText("30D")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(paymentTermService.disable).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success", async () => {
    vi.mocked(paymentTermService.disable).mockResolvedValue(true);
    renderPage();
    await waitFor(() => expect(screen.getByText("30D")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

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
    renderPage();
    await waitFor(() => expect(screen.getByText("30D")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

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

    renderPage();
    await waitFor(() => expect(screen.getByText("30D")).toBeTruthy());
    fireEvent.click(screen.getByTitle(/Desactivar/));
    await waitFor(() => expect(paymentTermService.disable).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });

  describe("campos numéricos (ZhNumberInput)", () => {
    const openNew = async () => {
      renderPage();
      await waitFor(() => expect(screen.getByText("30D")).toBeTruthy());
      fireEvent.click(screen.getByRole("tab", { name: /Nueva Condición/ }));
    };

    it("cuotas y días son ZhNumberInput (texto numérico), sin input type=number", async () => {
      await openNew();
      const installments = screen.getByLabelText(/Número de cuotas/) as HTMLInputElement;
      const days = screen.getByLabelText(/Días entre cuotas/) as HTMLInputElement;
      for (const input of [installments, days]) {
        expect(input.type).toBe("text");
        expect(input.inputMode).toBe("numeric");
        expect(input.classList.contains("zh-numeric-input")).toBe(true);
      }
      expect(installments.value).toBe("1");
      expect(days.value).toBe("0");
      expect(document.querySelector('input[type="number"]')).toBeNull();
    });

    it("bloquea teclas no numéricas y el signo menos", async () => {
      await openNew();
      const installments = screen.getByLabelText(/Número de cuotas/);
      expect(fireEvent.keyDown(installments, { key: "a" })).toBe(false);
      expect(fireEvent.keyDown(installments, { key: "-" })).toBe(false);
      expect(fireEvent.keyDown(installments, { key: "." })).toBe(false);
      expect(fireEvent.keyDown(installments, { key: "5" })).toBe(true);
    });

    it("transmite cuotas y días numéricos en el payload de creación (sin clamp)", async () => {
      vi.mocked(paymentTermService.create).mockResolvedValue(ACTIVE_PT);
      await openNew();
      fireEvent.change(screen.getByPlaceholderText("CONTADO, NET30, 3X30"), { target: { value: "3X30" } });
      fireEvent.change(screen.getByPlaceholderText("Contado / Crédito 30 días"), { target: { value: "Tres cuotas" } });
      fireEvent.change(screen.getByLabelText(/Número de cuotas/), { target: { value: "3" } });
      fireEvent.change(screen.getByLabelText(/Días entre cuotas/), { target: { value: "30" } });
      fireEvent.click(screen.getByRole("button", { name: /Crear/ }));
      await waitFor(() => expect(paymentTermService.create).toHaveBeenCalled());
      expect(paymentTermService.create).toHaveBeenCalledWith({
        code: "3X30",
        name: "Tres cuotas",
        installments: 3,
        daysBetweenInstallments: 30,
      });
    });

    it("conserva la regla existente: Guardar se deshabilita con cuotas < 1", async () => {
      await openNew();
      fireEvent.change(screen.getByPlaceholderText("CONTADO, NET30, 3X30"), { target: { value: "X" } });
      fireEvent.change(screen.getByPlaceholderText("Contado / Crédito 30 días"), { target: { value: "N" } });
      const save = screen.getByRole("button", { name: /Crear/ }) as HTMLButtonElement;
      expect(save.disabled).toBe(false);
      fireEvent.change(screen.getByLabelText(/Número de cuotas/), { target: { value: "0" } });
      expect(save.disabled).toBe(true);
    });
  });
});
