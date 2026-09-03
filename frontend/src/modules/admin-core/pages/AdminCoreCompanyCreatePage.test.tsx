// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { companyManagementService } from "../../company-management/api/companyManagementService";
import { adminCoreService } from "../api/adminCoreService";
import { accessService } from "../../auth/api/accessService";
import { sessionService } from "../../session/api/sessionService";
import { AdminCoreCompanyCreatePage } from "./AdminCoreCompanyCreatePage";

vi.mock("../../company-management/api/companyManagementService", () => ({
  companyManagementService: { create: vi.fn() },
}));

vi.mock("../api/adminCoreService", () => ({
  adminCoreService: { listTenants: vi.fn() },
}));

// Fase B: espías sobre endpoints operativos que AdminGlobalCore nunca debe disparar.
vi.mock("../../auth/api/accessService", () => ({
  accessService: { getSessionMenu: vi.fn() },
}));

vi.mock("../../session/api/sessionService", () => ({
  sessionService: {
    getAvailableBranches: vi.fn(),
    switchBranch: vi.fn(),
    getContext: vi.fn(),
  },
}));

const TENANT_A = { tenantId: "tenant-a", tenantName: "Tenant A", tenantIsActive: true };
const TENANT_B = { tenantId: "tenant-b", tenantName: "Tenant B", tenantIsActive: true };

function renderPage(initialPath = "/admin-core/companies/new") {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
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

  it("preselecciona el tenant recibido por querystring ?tenantId=", async () => {
    vi.mocked(adminCoreService.listTenants).mockResolvedValue([TENANT_A, TENANT_B]);

    renderPage("/admin-core/companies/new?tenantId=tenant-b");

    const select = (await screen.findByLabelText("Tenant / grupo destino", {
      exact: false,
    })) as HTMLSelectElement;
    await waitFor(() => expect(select.value).toBe("tenant-b"));
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
    vi.mocked(companyManagementService.create).mockResolvedValue({
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
      expect(companyManagementService.create).toHaveBeenCalledWith({
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

    expect(accessService.getSessionMenu).not.toHaveBeenCalled();
    expect(sessionService.getAvailableBranches).not.toHaveBeenCalled();
    expect(sessionService.getContext).not.toHaveBeenCalled();
  });
});
