// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PriceListsPage } from "./PriceListsPage";
import { priceListService, type PriceListDto } from "../api/pricingService";
import { message } from "../../../lib/messages";

function renderPage() {
  return render(
    <I18nProvider>
      <PriceListsPage />
    </I18nProvider>,
  );
}

/**
 * CRITICAL-CONFIRMATIONS-SENSITIVE-CONFIG-06 — "Activar/desactivar lista de precios": el catch
 * vacío original se reemplaza por confirmación previa + formatApiRequestError. Confirma antes de
 * ejecutar, no llama al backend si se cancela, éxito muestra message.success, fallo muestra el
 * mensaje real y ya no queda silencioso. No cambia el pricing engine.
 */

vi.mock("../api/pricingService", async () => {
  const actual = await vi.importActual<typeof import("../api/pricingService")>(
    "../api/pricingService",
  );
  return {
    ...actual,
    priceListService: {
      list: vi.fn(),
      getById: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
      enable: vi.fn(),
      disable: vi.fn(),
      setDefault: vi.fn(),
      getAssignedItems: vi.fn(),
    },
  };
});

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const ACTIVE_PL: PriceListDto = {
  id: "pl-1",
  code: "DEFAULT",
  name: "Lista general",
  currencyCode: "USD",
  isDefault: true,
  validFrom: null,
  validUntil: null,
  ruleType: null,
  ruleValue: null,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: null,
};

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(priceListService.list).mockResolvedValue([ACTIVE_PL]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("PriceListsPage — activar/desactivar: confirmación y feedback", () => {
  it("pide confirmación antes de desactivar", async () => {
    vi.mocked(priceListService.disable).mockResolvedValue(true);
    renderPage();
    await waitFor(() => expect(screen.getByText("Lista general")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(priceListService.disable).toHaveBeenCalledWith("pl-1");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/dejará de estar disponible para nuevas ventas/i);
  });

  it("si se cancela, no llama a priceListService.disable", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    renderPage();
    await waitFor(() => expect(screen.getByText("Lista general")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(priceListService.disable).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success", async () => {
    vi.mocked(priceListService.disable).mockResolvedValue(true);
    renderPage();
    await waitFor(() => expect(screen.getByText("Lista general")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Lista de precios desactivada correctamente."),
    );
  });

  it("si falla, ya no queda silencioso: muestra el mensaje real del backend", async () => {
    vi.mocked(priceListService.disable).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "La lista de precios es la predeterminada de la empresa." } },
      },
    });
    renderPage();
    await waitFor(() => expect(screen.getByText("Lista general")).toBeTruthy());

    fireEvent.click(screen.getByTitle(/Desactivar/));

    await waitFor(() =>
      expect(message.error).toHaveBeenCalledWith(
        "La lista de precios es la predeterminada de la empresa.",
      ),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(priceListService.disable).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Lista general")).toBeTruthy());
    fireEvent.click(screen.getByTitle(/Desactivar/));
    await waitFor(() => expect(priceListService.disable).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
