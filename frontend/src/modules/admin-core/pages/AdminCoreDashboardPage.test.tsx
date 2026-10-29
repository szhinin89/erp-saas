// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  render,
  screen,
  fireEvent,
  waitFor,
  within,
} from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { useAuthStore } from "../../../store/authStore";
import { adminCoreService } from "../api/adminCoreService";
import { AdminCoreLayout } from "../components/AdminCoreLayout";
import { AdminCoreDashboardPage } from "./AdminCoreDashboardPage";
import {
  COMPANY_CREATE_TENANT_HINT_KEY,
  readCompanyCreateTenantHint,
} from "../utils/companyCreateTenantHint";
import { globalAdminAuthFacade } from "../../auth/facades/globalAdminAuthFacade";

// Espías del shell operativo (auth/session) — se configuran o se verifica que NO se
// disparen; admin-core nunca los importa directamente.
const shellSpies = vi.hoisted(() => ({
  authService: { returnToGlobal: vi.fn() },
  accessService: { getSessionMenu: vi.fn() },
  sessionService: {
    getAvailableBranches: vi.fn(),
    switchBranch: vi.fn(),
    getContext: vi.fn(),
  },
}));

const GLOBAL_TENANT_ID = "00000000-0000-0000-0000-000000000000";

vi.mock("../api/adminCoreService", () => ({
  adminCoreService: { listCompanies: vi.fn(), updateCompany: vi.fn() },
}));

vi.mock("../../auth/api/authService", () => ({ authService: shellSpies.authService }));

// Fase B: espías sobre los endpoints operativos que AdminGlobalCore nunca debe disparar.
vi.mock("../../auth/facades/globalAdminAuthFacade", () => ({
  globalAdminAuthFacade: { operateCompany: vi.fn() },
}));

vi.mock("../../auth/api/accessService", () => ({ accessService: shellSpies.accessService }));

vi.mock("../../session/api/sessionService", () => ({ sessionService: shellSpies.sessionService }));

function renderDashboard() {
  return render(
    <MemoryRouter initialEntries={["/admin-core/dashboard"]}>
      <Routes>
        <Route element={<AdminCoreLayout />}>
          <Route
            path="/admin-core/dashboard"
            element={<AdminCoreDashboardPage />}
          />
        </Route>
        <Route path="/dashboard" element={<div>DASHBOARD_OPERATIVO</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
  useAuthStore.setState({
    user: {
      userId: "admin-1",
      fullName: "Global Admin",
      username: "global",
      email: null,
      role: "Admin",
      tenantId: GLOBAL_TENANT_ID,
      companyId: null,
    },
    isAuthenticated: true,
    hasHydrated: true,
  });
});

afterEach(() => {
  cleanup();
  useAuthStore.setState({
    user: null,
    isAuthenticated: false,
    hasHydrated: false,
    token: null,
    companySessionVersion: 0,
  });
});

describe("AdminCoreDashboardPage", () => {
  it("lista empresas agrupadas por tenant y nunca dispara endpoints operativos", async () => {
    vi.mocked(adminCoreService.listCompanies).mockResolvedValue([
      {
        tenantId: "tenant-a",
        tenantName: "Tenant A",
        tenantIsActive: true,
        companyId: "company-1",
        ruc: "1790012345001",
        legalName: "Empresa Uno",
        tradeName: null,
        isActive: true,
      },
    ]);

    renderDashboard();

    expect(await screen.findByText("Tenant A")).toBeTruthy();
    expect(screen.getByText("Empresa Uno")).toBeTruthy();

    expect(shellSpies.accessService.getSessionMenu).not.toHaveBeenCalled();
    expect(shellSpies.sessionService.getAvailableBranches).not.toHaveBeenCalled();
    expect(shellSpies.sessionService.getContext).not.toHaveBeenCalled();
  });

  it("cada grupo de tenant tiene una acción 'Crear empresa en este tenant' hacia /admin-core/companies/new sin tenantId en la URL (SEC-02)", async () => {
    vi.mocked(adminCoreService.listCompanies).mockResolvedValue([
      {
        tenantId: "tenant-a",
        tenantName: "Tenant A",
        tenantIsActive: true,
        companyId: "company-1",
        ruc: "1790012345001",
        legalName: "Empresa Uno",
        tradeName: null,
        isActive: true,
      },
    ]);

    renderDashboard();

    await screen.findByText("Tenant A");
    const link = screen.getByRole("link", {
      name: "Crear empresa en este tenant",
    });
    expect(link.getAttribute("href")).toBe("/admin-core/companies/new");
    expect(sessionStorage.getItem(COMPANY_CREATE_TENANT_HINT_KEY)).toBeNull();

    fireEvent.click(link);

    expect(readCompanyCreateTenantHint()).toBe("tenant-a");
  });

  it("operate-company navega a /dashboard operativo", async () => {
    vi.mocked(adminCoreService.listCompanies).mockResolvedValue([
      {
        tenantId: "tenant-a",
        tenantName: "Tenant A",
        tenantIsActive: true,
        companyId: "company-1",
        ruc: "1790012345001",
        legalName: "Empresa Uno",
        tradeName: null,
        isActive: true,
      },
    ]);
    vi.mocked(globalAdminAuthFacade.operateCompany).mockResolvedValue({
      userId: "admin-1",
      fullName: "Global Admin",
      username: "global",
      email: null,
      role: "Admin",
      tenantId: "tenant-a",
      companyId: "company-1",
      requiresCompanySelection: false,
      token: "operative-jwt",
      operatorMode: true,
    });

    renderDashboard();

    const button = await screen.findByRole("button", {
      name: "Ingresar a esta empresa",
    });
    fireEvent.click(button);

    await waitFor(() => {
      expect(globalAdminAuthFacade.operateCompany).toHaveBeenCalledWith("company-1");
    });
    expect(await screen.findByText("DASHBOARD_OPERATIVO")).toBeTruthy();
    expect(useAuthStore.getState().user?.companyId).toBe("company-1");
  });
});

describe("Editar empresa global", () => {
  const company = {
    tenantId: "tenant-a",
    tenantName: "Tenant A",
    tenantIsActive: true,
    companyId: "company-1",
    ruc: "TMP-EC-ceb18408",
    legalName: "Original",
    tradeName: null,
    isActive: true,
  };

  it("usa campos espaciados y acciones en el footer oficial sin perder la asociacion del formulario", async () => {
    vi.mocked(adminCoreService.listCompanies).mockResolvedValue([company]);
    renderDashboard();
    fireEvent.click(await screen.findByRole("button", { name: "Editar" }));
    const dialog = screen.getByRole("dialog", { name: "Editar empresa" });
    const form = within(dialog).getByRole("form", { name: "Datos de la empresa" }) as HTMLFormElement;
    const footer = dialog.querySelector(".zh-modal-footer")!;
    expect(dialog.querySelector(".zh-modal--md")).toBeTruthy();
    expect(form.querySelector(".zh-grid--1")).toBeTruthy();
    const fields = within(form).getAllByRole("textbox");
    expect(fields).toHaveLength(3);
    for (const field of fields) expect(field.closest(".zh-field")).toBeTruthy();
    expect(within(form).getByRole("checkbox", { name: "Empresa activa" }).closest(".zh-checkbox-label")).toBeTruthy();
    const save = within(footer as HTMLElement).getByRole("button", { name: "Guardar" }) as HTMLButtonElement;
    expect(save.form).toBe(form);
    expect(save.type).toBe("submit");
    expect(form.contains(save)).toBe(false);
    expect((within(form).getByRole("textbox", { name: "Raz\u00f3n social" }) as HTMLInputElement).required).toBe(true);
    fireEvent.click(within(footer as HTMLElement).getByRole("button", { name: "Cancelar" }));
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(adminCoreService.updateCompany).not.toHaveBeenCalled();
  });

  it("mantiene campos y acciones deshabilitados durante el guardado", async () => {
    vi.mocked(adminCoreService.listCompanies).mockResolvedValue([company]);
    let finishSave!: () => void;
    vi.mocked(adminCoreService.updateCompany).mockImplementation(() => new Promise<void>(resolve => { finishSave = resolve; }));
    renderDashboard();
    fireEvent.click(await screen.findByRole("button", { name: "Editar" }));
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));
    await waitFor(() => expect(adminCoreService.updateCompany).toHaveBeenCalledOnce());
    const dialog = screen.getByRole("dialog");
    for (const input of within(dialog).getAllByRole("textbox")) expect((input as HTMLInputElement).disabled).toBe(true);
    expect((within(dialog).getByRole("checkbox") as HTMLInputElement).disabled).toBe(true);
    expect((within(dialog).getByRole("button", { name: "Guardando..." }) as HTMLButtonElement).disabled).toBe(true);
    expect((within(dialog).getByRole("button", { name: "Cancelar" }) as HTMLButtonElement).disabled).toBe(true);
    fireEvent.keyDown(window, { key: "Escape" });
    expect(screen.getByRole("dialog")).toBeTruthy();
    finishSave();
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });

  it("guarda sobre la empresa existente y refresca el RUC pendiente desde backend", async () => {
    vi.mocked(adminCoreService.listCompanies)
      .mockResolvedValueOnce([company])
      .mockResolvedValueOnce([
        { ...company, ruc: "1790016919001", legalName: "Actualizada" },
      ]);
    vi.mocked(adminCoreService.updateCompany).mockResolvedValue();
    renderDashboard();
    fireEvent.click(await screen.findByRole("button", { name: "Editar" }));
    expect(screen.getByText("RUC Pendiente")).toBeTruthy();
    fireEvent.change(screen.getByRole("textbox", { name: "RUC" }), {
      target: { value: "1790016919001" },
    });
    fireEvent.change(
      screen.getByRole("textbox", { name: "Raz\u00f3n social" }),
      { target: { value: "Actualizada" } },
    );
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));
    await waitFor(() =>
      expect(adminCoreService.updateCompany).toHaveBeenCalledWith(
        { ...company, legalName: "Actualizada" },
        "1790016919001",
      ),
    );
    expect(await screen.findByText("1790016919001")).toBeTruthy();
    expect(screen.queryByText("RUC Pendiente")).toBeNull();
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("mantiene el formulario y los datos originales si backend rechaza", async () => {
    vi.mocked(adminCoreService.listCompanies).mockResolvedValue([company]);
    vi.mocked(adminCoreService.updateCompany).mockRejectedValue(
      new Error("El RUC ya esta registrado en el sistema."),
    );
    renderDashboard();
    fireEvent.click(await screen.findByRole("button", { name: "Editar" }));
    fireEvent.change(screen.getByRole("textbox", { name: "RUC" }), {
      target: { value: "1234567890123" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Guardar" }));
    await waitFor(() =>
      expect(adminCoreService.updateCompany).toHaveBeenCalled(),
    );
    expect(
      await screen.findByText("El RUC ya esta registrado en el sistema."),
    ).toBeTruthy();
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect(screen.getByText("RUC Pendiente")).toBeTruthy();
    expect(adminCoreService.listCompanies).toHaveBeenCalledTimes(1);
  });
});
