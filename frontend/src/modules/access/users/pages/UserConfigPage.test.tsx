// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  render,
  screen,
  waitFor,
  fireEvent,
  cleanup,
} from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { I18nProvider } from "../../../../i18n/i18n";
import { useAuthStore } from "../../../../store/authStore";
import { UserConfigPage } from "./UserConfigPage";
import { membershipService } from "../api/membershipService";
import { branchAssignmentService } from "../api/branchAssignmentService";
import { companyUserPreferencesService } from "../../api/companyUserPreferencesService";
import { profileService } from "../../api/profileService";
import { branchLookupFacade } from "../../../branches/facades/branchLookupFacade";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { message } from "../../../../lib/messages";

const navigateMock = vi.fn();

vi.mock("react-router-dom", async () => {
  const actual =
    await vi.importActual<typeof import("react-router-dom")>(
      "react-router-dom",
    );
  return { ...actual, useNavigate: () => navigateMock };
});

vi.mock("../api/membershipService", () => ({
  MEMBERSHIP_ROLES: ["Admin", "User"],
  membershipService: {
    list: vi.fn(),
    upsertMembership: vi.fn(),
    revokeMembership: vi.fn(),
    createSystemUser: vi.fn(),
    lookupUserByUsername: vi.fn(),
  },
}));

vi.mock("../api/branchAssignmentService", () => ({
  branchAssignmentService: {
    getMembershipBranches: vi.fn(),
    updateMembershipBranches: vi.fn(),
  },
}));

vi.mock("../../api/companyUserPreferencesService", () => ({
  COMPANY_USER_LOGIN_MODES: ["AskBranch", "DirectToDefault"],
  companyUserPreferencesService: {
    get: vi.fn(),
    update: vi.fn(),
  },
}));

vi.mock("../../api/profileService", () => ({
  profileService: {
    list: vi.fn(),
  },
}));

vi.mock("../../../branches/facades/branchLookupFacade", () => ({
  branchLookupFacade: {
    list: vi.fn(),
  },
}));

vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const membershipRow = {
  companyUserId: "membership-1",
  identityUserId: "identity-1",
  username: "ana",
  fullName: "Ana Perez",
  email: "ana@test.com",
  role: "User",
  isActive: true,
  profileId: "profile-1",
  profileName: "Ventas",
};

const twoBranchesCatalog = [
  {
    id: "branch-1",
    name: "Matriz",
    code: "001",
    address: "Av. Principal",
    countryId: null,
    provinceId: null,
    cantonId: null,
    parishId: null,
    phone: null,
    email: null,
    managerName: null,
    isActive: true,
    isMainBranch: true,
  },
  {
    id: "branch-2",
    name: "Sucursal Norte",
    code: "002",
    address: "Av. Norte",
    countryId: null,
    provinceId: null,
    cantonId: null,
    parishId: null,
    phone: null,
    email: null,
    managerName: null,
    isActive: true,
    isMainBranch: false,
  },
];

function setUserRole(role: string | null) {
  useAuthStore.setState({
    user:
      role === null
        ? null
        : {
            userId: "user-1",
            fullName: "Test User",
            username: "test.user",
            email: "test@example.com",
            role,
            tenantId: "tenant-1",
          },
    isAuthenticated: role !== null,
    hasHydrated: true,
  });
}

function renderAt(path: string, state?: unknown) {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[{ pathname: path, state }]}>
        <Routes>
          <Route path="/access/users/new" element={<UserConfigPage />} />
          <Route path="/access/users/:userId" element={<UserConfigPage />} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

/**
 * `getByRole('combobox', { name })`/`getByLabelText` no resuelven de forma confiable el nombre
 * accesible de un <select> cuando el <label> (ZHField) envuelve también las <option> — el
 * cómputo de nombre termina incluyendo el texto de las opciones. Se consulta por atributo `name`
 * del formulario en su lugar, que es estable y no depende de ese cálculo.
 */
function selectByFieldName(
  container: HTMLElement,
  name: string,
): HTMLSelectElement {
  const el = container.querySelector(`select[name="${name}"]`);
  if (!el) throw new Error(`select[name="${name}"] no encontrado`);
  return el as HTMLSelectElement;
}

beforeEach(() => {
  vi.clearAllMocks();
  navigateMock.mockReset();
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: true,
  });
  vi.mocked(membershipService.list).mockResolvedValue([membershipRow]);
  vi.mocked(membershipService.upsertMembership)
    .mockReset()
    .mockResolvedValue({});
  vi.mocked(membershipService.createSystemUser)
    .mockReset()
    .mockResolvedValue({ identityUserId: "identity-2", username: "nuevo" });
  vi.mocked(membershipService.lookupUserByUsername).mockReset();
  vi.mocked(branchAssignmentService.getMembershipBranches).mockResolvedValue({
    companyUserId: membershipRow.companyUserId,
    branches: [
      { branchId: "branch-1", branchName: "Matriz", authorized: true },
      { branchId: "branch-2", branchName: "Sucursal Norte", authorized: false },
    ],
  });
  vi.mocked(branchAssignmentService.updateMembershipBranches)
    .mockReset()
    .mockResolvedValue({
      companyUserId: membershipRow.companyUserId,
      branches: [],
    });
  vi.mocked(companyUserPreferencesService.get).mockResolvedValue({
    companyUserId: membershipRow.companyUserId,
    defaultBranchId: "branch-1",
    loginMode: "AskBranch",
  });
  vi.mocked(companyUserPreferencesService.update)
    .mockReset()
    .mockResolvedValue({
      companyUserId: membershipRow.companyUserId,
      defaultBranchId: null,
      loginMode: "AskBranch",
    });
  vi.mocked(profileService.list).mockResolvedValue([
    { id: "profile-1", name: "Ventas", description: null, isActive: true },
  ]);
  vi.mocked(branchLookupFacade.list).mockResolvedValue(twoBranchesCatalog);
  vi.mocked(message.confirm).mockResolvedValue(true);
  setUserRole("Admin");
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

describe("UserConfigPage — un solo formulario, un solo botón de guardar", () => {
  it('en modo edición muestra las 4 secciones y un único botón "Guardar configuración"', async () => {
    renderAt("/access/users/membership-1");

    await waitFor(() =>
      expect(
        (screen.getByLabelText("Username") as HTMLInputElement).value,
      ).toBe("ana"),
    );

    expect(
      screen.getByRole("tab", { name: "Información General" }),
    ).toBeTruthy();
    expect(
      screen.getByRole("tab", { name: "Acceso a sucursales" }),
    ).toBeTruthy();
    expect(
      screen.getByRole("tab", { name: "Preferencias de acceso" }),
    ).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Seguridad" })).toBeTruthy();
    expect(
      screen.getAllByRole("button", { name: "Guardar configuración" }),
    ).toHaveLength(1);
  });

  it("username no es editable en modo edición", async () => {
    renderAt("/access/users/membership-1");
    await waitFor(() =>
      expect(
        (screen.getByLabelText("Username") as HTMLInputElement).disabled,
      ).toBe(true),
    );
  });

  it("cambiar de tab no pierde los valores ya cargados (un solo useForm, no se desmonta el estado)", async () => {
    renderAt("/access/users/membership-1");
    await waitFor(() =>
      expect(
        (screen.getByLabelText("Username") as HTMLInputElement).value,
      ).toBe("ana"),
    );

    fireEvent.click(screen.getByRole("tab", { name: "Acceso a sucursales" }));
    await waitFor(() => expect(screen.getByText("Matriz")).toBeTruthy());
    // Matriz ya viene autorizada; se marca también Sucursal Norte.
    const toggles = screen.getAllByRole("switch");
    fireEvent.click(toggles[1]);

    fireEvent.click(screen.getByRole("tab", { name: "Información General" }));
    await waitFor(() =>
      expect(
        (screen.getByLabelText("Username") as HTMLInputElement).value,
      ).toBe("ana"),
    );

    fireEvent.click(screen.getByRole("tab", { name: "Acceso a sucursales" }));
    await waitFor(() => {
      const refreshedToggles = screen.getAllByRole(
        "switch",
      ) as HTMLInputElement[];
      expect(
        refreshedToggles.every(
          (el) => el.getAttribute("aria-checked") === "true",
        ),
      ).toBe(true);
    });

    // Solo un fetch de autorizaciones — no se repite el fetch al volver a visitar la tab.
    expect(branchAssignmentService.getMembershipBranches).toHaveBeenCalledTimes(
      1,
    );
  });

  it("guardar en modo edición llama a upsertMembership, updateMembershipBranches y preferences.update, muestra un solo toast y vuelve a la lista", async () => {
    renderAt("/access/users/membership-1", { from: "/access/users?q=ana" });
    await waitFor(() =>
      expect(
        (screen.getByLabelText("Username") as HTMLInputElement).value,
      ).toBe("ana"),
    );

    fireEvent.click(
      screen.getByRole("button", { name: "Guardar configuración" }),
    );

    await waitFor(() => {
      expect(membershipService.upsertMembership).toHaveBeenCalledWith({
        username: "ana",
        role: "User",
        profileId: "profile-1",
      });
      expect(
        branchAssignmentService.updateMembershipBranches,
      ).toHaveBeenCalledWith("membership-1", ["branch-1"]);
      expect(companyUserPreferencesService.update).toHaveBeenCalledWith(
        "membership-1",
        { loginMode: "AskBranch", defaultBranchId: "branch-1" },
      );
      expect(message.success).toHaveBeenCalledWith(
        "Usuario configurado correctamente.",
      );
      expect(navigateMock).toHaveBeenCalledWith("/access/users?q=ana");
    });
  });
});

describe("UserConfigPage — alta de usuario nuevo sin paso intermedio", () => {
  it('el botón "Guardar configuración" está deshabilitado hasta resolver el username', async () => {
    renderAt("/access/users/new");
    await waitFor(() => expect(screen.getByLabelText("Username")).toBeTruthy());

    expect(
      (
        screen.getByRole("button", {
          name: "Guardar configuración",
        }) as HTMLButtonElement
      ).disabled,
    ).toBe(true);
  });

  it("username inexistente revela los campos de alta y guardar hace un único flujo completo (crear + sucursales + preferencias)", async () => {
    vi.mocked(membershipService.lookupUserByUsername).mockResolvedValue({
      identityUserExists: false,
      fullName: null,
      membership: null,
    });
    vi.mocked(membershipService.list).mockResolvedValue([
      {
        ...membershipRow,
        companyUserId: "new-membership-id",
        username: "nuevo",
      },
    ]);

    renderAt("/access/users/new");
    await waitFor(() => expect(screen.getByLabelText("Username")).toBeTruthy());

    fireEvent.change(screen.getByLabelText("Username"), {
      target: { value: "nuevo" },
    });
    fireEvent.blur(screen.getByLabelText("Username"));

    await waitFor(() => expect(screen.getByLabelText("Nombre")).toBeTruthy());
    expect(
      (
        screen.getByRole("button", {
          name: "Guardar configuración",
        }) as HTMLButtonElement
      ).disabled,
    ).toBe(false);

    fireEvent.change(screen.getByLabelText("Nombre"), {
      target: { value: "Nuevo" },
    });
    fireEvent.change(screen.getByLabelText("Apellido"), {
      target: { value: "Usuario" },
    });
    // El campo tiene un "hint" adicional dentro del mismo <label> (RHF/ZHField), por eso el
    // match exacto de getByLabelText no aplica — se usa un prefijo con regex.
    fireEvent.change(screen.getByLabelText(/^Contraseña inicial/), {
      target: { value: "Passw0rd" },
    });

    fireEvent.click(screen.getByRole("tab", { name: "Acceso a sucursales" }));
    await waitFor(() => expect(screen.getByText("Matriz")).toBeTruthy());
    fireEvent.click(screen.getAllByRole("switch")[0]);

    fireEvent.click(
      screen.getByRole("button", { name: "Guardar configuración" }),
    );

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalled();
      expect(membershipService.createSystemUser).toHaveBeenCalledWith(
        expect.objectContaining({
          username: "nuevo",
          firstName: "Nuevo",
          lastName: "Usuario",
          password: "Passw0rd",
        }),
      );
      expect(
        branchAssignmentService.updateMembershipBranches,
      ).toHaveBeenCalledWith("new-membership-id", ["branch-1"]);
      expect(message.success).toHaveBeenCalled();
      expect(navigateMock).toHaveBeenCalledWith("/access/users");
    });
  });

  it("CRITICAL-CONFIRMATIONS-CLEANUP-07: si falla la búsqueda del usuario recién creado, no muestra falso éxito", async () => {
    // Antes de la corrección: este catch estaba vacío y el flujo caía directo a
    // message.success("Usuario configurado correctamente.") aunque sucursales/preferencias
    // nunca se hubieran intentado guardar (resolvedCompanyUserId se quedaba en null).
    vi.mocked(membershipService.lookupUserByUsername).mockResolvedValue({
      identityUserExists: false,
      fullName: null,
      membership: null,
    });
    vi.mocked(membershipService.list).mockRejectedValue({
      isAxiosError: true,
      response: { status: 500, data: { message: { user: "Error interno." } } },
    });

    renderAt("/access/users/new");
    await waitFor(() => expect(screen.getByLabelText("Username")).toBeTruthy());

    fireEvent.change(screen.getByLabelText("Username"), {
      target: { value: "nuevo" },
    });
    fireEvent.blur(screen.getByLabelText("Username"));

    await waitFor(() => expect(screen.getByLabelText("Nombre")).toBeTruthy());
    fireEvent.change(screen.getByLabelText("Nombre"), {
      target: { value: "Nuevo" },
    });
    fireEvent.change(screen.getByLabelText("Apellido"), {
      target: { value: "Usuario" },
    });
    fireEvent.change(screen.getByLabelText(/^Contraseña inicial/), {
      target: { value: "Passw0rd" },
    });

    fireEvent.click(
      screen.getByRole("button", { name: "Guardar configuración" }),
    );

    await waitFor(() => {
      expect(membershipService.createSystemUser).toHaveBeenCalled();
      expect(message.error).toHaveBeenCalledWith(
        "El usuario se guardó, pero algunas secciones no se pudieron actualizar. Revísalas antes de continuar.",
      );
    });
    expect(message.success).not.toHaveBeenCalled();
    // No navega con falso éxito: al no resolverse companyUserId, sucursales/preferencias no se
    // intentaron guardar y el usuario se queda viendo el error, no la lista.
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it("si se cancela la confirmación de creación, no llama createSystemUser", async () => {
    vi.mocked(membershipService.lookupUserByUsername).mockResolvedValue({
      identityUserExists: false,
      fullName: null,
      membership: null,
    });
    vi.mocked(message.confirm).mockResolvedValue(false);

    renderAt("/access/users/new");
    await waitFor(() => expect(screen.getByLabelText("Username")).toBeTruthy());

    fireEvent.change(screen.getByLabelText("Username"), {
      target: { value: "nuevo" },
    });
    fireEvent.blur(screen.getByLabelText("Username"));

    await waitFor(() => expect(screen.getByLabelText("Nombre")).toBeTruthy());
    fireEvent.change(screen.getByLabelText("Nombre"), {
      target: { value: "Nuevo" },
    });
    fireEvent.change(screen.getByLabelText("Apellido"), {
      target: { value: "Usuario" },
    });
    fireEvent.change(screen.getByLabelText(/^Contraseña inicial/), {
      target: { value: "Passw0rd" },
    });

    fireEvent.click(
      screen.getByRole("button", { name: "Guardar configuración" }),
    );

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(membershipService.createSystemUser).not.toHaveBeenCalled();
    expect(navigateMock).not.toHaveBeenCalledWith("/access/users");
  });

  it("username que ya es miembro activo muestra un mensaje claro y un link, no un formulario", async () => {
    vi.mocked(membershipService.lookupUserByUsername).mockResolvedValue({
      identityUserExists: true,
      fullName: "Ana Perez",
      membership: {
        companyUserMembershipId: "membership-1",
        isActive: true,
        role: "User",
        profileId: "profile-1",
      },
    });

    renderAt("/access/users/new");
    await waitFor(() => expect(screen.getByLabelText("Username")).toBeTruthy());

    fireEvent.change(screen.getByLabelText("Username"), {
      target: { value: "ana" },
    });
    fireEvent.blur(screen.getByLabelText("Username"));

    await waitFor(() =>
      expect(screen.getByText(/ya forma parte de tu equipo/)).toBeTruthy(),
    );
    expect(
      screen.getByRole("link", {
        name: "Ir a la configuración de este usuario",
      }),
    ).toBeTruthy();
    expect(screen.queryByLabelText("Nombre")).toBeNull();
    expect(
      (
        screen.getByRole("button", {
          name: "Guardar configuración",
        }) as HTMLButtonElement
      ).disabled,
    ).toBe(true);
  });
});

describe("UserConfigPage — reglas de negocio de sucursales/preferencias", () => {
  it("revocar la sucursal por defecto limpia el campo y muestra la nota, sin llegar a un 422", async () => {
    // Tres sucursales autorizadas (Matriz es la default) — al revocar Matriz deben quedar dos
    // autorizadas (no una sola), para aislar esta regla de la de auto-selección de "1 sola".
    vi.mocked(branchLookupFacade.list).mockResolvedValue([
      ...twoBranchesCatalog,
      {
        id: "branch-3",
        name: "Sucursal Sur",
        code: "003",
        address: "Av. Sur",
        countryId: null,
        provinceId: null,
        cantonId: null,
        parishId: null,
        phone: null,
        email: null,
        managerName: null,
        isActive: true,
        isMainBranch: false,
      },
    ]);
    vi.mocked(branchAssignmentService.getMembershipBranches).mockResolvedValue({
      companyUserId: membershipRow.companyUserId,
      branches: [
        { branchId: "branch-1", branchName: "Matriz", authorized: true },
        {
          branchId: "branch-2",
          branchName: "Sucursal Norte",
          authorized: true,
        },
        { branchId: "branch-3", branchName: "Sucursal Sur", authorized: true },
      ],
    });

    const { container } = renderAt("/access/users/membership-1");
    await waitFor(() =>
      expect(
        (screen.getByLabelText("Username") as HTMLInputElement).value,
      ).toBe("ana"),
    );

    fireEvent.click(screen.getByRole("tab", { name: "Acceso a sucursales" }));
    await waitFor(() => expect(screen.getByText("Matriz")).toBeTruthy());
    // Matriz (autorizada, es la default) se desmarca; las otras dos siguen autorizadas.
    fireEvent.click(screen.getAllByRole("switch")[0]);

    fireEvent.click(
      screen.getByRole("tab", { name: "Preferencias de acceso" }),
    );
    await waitFor(() => {
      expect(
        screen.getByText(
          /Se quitó la sucursal por defecto porque ya no está autorizada/,
        ),
      ).toBeTruthy();
      expect(selectByFieldName(container, "defaultBranchId").value).toBe("");
    });
  });

  it("una sola sucursal autorizada autoselecciona la sucursal por defecto", async () => {
    vi.mocked(branchAssignmentService.getMembershipBranches).mockResolvedValue({
      companyUserId: membershipRow.companyUserId,
      branches: [
        { branchId: "branch-1", branchName: "Matriz", authorized: false },
        {
          branchId: "branch-2",
          branchName: "Sucursal Norte",
          authorized: false,
        },
      ],
    });
    vi.mocked(companyUserPreferencesService.get).mockResolvedValue({
      companyUserId: membershipRow.companyUserId,
      defaultBranchId: null,
      loginMode: "AskBranch",
    });

    const { container } = renderAt("/access/users/membership-1");
    await waitFor(() =>
      expect(
        (screen.getByLabelText("Username") as HTMLInputElement).value,
      ).toBe("ana"),
    );

    fireEvent.click(screen.getByRole("tab", { name: "Acceso a sucursales" }));
    await waitFor(() => expect(screen.getByText("Matriz")).toBeTruthy());
    fireEvent.click(screen.getAllByRole("switch")[0]);

    fireEvent.click(
      screen.getByRole("tab", { name: "Preferencias de acceso" }),
    );
    await waitFor(() =>
      expect(selectByFieldName(container, "defaultBranchId").value).toBe(
        "branch-1",
      ),
    );
  });

  it("ZH-IAM-COMPANY-USER-PREFERENCES-VALIDATION-01: revocar la sucursal por defecto y guardar SIN volver a Preferencias no manda la branch vieja", async () => {
    // Reproduce el 422 real: defaultBranchId="branch-1" cargado (válido en ese momento), el
    // usuario revoca branch-1 desde "Acceso a sucursales" y pulsa "Guardar configuración"
    // directamente desde esa misma pestaña — sin volver a "Preferencias", que es la única que
    // desmonta/remonta y corre el efecto que limpia defaultBranchId reactivamente. El fix
    // normaliza el payload en el propio submit contra authorizedBranchIds, sin depender de que
    // esa sección se haya montado.
    vi.mocked(companyUserPreferencesService.get).mockResolvedValue({
      companyUserId: membershipRow.companyUserId,
      defaultBranchId: "branch-1",
      loginMode: "AskBranch",
    });

    renderAt("/access/users/membership-1");
    await waitFor(() =>
      expect(
        (screen.getByLabelText("Username") as HTMLInputElement).value,
      ).toBe("ana"),
    );

    fireEvent.click(screen.getByRole("tab", { name: "Acceso a sucursales" }));
    await waitFor(() => expect(screen.getByText("Matriz")).toBeTruthy());
    // Matriz (branch-1) es la única autorizada y la default cargada — se revoca.
    fireEvent.click(screen.getAllByRole("switch")[0]);

    // Guarda directamente desde "Acceso a sucursales", nunca visita "Preferencias" de nuevo.
    fireEvent.click(
      screen.getByRole("button", { name: "Guardar configuración" }),
    );

    await waitFor(() => {
      expect(
        branchAssignmentService.updateMembershipBranches,
      ).toHaveBeenCalledWith("membership-1", []);
      expect(companyUserPreferencesService.update).toHaveBeenCalledWith(
        "membership-1",
        { loginMode: "AskBranch", defaultBranchId: null },
      );
    });
  });

  it("ZH-IAM-COMPANY-USER-PREFERENCES-VALIDATION-01: en DirectToDefault, revocar la única sucursal bloquea el submit en el propio cliente (Zod) sin llegar al backend", async () => {
    // Complementa el test anterior: en DirectToDefault, userConfigSchema.superRefine SÍ valida
    // defaultBranchId contra authorizedBranchIds en cada submit (independiente de qué tab esté
    // montada), así que este caso nunca llega a generar el 422 — RHF simplemente no invoca
    // onSubmit. El bug real (y lo que corrige este fix) es exclusivo del modo AskBranch, que ese
    // mismo superRefine no valida (ver test anterior).
    vi.mocked(companyUserPreferencesService.get).mockResolvedValue({
      companyUserId: membershipRow.companyUserId,
      defaultBranchId: "branch-1",
      loginMode: "DirectToDefault",
    });

    renderAt("/access/users/membership-1");
    await waitFor(() =>
      expect(
        (screen.getByLabelText("Username") as HTMLInputElement).value,
      ).toBe("ana"),
    );

    fireEvent.click(screen.getByRole("tab", { name: "Acceso a sucursales" }));
    await waitFor(() => expect(screen.getByText("Matriz")).toBeTruthy());
    fireEvent.click(screen.getAllByRole("switch")[0]);

    fireEvent.click(
      screen.getByRole("button", { name: "Guardar configuración" }),
    );

    // Da tiempo a que un submit exitoso (si el bloqueo fallara) dispare sus llamadas.
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(branchAssignmentService.updateMembershipBranches).not.toHaveBeenCalled();
    expect(companyUserPreferencesService.update).not.toHaveBeenCalled();
  });

  it("cero sucursales autorizadas fuerza AskBranch, deshabilita el select y muestra el mensaje explicativo", async () => {
    vi.mocked(branchAssignmentService.getMembershipBranches).mockResolvedValue({
      companyUserId: membershipRow.companyUserId,
      branches: [
        { branchId: "branch-1", branchName: "Matriz", authorized: false },
        {
          branchId: "branch-2",
          branchName: "Sucursal Norte",
          authorized: false,
        },
      ],
    });
    vi.mocked(companyUserPreferencesService.get).mockResolvedValue({
      companyUserId: membershipRow.companyUserId,
      defaultBranchId: null,
      loginMode: "AskBranch",
    });

    const { container } = renderAt("/access/users/membership-1");
    await waitFor(() =>
      expect(
        (screen.getByLabelText("Username") as HTMLInputElement).value,
      ).toBe("ana"),
    );

    fireEvent.click(
      screen.getByRole("tab", { name: "Preferencias de acceso" }),
    );
    await waitFor(() => {
      expect(
        screen.getByText(/no tiene sucursales autorizadas todavía/),
      ).toBeTruthy();
      expect(selectByFieldName(container, "loginMode").disabled).toBe(true);
    });
  });
});

describe("UserConfigPage — cancelar", () => {
  it("sin cambios, Cancelar navega directo sin preguntar", async () => {
    renderAt("/access/users/membership-1", { from: "/access/users" });
    await waitFor(() =>
      expect(
        (screen.getByLabelText("Username") as HTMLInputElement).value,
      ).toBe("ana"),
    );

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(message.confirm).not.toHaveBeenCalled();
    expect(navigateMock).toHaveBeenCalledWith("/access/users");
  });

  it("con cambios sin guardar, Cancelar pide confirmación antes de navegar", async () => {
    const { container } = renderAt("/access/users/membership-1", {
      from: "/access/users",
    });
    await waitFor(() =>
      expect(
        (screen.getByLabelText("Username") as HTMLInputElement).value,
      ).toBe("ana"),
    );

    fireEvent.change(selectByFieldName(container, "role"), {
      target: { value: "Admin" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalled();
      expect(navigateMock).toHaveBeenCalledWith("/access/users");
    });
  });
});

describe("UserConfigPage — permisos", () => {
  it("sin acceso renderiza NoAccessPage", async () => {
    vi.mocked(usePermissionsUi).mockReturnValue({
      canShow: () => false,
      has: () => false,
      isAdminRole: false,
    });

    renderAt("/access/users/new");

    expect(
      screen.getAllByText("No tienes acceso a esta pantalla.").length,
    ).toBeGreaterThan(0);
  });
});
