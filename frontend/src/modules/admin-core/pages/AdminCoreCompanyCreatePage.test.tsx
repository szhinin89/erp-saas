// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { adminCoreService } from "../api/adminCoreService";
import {
  COMPANY_CREATE_TENANT_HINT_KEY,
  COMPANY_CREATE_TENANT_HINT_TTL_MS,
  saveCompanyCreateTenantHint,
} from "../utils/companyCreateTenantHint";
import { AdminCoreCompanyCreatePage } from "./AdminCoreCompanyCreatePage";
import { companyRegistrationFacade } from "../../company-management/facades/companyRegistrationFacade";

// Espías del shell operativo (auth/session) — se configuran o se verifica que NO se
// disparen; admin-core nunca los importa directamente.
const shellSpies = vi.hoisted(() => ({
  accessService: { getSessionMenu: vi.fn() },
  sessionService: {
    getAvailableBranches: vi.fn(),
    switchBranch: vi.fn(),
    getContext: vi.fn(),
  },
}));

vi.mock("../../company-management/facades/companyRegistrationFacade", () => ({
  companyRegistrationFacade: { create: vi.fn() },
}));

vi.mock("../api/adminCoreService", () => ({
  adminCoreService: { listTenants: vi.fn() },
}));

// Fase B: espías sobre endpoints operativos que AdminGlobalCore nunca debe disparar.
vi.mock("../../auth/api/accessService", () => ({ accessService: shellSpies.accessService }));

vi.mock("../../session/api/sessionService", () => ({ sessionService: shellSpies.sessionService }));

const TENANT_A = { tenantId: "tenant-a", tenantName: "Tenant A", tenantIsActive: true };
const TENANT_B = { tenantId: "tenant-b", tenantName: "Tenant B", tenantIsActive: true };

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location">{`${location.pathname}${location.search}`}</div>;
}

function renderPage(initialPath = "/admin-core/companies/new") {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <LocationProbe />
      <Routes>
        <Route path="/admin-core/companies/new" element={<AdminCoreCompanyCreatePage />} />
        <Route path="/admin-core/dashboard" element={<div>ADMIN_CORE_DASHBOARD</div>} />
        <Route path="/companies" element={<div>COMPANIES_OPERATIVO</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("AdminCoreCompanyCreatePage — selector de tenant", () => {
  it("ya no muestra el input de texto libre 'GUID del tenant real'", async () => {
    vi.mocked(adminCoreService.listTenants).mockResolvedValue([TENANT_A, TENANT_B]);

    renderPage();

    await screen.findByLabelText("Tenant / grupo destino", { exact: false });
    expect(screen.queryByPlaceholderText("GUID del tenant real")).toBeNull();
  });

  it("renderiza el selector 'Tenant / grupo destino' cargado desde adminCoreService", async () => {
    vi.mocked(adminCoreService.listTenants).mockResolvedValue([TENANT_A, TENANT_B]);

    renderPage();

    const select = (await screen.findByLabelText("Tenant / grupo destino", {
      exact: false,
    })) as HTMLSelectElement;
    expect(adminCoreService.listTenants).toHaveBeenCalledTimes(1);
    expect(select.tagName).toBe("SELECT");
    expect(screen.getByRole("option", { name: "Tenant A" })).toBeTruthy();
    expect(screen.getByRole("option", { name: "Tenant B" })).toBeTruthy();
  });

  it("si hay un solo tenant, lo preselecciona automáticamente", async () => {
    vi.mocked(adminCoreService.listTenants).mockResolvedValue([TENANT_A]);

    renderPage();

    const select = (await screen.findByLabelText("Tenant / grupo destino", {
      exact: false,
    })) as HTMLSelectElement;
    await waitFor(() => expect(select.value).toBe("tenant-a"));
  });

  it("preselecciona el tenant del hint de navegación y lo consume (un solo uso)", async () => {
    vi.mocked(adminCoreService.listTenants).mockResolvedValue([TENANT_A, TENANT_B]);
    saveCompanyCreateTenantHint("tenant-b");

    renderPage();

    const select = (await screen.findByLabelText("Tenant / grupo destino", {
      exact: false,
    })) as HTMLSelectElement;
    await waitFor(() => expect(select.value).toBe("tenant-b"));
    expect(sessionStorage.getItem(COMPANY_CREATE_TENANT_HINT_KEY)).toBeNull();
  });

  it("ignora y limpia un hint cuyo tenant no figura en el listado de la API", async () => {
    vi.mocked(adminCoreService.listTenants).mockResolvedValue([TENANT_A, TENANT_B]);
    saveCompanyCreateTenantHint("tenant-ajeno");

    renderPage();

    const select = (await screen.findByLabelText("Tenant / grupo destino", {
      exact: false,
    })) as HTMLSelectElement;
    await waitFor(() =>
      expect(sessionStorage.getItem(COMPANY_CREATE_TENANT_HINT_KEY)).toBeNull(),
    );
    expect(select.value).toBe("");
  });

  it("ignora y limpia un hint vencido", async () => {
    vi.mocked(adminCoreService.listTenants).mockResolvedValue([TENANT_A, TENANT_B]);
    sessionStorage.setItem(
      COMPANY_CREATE_TENANT_HINT_KEY,
      JSON.stringify({
        tenantId: "tenant-b",
        savedAtMs: Date.now() - COMPANY_CREATE_TENANT_HINT_TTL_MS - 1,
      }),
    );

    renderPage();

    const select = (await screen.findByLabelText("Tenant / grupo destino", {
      exact: false,
    })) as HTMLSelectElement;
    await waitFor(() =>
      expect(sessionStorage.getItem(COMPANY_CREATE_TENANT_HINT_KEY)).toBeNull(),
    );
    expect(select.value).toBe("");
  });

  it("ignora y limpia un hint malformado", async () => {
    vi.mocked(adminCoreService.listTenants).mockResolvedValue([TENANT_A, TENANT_B]);
    sessionStorage.setItem(COMPANY_CREATE_TENANT_HINT_KEY, "{no-es-json");

    renderPage();

    const select = (await screen.findByLabelText("Tenant / grupo destino", {
      exact: false,
    })) as HTMLSelectElement;
    expect(sessionStorage.getItem(COMPANY_CREATE_TENANT_HINT_KEY)).toBeNull();
    expect(select.value).toBe("");
  });

  it("limpia el hint aunque falle la carga de tenants", async () => {
    vi.mocked(adminCoreService.listTenants).mockRejectedValue(new Error("boom"));
    saveCompanyCreateTenantHint("tenant-b");

    renderPage();

    await waitFor(() => expect(adminCoreService.listTenants).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(sessionStorage.getItem(COMPANY_CREATE_TENANT_HINT_KEY)).toBeNull(),
    );
  });

  it("SEC-02: un enlace legacy con tenant en la URL no preselecciona y la URL queda limpia", async () => {
    vi.mocked(adminCoreService.listTenants).mockResolvedValue([TENANT_A, TENANT_B]);

    renderPage("/admin-core/companies/new?tenantId=tenant-b");

    const select = (await screen.findByLabelText("Tenant / grupo destino", {
      exact: false,
    })) as HTMLSelectElement;
    await waitFor(() =>
      expect(screen.getByTestId("location").textContent).toBe("/admin-core/companies/new"),
    );
    expect(select.value).toBe("");
  });

  it("si no hay tenants, muestra el mensaje controlado y no renderiza el formulario", async () => {
    vi.mocked(adminCoreService.listTenants).mockResolvedValue([]);

    renderPage();

    expect(
      await screen.findByText("No hay tenants disponibles para crear empresas."),
    ).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Crear empresa" })).toBeNull();
  });

  it("al crear una empresa envía el tenantId seleccionado y muestra éxito", async () => {
    vi.mocked(adminCoreService.listTenants).mockResolvedValue([TENANT_A, TENANT_B]);
    vi.mocked(companyRegistrationFacade.create).mockResolvedValue({
      id: "company-1",
      tenantId: "tenant-b",
      legalName: "Empresa Nueva",
      tradeName: null,
      taxId: "1790012345001",
      countryCode: "ECU",
      timezone: "America/Guayaquil",
      currencyCode: "USD",
      isActive: true,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    });

    renderPage();

    const select = (await screen.findByLabelText("Tenant / grupo destino", {
      exact: false,
    })) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: "tenant-b" } });
    fireEvent.change(screen.getByLabelText("RUC", { exact: false }), {
      target: { value: "1790012345001" },
    });
    fireEvent.change(screen.getByLabelText("Razón social", { exact: false }), {
      target: { value: "Empresa Nueva" },
    });

    fireEvent.click(screen.getByRole("button", { name: "Crear empresa" }));

    expect(await screen.findByText(/Empresa "Empresa Nueva" creada correctamente\./)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Crear otra empresa" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Volver al dashboard global" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Cerrar sesión" })).toBeTruthy();
    expect(screen.queryByText("COMPANIES_OPERATIVO")).toBeNull();

    await waitFor(() => {
      expect(companyRegistrationFacade.create).toHaveBeenCalledWith({
        tenantId: "tenant-b",
        taxId: "1790012345001",
        legalName: "Empresa Nueva",
        tradeName: null,
      });
    });
  });

  it("nunca dispara endpoints operativos", async () => {
    vi.mocked(adminCoreService.listTenants).mockResolvedValue([TENANT_A]);

    renderPage();

    await screen.findByLabelText("Tenant / grupo destino", { exact: false });

    expect(shellSpies.accessService.getSessionMenu).not.toHaveBeenCalled();
    expect(shellSpies.sessionService.getAvailableBranches).not.toHaveBeenCalled();
    expect(shellSpies.sessionService.getContext).not.toHaveBeenCalled();
  });
});
