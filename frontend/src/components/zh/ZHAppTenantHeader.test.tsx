// @vitest-environment jsdom
import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { I18nProvider } from "../../i18n/i18n";
import { useActiveBranchStore } from "../../store/activeBranchStore";
import { useAuthStore } from "../../store/authStore";
import { useSessionStore } from "../../store/sessionStore";
import { ZHAppTenantHeader } from "./ZHAppTenantHeader";

function renderHeader() {
  return render(
    <I18nProvider>
      <ZHAppTenantHeader />
    </I18nProvider>,
  );
}

describe("ZHAppTenantHeader", () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
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
      token: null,
      isAuthenticated: true,
      hasHydrated: true,
      companySessionVersion: 0,
    });
    useSessionStore.setState({
      tenant: {
        id: "company-1",
        displayName: "Comercial Andrade S.A.",
        logo: null,
      },
    });
    useActiveBranchStore.setState({
      branch: { id: "branch-1", name: "Matriz", isMainBranch: true },
    });
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
    useSessionStore.setState({
      tenant: null,
      identity: null,
      authorization: null,
      preferences: null,
      isLoaded: false,
    });
    useActiveBranchStore.setState({ branch: null });
    localStorage.clear();
    sessionStorage.clear();
  });

  it("muestra empresa activa, sucursal activa y rol", () => {
    renderHeader();

    expect(screen.getByText("Comercial Andrade S.A. / Matriz")).toBeTruthy();
    expect(screen.getByText("Admin")).toBeTruthy();
    expect(screen.getByTitle(/Empresa: Comercial Andrade S\.A\./)).toBeTruthy();
  });

  it("muestra fallback cuando hay empresa pero no hay sucursal activa", () => {
    useActiveBranchStore.setState({ branch: null });

    renderHeader();

    expect(
      screen.getByText("Comercial Andrade S.A. / Seleccionar sucursal"),
    ).toBeTruthy();
    expect(screen.getByText("Admin")).toBeTruthy();
  });

  it("deja de mostrar la sucursal anterior cuando se limpia activeBranchStore", () => {
    renderHeader();

    expect(screen.getByText("Comercial Andrade S.A. / Matriz")).toBeTruthy();

    act(() => {
      useActiveBranchStore.getState().clear();
    });

    expect(screen.queryByText("Comercial Andrade S.A. / Matriz")).toBeNull();
    expect(
      screen.getByText("Comercial Andrade S.A. / Seleccionar sucursal"),
    ).toBeTruthy();
  });
});
