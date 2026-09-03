// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { companyManagementService } from "../../company-management/api/companyManagementService";
import { AdminCoreCompanyCreatePage } from "./AdminCoreCompanyCreatePage";

vi.mock("../../company-management/api/companyManagementService", () => ({
  companyManagementService: { create: vi.fn() },
}));

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/admin-core/companies/new"]}>
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
  vi.restoreAllMocks();
});

describe("AdminCoreCompanyCreatePage", () => {
  it("al crear una empresa muestra éxito con 3 acciones y NO navega a /companies", async () => {
    vi.mocked(companyManagementService.create).mockResolvedValue({
      id: "company-1",
      tenantId: "tenant-1",
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

    fireEvent.change(screen.getByLabelText("Tenant destino", { exact: false }), {
      target: { value: "tenant-1" },
    });
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
        tenantId: "tenant-1",
        taxId: "1790012345001",
        legalName: "Empresa Nueva",
        tradeName: null,
      });
    });
  });
});
