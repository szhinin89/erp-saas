// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import type { AuthResponse } from "../types/auth";
import { authService } from "../modules/auth/api/authService";
import { syncCompanySelection } from "../modules/auth/syncCompanySelection";
import { CompanySwitcher } from "./CompanySwitcher";

vi.mock("../modules/auth/api/authService", () => ({
  authService: {
    listMyCompanies: vi.fn(),
    switchCompany: vi.fn(),
  },
}));

vi.mock("../modules/auth/syncCompanySelection", () => ({
  syncCompanySelection: vi.fn(),
}));

const authResponse: AuthResponse = {
  userId: "user-1",
  fullName: "Ana Perez",
  username: "ana",
  email: "ana@test.com",
  role: "Admin",
  tenantId: "tenant-1",
  companyId: "company-2",
  token: "access-token",
};

function renderSwitcher() {
  return render(
    <MemoryRouter initialEntries={["/settings"]}>
      <Routes>
        <Route path="/settings" element={<CompanySwitcher />} />
        <Route path="/dashboard" element={<div>Dashboard listo</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("CompanySwitcher", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({
      user: {
        userId: "user-1",
        fullName: "Ana Perez",
        username: "ana",
        email: "ana@test.com",
        role: "Admin",
        tenantId: "tenant-1",
        companyId: "company-1",
      },
      token: "old-token",
      isAuthenticated: true,
      hasHydrated: true,
      companySessionVersion: 0,
    });
    vi.mocked(authService.listMyCompanies).mockResolvedValue([
      {
        companyId: "company-1",
        tenantId: "tenant-1",
        legalName: "Empresa Uno S.A.",
        displayName: "Empresa Uno",
        ruc: "0999999998001",
        role: "Admin",
      },
      {
        companyId: "company-2",
        tenantId: "tenant-1",
        legalName: "Empresa Dos S.A.",
        displayName: "Empresa Dos",
        ruc: "0999999999001",
        role: "Admin",
      },
    ]);
    vi.mocked(authService.switchCompany).mockResolvedValue(authResponse);
    vi.mocked(syncCompanySelection).mockResolvedValue(undefined);
  });

  afterEach(() => {
    cleanup();
    useAuthStore.setState({
      user: null,
      token: null,
      isAuthenticated: false,
      hasHydrated: false,
      companySessionVersion: 0,
    });
  });

  it("usa la sincronización común después de switchCompany y navega al dashboard", async () => {
    renderSwitcher();

    const select = await screen.findByLabelText("Cambiar empresa operativa");
    fireEvent.change(select, { target: { value: "company-2" } });

    await waitFor(() => {
      expect(authService.switchCompany).toHaveBeenCalledWith("company-2");
    });
    expect(syncCompanySelection).toHaveBeenCalledWith(authResponse);
    expect(await screen.findByText("Dashboard listo")).toBeTruthy();
  });
});
