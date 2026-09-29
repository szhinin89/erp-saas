// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { useAuthStore } from "../../../store/authStore";
import { AdminCoreLoginPage } from "./AdminCoreLoginPage";
import { globalAdminAuthFacade } from "../../auth/facades/globalAdminAuthFacade";

const GLOBAL_TENANT_ID = "00000000-0000-0000-0000-000000000000";

vi.mock("../../auth/facades/globalAdminAuthFacade", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../auth/facades/globalAdminAuthFacade")>()),
  globalAdminAuthFacade: { globalLogin: vi.fn() },
}));

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/admin-core/login"]}>
      <Routes>
        <Route path="/admin-core/login" element={<AdminCoreLoginPage />} />
        <Route path="/admin-core/dashboard" element={<div>ADMIN_CORE_DASHBOARD</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  useAuthStore.setState({
    user: null,
    isAuthenticated: false,
    hasHydrated: false,
    token: null,
    companySessionVersion: 0,
  });
});

describe("AdminCoreLoginPage", () => {
  it("hace login global y navega a /admin-core/dashboard", async () => {
    vi.mocked(globalAdminAuthFacade.globalLogin).mockResolvedValue({
      userId: "admin-1",
      fullName: "Global Admin",
      username: "global",
      email: null,
      role: "Admin",
      tenantId: GLOBAL_TENANT_ID,
      companyId: null,
      requiresCompanySelection: false,
      token: "global-jwt",
    });

    renderPage();

    fireEvent.change(screen.getByLabelText("Usuario"), {
      target: { value: "global" },
    });
    fireEvent.change(screen.getByLabelText("Contraseña"), {
      target: { value: "secret" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Ingresar" }));

    await waitFor(() => {
      expect(globalAdminAuthFacade.globalLogin).toHaveBeenCalledWith({
        username: "global",
        password: "secret",
      });
    });

    expect(await screen.findByText("ADMIN_CORE_DASHBOARD")).toBeTruthy();
    expect(useAuthStore.getState().user?.tenantId).toBe(GLOBAL_TENANT_ID);
  });
});
