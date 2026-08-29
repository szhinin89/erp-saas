// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { CarriersPage } from "./CarriersPage";
import { I18nProvider } from "../../../../i18n/i18n";
import { carrierService, type Carrier } from "../api/carrierService";
import { message } from "../../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-CLEANUP-07 — residuo encontrado en el barrido: activar/desactivar un
 * transportista no pedía confirmación previa ni mostraba message.success (el hook ya exponía el
 * error real vía formatApiRequestError/saveError, pero sin confirmación ni éxito visible). No
 * cambia el payload de carrierService.enable/disable.
 */

vi.mock("../api/carrierService", () => ({
  carrierService: {
    getAll: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    enable: vi.fn(),
    disable: vi.fn(),
  },
}));

vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => true }),
}));

vi.mock("../../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const ACTIVE_CARRIER: Carrier = {
  id: "carrier-1",
  identificationType: "RUC",
  identificationNumber: "1790000000001",
  legalName: "Transportes Uno",
  licensePlate: "ABC-1234",
  phone: null,
  email: null,
  isActive: true,
};

function renderPage() {
  return render(
    <I18nProvider>
      <CarriersPage />
    </I18nProvider>,
  );
}

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(carrierService.getAll).mockResolvedValue([ACTIVE_CARRIER]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("CarriersPage — activar/desactivar: confirmación y feedback", () => {
  it("pide confirmación antes de desactivar", async () => {
    vi.mocked(carrierService.disable).mockResolvedValue(ACTIVE_CARRIER);
    renderPage();
    await waitFor(() => expect(screen.getByText("Transportes Uno")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Disable"));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(carrierService.disable).toHaveBeenCalledWith("carrier-1");
    });
  });

  it("si se cancela, no llama a carrierService.disable", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    renderPage();
    await waitFor(() => expect(screen.getByText("Transportes Uno")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Disable"));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(carrierService.disable).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success", async () => {
    vi.mocked(carrierService.disable).mockResolvedValue(ACTIVE_CARRIER);
    renderPage();
    await waitFor(() => expect(screen.getByText("Transportes Uno")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Disable"));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Transportista desactivado correctamente."),
    );
  });

  it("si falla, no muestra éxito (el error real ya se mostraba vía saveError)", async () => {
    vi.mocked(carrierService.disable).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El transportista tiene despachos activos." } },
      },
    });
    renderPage();
    await waitFor(() => expect(screen.getByText("Transportes Uno")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Disable"));

    await waitFor(() =>
      expect(screen.getByText("El transportista tiene despachos activos.")).toBeTruthy(),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(carrierService.disable).mockResolvedValue(ACTIVE_CARRIER);

    renderPage();
    await waitFor(() => expect(screen.getByText("Transportes Uno")).toBeTruthy());
    fireEvent.click(screen.getByTitle("Disable"));
    await waitFor(() => expect(carrierService.disable).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
