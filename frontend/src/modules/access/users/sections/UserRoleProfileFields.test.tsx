// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { useForm } from "react-hook-form";
import { I18nProvider } from "../../../../i18n/i18n";
import { UserRoleProfileFields } from "./UserRoleProfileFields";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import type { Profile } from "../../api/profileService";
import type { UserConfigFormValues } from "../../../../schemas/access/userConfigSchema";

vi.mock("../../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));

/** IAM-V1-F3 — empresas nuevas sin perfiles: estado vacío del formulario de Usuarios. */
const EMPTY_MESSAGE =
  "Primero cree un perfil y configure sus permisos para registrar usuarios operativos.";

function mockPermissions(keys: string[]) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (k: string) => keys.includes(k),
    has: (k: string) => keys.includes(k),
    isAdminRole: false,
  });
}

function Harness(props: {
  role: string;
  profiles: Profile[];
  profilesLoaded?: boolean;
}) {
  const { register, control, formState } = useForm<UserConfigFormValues>({
    defaultValues: { role: props.role, profileId: null } as Partial<UserConfigFormValues>,
  });
  return (
    <UserRoleProfileFields
      register={register}
      control={control}
      errors={formState.errors}
      profiles={props.profiles}
      profilesLoaded={props.profilesLoaded}
      disabled={false}
      allowNoProfile={false}
    />
  );
}

function renderFields(props: Parameters<typeof Harness>[0]) {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <Harness {...props} />
      </MemoryRouter>
    </I18nProvider>,
  );
}

const profile = { id: "p-1", name: "Cajero", isActive: true } as Profile;

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("UserRoleProfileFields — sin perfiles", () => {
  it("usuario operativo sin perfiles: muestra la indicación y el acceso a Perfiles si tiene permiso", () => {
    mockPermissions(["access.profiles.view"]);
    renderFields({ role: "User", profiles: [] });

    expect(screen.getByText(EMPTY_MESSAGE)).toBeTruthy();
    const link = screen.getByRole("link", { name: "Ir a Perfiles" });
    expect(link.getAttribute("href")).toBe("/admin/roles");
  });

  it("sin permiso sobre Perfiles: muestra la indicación pero no el enlace", () => {
    mockPermissions([]);
    renderFields({ role: "User", profiles: [] });

    expect(screen.getByText(EMPTY_MESSAGE)).toBeTruthy();
    expect(screen.queryByRole("link", { name: "Ir a Perfiles" })).toBeNull();
  });

  it("mientras los perfiles cargan no muestra el estado vacío", () => {
    mockPermissions(["access.profiles.view"]);
    renderFields({ role: "User", profiles: [], profilesLoaded: false });

    expect(screen.queryByText(EMPTY_MESSAGE)).toBeNull();
  });

  it("con perfiles existentes muestra el selector y no la indicación", () => {
    mockPermissions(["access.profiles.view"]);
    renderFields({ role: "User", profiles: [profile] });

    expect(screen.queryByText(EMPTY_MESSAGE)).toBeNull();
    expect(screen.getByRole("option", { name: "Cajero" })).toBeTruthy();
  });

  it("Administrador de empresa sin perfiles: no aplica perfil y no se bloquea", () => {
    mockPermissions([]);
    renderFields({ role: "Admin", profiles: [] });

    expect(screen.queryByText(EMPTY_MESSAGE)).toBeNull();
    expect(screen.getByText(/no aplica/i)).toBeTruthy();
  });
});
