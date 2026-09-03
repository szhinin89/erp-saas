// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { companyManagementService } from "../api/companyManagementService";
import { CompanyManagementListPage } from "./CompanyManagementListPage";

vi.mock("../api/companyManagementService", () => ({
  companyManagementService: { list: vi.fn() },
}));

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <CompanyManagementListPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(companyManagementService.list).mockResolvedValue([]);
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

/** ZH-ADMINGLOBALCORE-MENU-BOUNDARY-CLEANUP-05O */
describe("CompanyManagementListPage — sin acción operativa de crear empresa", () => {
  it("no muestra el botón/link 'Nueva empresa' (la creación es exclusiva de AdminCore)", async () => {
    renderPage();

    await screen.findByText("Actualizar");

    expect(screen.queryByText("Nueva empresa")).toBeNull();
    expect(screen.queryByRole("link", { name: "Nueva empresa" })).toBeNull();
  });

  it("mantiene el botón de refrescar el listado", async () => {
    renderPage();

    expect(await screen.findByRole("button", { name: "Actualizar" })).toBeTruthy();
  });
});
