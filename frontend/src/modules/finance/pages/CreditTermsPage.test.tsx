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

  describe("campos numéricos (ZhNumberInput)", () => {
    const FIN_CT = {
      ...ACTIVE_CT,
      mode: "2", // parseInt("2") → Financiero Estricto (muestra cuotas)
      totalDays: 60,
      installments: [{ installmentNumber: 1, daysOffset: 5, percentage: 100 }],
    } as unknown as CreditTermDto;

    const fieldInput = (label: string) => {
      const field = screen.getByText(label).closest(".zh-field") as HTMLElement;
      return field.querySelector("input") as HTMLInputElement;
    };

    const openEdit = async () => {
      vi.mocked(creditTermService.list).mockResolvedValue([FIN_CT]);
      renderPage();
      await waitFor(() => expect(screen.getByText("30D")).toBeTruthy());
      fireEvent.click(screen.getByTitle(/Editar condición de crédito 30D/));
    };

    it("plazo total y días offset son ZhNumberInput; offset conserva su clase de ancho", async () => {
      await openEdit();
      const total = fieldInput("Plazo total (días)");
      const offset = fieldInput("Días offset");
      for (const input of [total, offset]) {
        expect(input.type).toBe("text");
        expect(input.inputMode).toBe("numeric");
        expect(input.classList.contains("zh-numeric-input")).toBe(true);
      }
      expect(offset.classList.contains("ct-installment-days-input")).toBe(true);
      expect(total.value).toBe("60");
      expect(offset.value).toBe("5");
      expect(document.querySelector('input[type="number"]')).toBeNull();
    });

    it("bloquea teclas no numéricas y el signo menos", async () => {
      await openEdit();
      const total = fieldInput("Plazo total (días)");
      expect(fireEvent.keyDown(total, { key: "e" })).toBe(false);
      expect(fireEvent.keyDown(total, { key: "-" })).toBe(false);
      expect(fireEvent.keyDown(total, { key: "7" })).toBe(true);
    });

    it("transmite totalDays y daysOffset numéricos en el payload de actualización (sin clamp)", async () => {
      vi.mocked(creditTermService.update).mockResolvedValue(FIN_CT);
      await openEdit();
      fireEvent.change(fieldInput("Plazo total (días)"), { target: { value: "90" } });
      // Valor mayor que el plazo total: no se aplica ningún límite en la UI (el backend valida).
      fireEvent.change(fieldInput("Días offset"), { target: { value: "120" } });
      fireEvent.click(screen.getByRole("button", { name: /Actualizar/ }));
      await waitFor(() => expect(creditTermService.update).toHaveBeenCalled());
      expect(creditTermService.update).toHaveBeenCalledWith(
        "ct-1",
        expect.objectContaining({
          totalDays: 90,
          installments: [{ number: 1, daysOffset: 120, percentage: 100 }],
        }),
      );
    });
  });
});
