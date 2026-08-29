// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataSuppliersPage } from "./MasterDataSuppliersPage";
import { useMasterDataSuppliersUiStore } from "../store/masterDataPartnerUiStore";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";

/**
 * CRITICAL-CONFIRMATIONS-BUSINESS-PARTNERS-04 — "Activar/desactivar proveedor" sigue el mismo
 * estándar que cliente (mismo bug de falso éxito, misma corrección: el hook relanza el error).
 */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    searchBusinessPartnersPaged: vi.fn(),
    deactivateBusinessPartner: vi.fn(),
    activateBusinessPartner: vi.fn(),
    assignRole: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
    confirm: vi.fn(),
  },
}));

const SUPPLIER: BusinessPartnerSummaryDto = {
  id: "bp-2",
  identificationType: "04",
  identificationNumber: "0999999999001",
  legalName: "Proveedor Uno",
  tradeName: null,
  legalEntityTypeCode: 2,
  countryCode: "EC",
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
};

function renderPage() {
  const utils = render(
    <I18nProvider>
      <MemoryRouter>
        <MasterDataSuppliersPage />
      </MemoryRouter>
    </I18nProvider>,
  );
  fireEvent.click(screen.getByRole("tab", { name: /Listado/i }));
  return utils;
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(businessPartnerFacade.searchBusinessPartnersPaged).mockResolvedValue({
    items: [SUPPLIER],
    totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
  });
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => {
  cleanup();
  useMasterDataSuppliersUiStore.setState({
    activeTab: "resumen",
    editingPartner: null,
    recentActivity: [],
  });
});

describe("MasterDataSuppliersPage — desactivar proveedor: confirmación y feedback", () => {
  it("pide confirmación antes de desactivar, explicando que no borra histórico", async () => {
    vi.mocked(businessPartnerFacade.deactivateBusinessPartner).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar proveedor" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(businessPartnerFacade.deactivateBusinessPartner).toHaveBeenCalledWith("bp-2");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/dejará de estar disponible/i);
    expect(String(options.message)).toMatch(/no se elimina/i);
  });

  it("si se cancela, no llama al backend", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar proveedor" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(businessPartnerFacade.deactivateBusinessPartner).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success (no message.info)", async () => {
    vi.mocked(businessPartnerFacade.deactivateBusinessPartner).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar proveedor" }));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Proveedor desactivado correctamente."),
    );
    expect(message.info).not.toHaveBeenCalled();
  });

  it("REGRESIÓN falso-éxito: si el backend falla, NO muestra éxito y sí muestra el error real", async () => {
    vi.mocked(businessPartnerFacade.deactivateBusinessPartner).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El proveedor tiene compras pendientes." } },
      },
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Desactivar proveedor" }));

    await waitFor(() => {
      expect(message.error).toHaveBeenCalledWith("El proveedor tiene compras pendientes.");
    });
    expect(message.success).not.toHaveBeenCalled();
    expect(message.info).not.toHaveBeenCalled();
  });
});

describe("MasterDataSuppliersPage — activar proveedor: confirmación y feedback", () => {
  const INACTIVE_SUPPLIER: BusinessPartnerSummaryDto = { ...SUPPLIER, isActive: false };

  it("pide confirmación antes de activar y muestra success al confirmar", async () => {
    vi.mocked(businessPartnerFacade.searchBusinessPartnersPaged).mockResolvedValue({
      items: [INACTIVE_SUPPLIER],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
    });
    vi.mocked(businessPartnerFacade.activateBusinessPartner).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Activar proveedor" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(businessPartnerFacade.activateBusinessPartner).toHaveBeenCalledWith("bp-2");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/volverá a estar disponible/i);
    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Proveedor activado correctamente."),
    );
  });

  it("si falla, no muestra éxito falso", async () => {
    vi.mocked(businessPartnerFacade.searchBusinessPartnersPaged).mockResolvedValue({
      items: [INACTIVE_SUPPLIER],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
    });
    vi.mocked(businessPartnerFacade.activateBusinessPartner).mockRejectedValue({
      isAxiosError: true,
      response: { status: 500, data: {} },
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Activar proveedor" }));

    await waitFor(() => expect(message.error).toHaveBeenCalled());
    expect(message.success).not.toHaveBeenCalled();
  });
});

describe("MasterDataSuppliersPage — sin diálogos nativos", () => {
  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(businessPartnerFacade.deactivateBusinessPartner).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Proveedor Uno")).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "Desactivar proveedor" }));
    await waitFor(() =>
      expect(businessPartnerFacade.deactivateBusinessPartner).toHaveBeenCalled(),
    );

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
