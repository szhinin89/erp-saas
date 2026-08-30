// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataCustomersPage } from "./MasterDataCustomersPage";
import { useMasterDataCustomersUiStore } from "../store/masterDataPartnerUiStore";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";

/**
 * CRITICAL-CONFIRMATIONS-BUSINESS-PARTNERS-04 — cubre "Activar/desactivar cliente", incluyendo
 * el falso éxito detectado por la auditoría: antes se mostraba éxito (message.info) aunque
 * page.disableCustomer/activateCustomer hubiera fallado, porque el hook no relanzaba el error.
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

const CUSTOMER: BusinessPartnerSummaryDto = {
  id: "bp-1",
  identificationType: "05",
  identificationNumber: "0999999999",
  legalName: "Cliente Uno",
  tradeName: null,
  legalEntityTypeCode: 1,
  countryCode: "EC",
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
};

/** MasterDataCustomersPage resetea el store (incluida activeTab) al montar — se preseleccionaba
 * "listado" antes de renderizar, pero el propio `useEffect(() => reset(), [])` de la pantalla lo
 * revierte a "resumen". Se navega a la pestaña "Listado" después de montar en su lugar. */
function renderPage() {
  const utils = render(
    <I18nProvider>
      <MemoryRouter>
        <MasterDataCustomersPage />
      </MemoryRouter>
    </I18nProvider>,
  );
  fireEvent.click(screen.getByRole("tab", { name: /Listado/i }));
  return utils;
}

beforeEach(() => {
  vi.clearAllMocks();
  useMasterDataCustomersUiStore.setState({
    activeTab: "listado",
    editingPartner: null,
    recentActivity: [],
  });
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(businessPartnerFacade.searchBusinessPartnersPaged).mockResolvedValue({
    items: [CUSTOMER],
    totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
  });
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => {
  cleanup();
  useMasterDataCustomersUiStore.setState({
    activeTab: "resumen",
    editingPartner: null,
    recentActivity: [],
  });
});

describe("MasterDataCustomersPage — desactivar cliente: confirmación y feedback", () => {
  it("pide confirmación antes de desactivar, explicando que no borra histórico", async () => {
    vi.mocked(businessPartnerFacade.deactivateBusinessPartner).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Cliente Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Deshabilitar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(businessPartnerFacade.deactivateBusinessPartner).toHaveBeenCalledWith("bp-1");
    });

    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/dejará de estar disponible/i);
    expect(String(options.message)).toMatch(/no se elimina/i);
  });

  it("si se cancela, no llama al backend", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);

    renderPage();
    await waitFor(() => expect(screen.getByText("Cliente Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Deshabilitar" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(businessPartnerFacade.deactivateBusinessPartner).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente muestra message.success (no message.info)", async () => {
    vi.mocked(businessPartnerFacade.deactivateBusinessPartner).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Cliente Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Deshabilitar" }));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Cliente desactivado correctamente."),
    );
    expect(message.info).not.toHaveBeenCalled();
  });

  it("REGRESIÓN falso-éxito: si el backend falla, NO muestra éxito y sí muestra el error real", async () => {
    vi.mocked(businessPartnerFacade.deactivateBusinessPartner).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El cliente tiene facturas pendientes." } },
      },
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Cliente Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Deshabilitar" }));

    await waitFor(() => {
      expect(message.error).toHaveBeenCalledWith("El cliente tiene facturas pendientes.");
    });
    // Antes de la corrección, este assert fallaba: se llamaba message.info("Cliente desactivado.")
    // incondicionalmente, sin importar el resultado real del backend.
    expect(message.success).not.toHaveBeenCalled();
    expect(message.info).not.toHaveBeenCalled();
  });
});

describe("MasterDataCustomersPage — activar cliente: confirmación y feedback", () => {
  const INACTIVE_CUSTOMER: BusinessPartnerSummaryDto = { ...CUSTOMER, isActive: false };

  it("pide confirmación antes de activar y muestra success al confirmar", async () => {
    vi.mocked(businessPartnerFacade.searchBusinessPartnersPaged).mockResolvedValue({
      items: [INACTIVE_CUSTOMER],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
    });
    vi.mocked(businessPartnerFacade.activateBusinessPartner).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Cliente Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Habilitar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(businessPartnerFacade.activateBusinessPartner).toHaveBeenCalledWith("bp-1");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/volverá a estar disponible/i);
    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Cliente activado correctamente."),
    );
  });

  it("si falla, no muestra éxito falso", async () => {
    vi.mocked(businessPartnerFacade.searchBusinessPartnersPaged).mockResolvedValue({
      items: [INACTIVE_CUSTOMER],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
    });
    vi.mocked(businessPartnerFacade.activateBusinessPartner).mockRejectedValue({
      isAxiosError: true,
      response: { status: 500, data: {} },
    });

    renderPage();
    await waitFor(() => expect(screen.getByText("Cliente Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Habilitar" }));

    await waitFor(() => expect(message.error).toHaveBeenCalled());
    expect(message.success).not.toHaveBeenCalled();
  });
});

describe("MasterDataCustomersPage — sin diálogos nativos", () => {
  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(businessPartnerFacade.deactivateBusinessPartner).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText("Cliente Uno")).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "Deshabilitar" }));
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

describe("MasterDataCustomersPage — ZH-LISTING-COMPLIANCE-AUDIT-08, showRowNumber", () => {
  it('muestra "N°" como primera columna sin perder la identificación del cliente', async () => {
    renderPage();
    await waitFor(() => expect(screen.getByText("Cliente Uno")).toBeTruthy());

    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers[0]).toBe("N°");
    expect(screen.getByText("0999999999")).toBeTruthy();
  });
});
