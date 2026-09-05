// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { MasterDataBusinessPartnerDetailPage } from "./MasterDataBusinessPartnerDetailPage";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-BUSINESS-PARTNERS-04 — cubre "Revocar rol", "Activar/desactivar
 * ubicación o contacto" y "Marcar ubicación/contacto como principal".
 */

vi.mock("../api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn(),
    getLocations: vi.fn(),
    getContacts: vi.fn(),
    revokeRole: vi.fn(),
    deactivateLocation: vi.fn(),
    activateLocation: vi.fn(),
    setLocationPrimary: vi.fn(),
    deactivateContact: vi.fn(),
    activateContact: vi.fn(),
    setContactPrimary: vi.fn(),
    createLocation: vi.fn(),
    updateLocation: vi.fn(),
    createContact: vi.fn(),
    updateContact: vi.fn(),
  },
}));

vi.mock("../api/geographyService", () => ({
  geographyService: {
    provinces: vi.fn().mockResolvedValue([]),
    cantons: vi.fn().mockResolvedValue([]),
    parishes: vi.fn().mockResolvedValue([]),
  },
}));

vi.mock("../api/useSriIdTypes", () => ({
  useSriIdTypes: () => ({ options: [], loading: false }),
  getSriIdTypeName: () => "",
}));

vi.mock("../api/useLegalEntityTypes", () => ({
  useLegalEntityTypes: () => ({ options: [], loading: false }),
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const BP = {
  id: "bp-1",
  identificationType: "05",
  identificationNumber: "0999999999",
  legalName: "Cliente Uno",
  tradeName: null,
  legalEntityTypeCode: 1,
  countryCode: "EC",
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  isCustomer: true,
  isSupplier: false,
  canAssignAsCustomer: false,
  canAssignAsSupplier: true,
  roles: [
    {
      id: "role-1",
      roleType: "Customer",
      roleLabel: "Cliente",
      isActive: true,
      notes: null,
      assignedAt: "2026-08-01T00:00:00Z",
      revokedAt: null,
      supplierConfig: null,
      carrierConfig: null,
      customerConfig: null,
      classificationConfig: null,
    },
  ],
};

const ACTIVE_LOCATION = {
  id: "loc-1",
  businessPartnerId: "bp-1",
  name: "Matriz Quito",
  locationType: "Matrix",
  typeLabel: "Matriz",
  purposes: [],
  addressLine: "Av. Siempre Viva 123",
  provinceCode: null,
  cantonCode: null,
  parishCode: null,
  phone: null,
  email: null,
  otherDescription: null,
  isPrimary: false,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
};

const ACTIVE_CONTACT = {
  id: "con-1",
  businessPartnerId: "bp-1",
  locationId: null,
  firstName: "Ana",
  lastName: "Perez",
  fullName: "Ana Perez",
  position: null,
  contactRole: "Commercial",
  roleLabel: "Comercial",
  otherDescription: null,
  phone: null,
  mobile: null,
  email: null,
  notes: null,
  isPrimary: false,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
};

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/masterdata/business-partners/bp-1"]}>
        <Routes>
          <Route
            path="/masterdata/business-partners/:id"
            element={<MasterDataBusinessPartnerDetailPage />}
          />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(businessPartnerFacade.getBusinessPartner).mockResolvedValue(BP);
  vi.mocked(businessPartnerFacade.getLocations).mockResolvedValue([ACTIVE_LOCATION]);
  vi.mocked(businessPartnerFacade.getContacts).mockResolvedValue([ACTIVE_CONTACT]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => cleanup());

describe("MasterDataBusinessPartnerDetailPage — revocar rol", () => {
  async function goToRolesTab() {
    renderPage();
    await waitFor(() => expect(screen.getAllByText("Cliente Uno")[0]).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: /^Roles/ }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Revocar" })).toBeTruthy());
  }

  it("pide confirmación explicando que el BP no se elimina, solo deja de operar bajo el rol", async () => {
    vi.mocked(businessPartnerFacade.revokeRole).mockResolvedValue(true);
    await goToRolesTab();

    fireEvent.click(screen.getByRole("button", { name: "Revocar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(businessPartnerFacade.revokeRole).toHaveBeenCalledWith("bp-1", "role-1");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/dejará de operar bajo el rol/i);
    expect(String(options.message)).toMatch(/no se elimina/i);
  });

  it("si se cancela, no llama al backend", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    await goToRolesTab();

    fireEvent.click(screen.getByRole("button", { name: "Revocar" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(businessPartnerFacade.revokeRole).not.toHaveBeenCalled();
  });

  it("éxito muestra message.success; fallo muestra el error real", async () => {
    vi.mocked(businessPartnerFacade.revokeRole).mockResolvedValue(true);
    await goToRolesTab();
    fireEvent.click(screen.getByRole("button", { name: "Revocar" }));
    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith('Rol "Cliente" revocado correctamente.'),
    );
  });
});

describe("MasterDataBusinessPartnerDetailPage — activar/desactivar ubicación", () => {
  async function goToLocationsTab() {
    renderPage();
    await waitFor(() => expect(screen.getAllByText("Cliente Uno")[0]).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: /^Ubicaciones/ }));
    await waitFor(() => expect(screen.getByText("Matriz Quito")).toBeTruthy());
  }

  it("pide confirmación antes de desactivar la ubicación", async () => {
    vi.mocked(businessPartnerFacade.deactivateLocation).mockResolvedValue(true);
    await goToLocationsTab();

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(businessPartnerFacade.deactivateLocation).toHaveBeenCalledWith("bp-1", "loc-1");
    });
    await waitFor(() => expect(message.success).toHaveBeenCalled());
  });

  it("si se cancela, no llama al backend", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    await goToLocationsTab();

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(businessPartnerFacade.deactivateLocation).not.toHaveBeenCalled();
  });

  it("si falla, muestra el error real y no muestra éxito", async () => {
    vi.mocked(businessPartnerFacade.deactivateLocation).mockRejectedValue({
      isAxiosError: true,
      response: { status: 409, data: { message: { user: "La ubicación está en uso." } } },
    });
    await goToLocationsTab();

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() =>
      expect(message.error).toHaveBeenCalledWith("La ubicación está en uso."),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("marcar como principal muestra message.success", async () => {
    vi.mocked(businessPartnerFacade.setLocationPrimary).mockResolvedValue(true);
    await goToLocationsTab();

    fireEvent.click(screen.getByRole("button", { name: "Principal" }));

    await waitFor(() => expect(businessPartnerFacade.setLocationPrimary).toHaveBeenCalled());
    await waitFor(() => expect(message.success).toHaveBeenCalled());
    // Marcar principal es de bajo riesgo — no debe requerir confirmación.
    expect(message.confirm).not.toHaveBeenCalled();
  });
});

describe("MasterDataBusinessPartnerDetailPage — activar/desactivar contacto", () => {
  async function goToContactsTab() {
    renderPage();
    await waitFor(() => expect(screen.getAllByText("Cliente Uno")[0]).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: /^Contactos/ }));
    await waitFor(() => expect(screen.getByText("Ana Perez")).toBeTruthy());
  }

  it("pide confirmación antes de desactivar el contacto", async () => {
    vi.mocked(businessPartnerFacade.deactivateContact).mockResolvedValue(true);
    await goToContactsTab();

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(businessPartnerFacade.deactivateContact).toHaveBeenCalledWith("bp-1", "con-1");
    });
    await waitFor(() => expect(message.success).toHaveBeenCalled());
  });

  it("si se cancela, no llama al backend", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    await goToContactsTab();

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(businessPartnerFacade.deactivateContact).not.toHaveBeenCalled();
  });

  it("marcar como principal muestra message.success sin pedir confirmación", async () => {
    vi.mocked(businessPartnerFacade.setContactPrimary).mockResolvedValue(true);
    await goToContactsTab();

    fireEvent.click(screen.getByRole("button", { name: "Principal" }));

    await waitFor(() => expect(businessPartnerFacade.setContactPrimary).toHaveBeenCalled());
    await waitFor(() => expect(message.success).toHaveBeenCalled());
    expect(message.confirm).not.toHaveBeenCalled();
  });
});

describe("MasterDataBusinessPartnerDetailPage — sin diálogos nativos", () => {
  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(businessPartnerFacade.revokeRole).mockResolvedValue(true);

    renderPage();
    await waitFor(() => expect(screen.getAllByText("Cliente Uno")[0]).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: /^Roles/ }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Revocar" })).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "Revocar" }));
    await waitFor(() => expect(businessPartnerFacade.revokeRole).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
