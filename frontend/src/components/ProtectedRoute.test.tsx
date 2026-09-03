// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import { ProtectedRoute } from "./ProtectedRoute";

const GLOBAL_TENANT_ID = "00000000-0000-0000-0000-000000000000";

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<ProtectedRoute />}>
          <Route path="/dashboard" element={<div>DASHBOARD_OPERATIVO</div>} />
          <Route
            path="/electronic-documents/monitor"
            element={<div>ELECTRONIC_DOCUMENTS_MONITOR</div>}
          />
        </Route>
        <Route path="/select-company" element={<div>SELECT_COMPANY</div>} />
        <Route path="/admin-core/dashboard" element={<div>ADMIN_CORE_DASHBOARD</div>} />
        <Route path="/login" element={<div>LOGIN</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

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

describe("ProtectedRoute — /electronic-documents/monitor", () => {
  it("usuario tenant normal sin companyId es redirigido a /select-company", () => {
    useAuthStore.setState({
      user: {
        userId: "user-1",
        fullName: "Ana Perez",
        username: "ana",
        email: "ana@test.com",
        role: "Admin",
        tenantId: "tenant-1",
        companyId: null,
      },
      isAuthenticated: true,
      hasHydrated: true,
    });

    renderAt("/electronic-documents/monitor");

    expect(screen.getByText("SELECT_COMPANY")).toBeTruthy();
    expect(screen.queryByText("ELECTRONIC_DOCUMENTS_MONITOR")).toBeNull();
  });

  it("AdminGlobalCore global (sin operar empresa) es redirigido a /admin-core/dashboard", () => {
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

    renderAt("/electronic-documents/monitor");

    expect(screen.getByText("ADMIN_CORE_DASHBOARD")).toBeTruthy();
    expect(screen.queryByText("ELECTRONIC_DOCUMENTS_MONITOR")).toBeNull();
  });

  it("usuario operativo con companyId real puede montar el monitor", () => {
    useAuthStore.setState({
      user: {
        userId: "user-2",
        fullName: "Carlos Ruiz",
        username: "carlos",
        email: "carlos@test.com",
        role: "Admin",
        tenantId: "tenant-1",
        companyId: "company-1",
      },
      isAuthenticated: true,
      hasHydrated: true,
    });

    renderAt("/electronic-documents/monitor");

    expect(screen.getByText("ELECTRONIC_DOCUMENTS_MONITOR")).toBeTruthy();
  });

  it("AdminGlobalCore operando empresa (tenantId/companyId reales) puede montar el monitor", () => {
    useAuthStore.setState({
      user: {
        userId: "admin-1",
        fullName: "Global Admin",
        username: "global",
        email: null,
        role: "Admin",
        tenantId: "tenant-operated-1",
        companyId: "company-operated-1",
      },
      isAuthenticated: true,
      hasHydrated: true,
    });

    renderAt("/electronic-documents/monitor");

    expect(screen.getByText("ELECTRONIC_DOCUMENTS_MONITOR")).toBeTruthy();
  });

  it("no rompe rutas operativas existentes (/dashboard sigue accesible con companyId)", () => {
    useAuthStore.setState({
      user: {
        userId: "user-3",
        fullName: "Laura Diaz",
        username: "laura",
        email: "laura@test.com",
        role: "Admin",
        tenantId: "tenant-1",
        companyId: "company-1",
      },
      isAuthenticated: true,
      hasHydrated: true,
    });

    renderAt("/dashboard");

    expect(screen.getByText("DASHBOARD_OPERATIVO")).toBeTruthy();
  });
});
