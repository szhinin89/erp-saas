// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import { AdminCoreProtectedRoute } from "./AdminCoreProtectedRoute";

const GLOBAL_TENANT_ID = "00000000-0000-0000-0000-000000000000";

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<AdminCoreProtectedRoute />}>
          <Route path="/admin-core/dashboard" element={<div>ADMIN_CORE_DASHBOARD</div>} />
        </Route>
        <Route path="/admin-core/login" element={<div>ADMIN_CORE_LOGIN</div>} />
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

describe("AdminCoreProtectedRoute", () => {
  it("deja pasar una sesión AdminGlobalCore genuina (tenant_id vacío, sin companyId, rol Admin)", () => {
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

    renderAt("/admin-core/dashboard");

    expect(screen.getByText("ADMIN_CORE_DASHBOARD")).toBeTruthy();
  });

  it("redirige a un AdminEmpresa (tenant real) fuera de /admin-core/*", () => {
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
      isAuthenticated: true,
      hasHydrated: true,
    });

    renderAt("/admin-core/dashboard");

    expect(screen.getByText("ADMIN_CORE_LOGIN")).toBeTruthy();
  });

  it("redirige a un usuario no autenticado a /admin-core/login", () => {
    useAuthStore.setState({
      user: null,
      isAuthenticated: false,
      hasHydrated: true,
    });

    renderAt("/admin-core/dashboard");

    expect(screen.getByText("ADMIN_CORE_LOGIN")).toBeTruthy();
  });
});
