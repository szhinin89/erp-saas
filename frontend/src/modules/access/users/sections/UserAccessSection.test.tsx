// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../../i18n/i18n";
import { UserAccessSection } from "./UserAccessSection";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { profileService } from "../../api/profileService";
import { adminPermissionsService } from "../../api/adminPermissionsService";
import type { CompanyUserMembershipAdminDto } from "../api/membershipService";

vi.mock("../../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));
vi.mock("../../api/profileService", () => ({ profileService: { getPermissions: vi.fn() } }));
vi.mock("../../api/adminPermissionsService", () => ({
  adminPermissionsService: { getCatalog: vi.fn() },
}));

/** IAM-V1-F2 — pestaña «Accesos» de la ficha del usuario (solo lectura). */
const operative: CompanyUserMembershipAdminDto = {
  companyUserId: "cu-1",
  identityUserId: "iu-1",
  username: "ana",
  fullName: "Ana Perez",
  email: null,
  role: "User",
  isActive: true,
  profileId: "profile-1",
  profileName: "Cajero",
};

const CATALOG = {
  groups: [
    {
      code: "sales",
      labelKey: "Ventas",
      sortOrder: 1,
      categories: [
        {
          id: "c1",
          labelKey: "Operación",
          sortOrder: 1,
          items: [
            {
              id: "i1",
              labelKey: "Facturas",
              route: "/sales",
              permission: "sales.view",
              sortOrder: 1,
              actions: [
                { code: "sales.view", label: "Ver / Acceder", description: "", sortOrder: 0 },
                { code: "sales.create", label: "Crear", description: "", sortOrder: 1 },
              ],
            },
          ],
        },
      ],
    },
  ],
};

function mockPermissions(keys: string[]) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (k: string) => keys.includes(k),
    has: (k: string) => keys.includes(k),
    isAdminRole: false,
  });
}

function renderSection(membership: typeof operative) {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <UserAccessSection membership={membership} />
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  vi.mocked(adminPermissionsService.getCatalog).mockResolvedValue(CATALOG);
  vi.mocked(profileService.getPermissions).mockResolvedValue({
    profileId: "profile-1",
    items: [{ permissionKey: "sales.view", isAllowed: true }],
  });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("UserAccessSection", () => {
  it("Administrador de empresa: acceso total por su función, sin perfil ni consulta de permisos", () => {
    mockPermissions(["access.profiles.view"]);
    renderSection({ ...operative, role: "Admin", profileId: null, profileName: null });

    expect(screen.getByText("Administrador de empresa")).toBeTruthy();
    expect(screen.getByText(/Acceso total a esta empresa por su función/)).toBeTruthy();
    expect(profileService.getPermissions).not.toHaveBeenCalled();
  });

  it("operativo sin perfil: aviso explícito de que no tiene acceso", () => {
    mockPermissions(["access.profiles.view"]);
    renderSection({ ...operative, profileId: null, profileName: null });

    expect(screen.getByText(/no tiene acceso a ninguna pantalla/)).toBeTruthy();
    expect(profileService.getPermissions).not.toHaveBeenCalled();
  });

  it("operativo con perfil: muestra lo que concede su perfil y el enlace para gestionarlo", async () => {
    mockPermissions(["access.profiles.view"]);
    renderSection(operative);

    const summary = await screen.findByRole("region", { name: "Este perfil concede" });
    expect(summary.textContent).toContain("Facturas");
    expect(summary.textContent).toContain("Ver / Acceder");
    expect(summary.textContent).not.toContain("Crear");
    expect(
      screen.getByRole("link", { name: "Gestionar permisos del perfil" }).getAttribute("href"),
    ).toBe("/admin/permissions?profileId=profile-1");
  });

  it("sin access.profiles.view: solo el nombre del perfil, sin leer sus permisos", async () => {
    mockPermissions([]);
    renderSection(operative);

    expect(screen.getByText("Cajero")).toBeTruthy();
    await waitFor(() => expect(profileService.getPermissions).not.toHaveBeenCalled());
    expect(screen.queryByRole("region", { name: "Este perfil concede" })).toBeNull();
  });
});
