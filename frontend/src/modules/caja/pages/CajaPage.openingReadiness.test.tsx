// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { dictionaries, storageKey } from "../../../i18n/dictionaries";
import { CajaPage } from "./CajaPage";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { useAuthStore } from "../../../store/authStore";
import { useElectronicInvoicingStatusStore } from "../../../store/electronicInvoicingStatusStore";
import { NavigationAccessContext, buildNavigationAccess } from "../../../components/navigationAccess";
import type { MainMenuGroup } from "../../../components/useAppLayoutNavigation";
import { cajaService } from "../api/cajaService";
import type { CashRegisterDto, CashRegisterOpeningReadinessDto } from "../api/cajaService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { operationalPreferencesLookupFacade } from "../../configuracion/facades/operationalPreferencesLookupFacade";
import type { OperationalPreferencesDto } from "../../configuracion/facades/operationalPreferencesLookupFacade";
import { message } from "../../../lib/messages";

/**
 * READINESS-2B-3 — habilitación preventiva de cajas en la apertura: la pantalla solo presenta el
 * diagnóstico del backend para la caja seleccionada (su propio punto de emisión) y bloquea iniciar
 * la jornada con una caja no preparada, sin afectar a las demás. El backend vuelve a decidir al abrir.
 */

vi.mock("../components/CashFundingRequestsPendingNotice", () => ({
  CashFundingRequestsPendingNotice: () => null,
}));
vi.mock("../api/cajaService", () => ({
  cajaService: {
    getCashRegisters: vi.fn(),
    list: vi.fn(),
    getMy: vi.fn(),
    open: vi.fn(),
    getOpeningReadiness: vi.fn(),
  },
}));
vi.mock("../../configuracion/facades/operationalPreferencesLookupFacade", () => ({
  operationalPreferencesLookupFacade: { getPreferences: vi.fn() },
}));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));
vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

const dict = dictionaries.es;

function register(id: string, code: string, branchId = "branch-1"): CashRegisterDto {
  return {
    id, branchId, branchName: "Matriz", branchCode: "001", emissionPointId: `ep-${id}`,
    establishmentCode: "001", emissionPointCode: "002", emissionPointName: null, code, name: code,
    notes: null, isActive: true, hasHistory: false, accountingAccountId: null,
    defaultWarehouseId: "wh-1", defaultWarehouseCode: "B1", defaultWarehouseName: "Bodega",
    defaultCustomerId: null, defaultCustomerName: null, createdAt: "2026-07-01T00:00:00Z",
    updatedAt: null, emissionType: "Electronic", emissionPointIsActive: true,
  };
}

function readiness(
  cashRegisterId: string,
  overrides: Partial<CashRegisterOpeningReadinessDto> = {},
): CashRegisterOpeningReadinessDto {
  return {
    cashRegisterId, branchId: "branch-1", emissionPointId: `ep-${cashRegisterId}`,
    emissionType: "Electronic", canOpen: true, fiscalStatus: "Verified", requirements: [], ...overrides,
  };
}

function withoutCertificate(cashRegisterId: string, branchId = "branch-1"): CashRegisterOpeningReadinessDto {
  return readiness(cashRegisterId, {
    branchId,
    canOpen: false,
    fiscalStatus: "Pending",
    requirements: [
      { code: "electronicEmission.certificate", severity: "Required", actionTarget: "ElectronicInvoicingSettings" },
    ],
  });
}

const MENU: MainMenuGroup[] = [
  {
    id: "sri", label: "SRI", isActive: false,
    items: [{ id: "edoc", to: "/sri/configuration/electronic-invoicing", label: "Facturación electrónica" }],
  },
];

async function openTab(menu: MainMenuGroup[] = MENU) {
  render(
    <MemoryRouter>
      <NavigationAccessContext.Provider value={buildNavigationAccess(menu, true)}>
        <I18nProvider><CajaPage /></I18nProvider>
      </NavigationAccessContext.Provider>
    </MemoryRouter>,
  );
  fireEvent.click(await screen.findByRole("button", { name: new RegExp(dict["caja.session.open"]) }));
  await screen.findByText(dict["caja.openContext.operational"]);
}

const openButton = () => screen.getByRole("button", { name: dict["caja.session.open"] }) as HTMLButtonElement;

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.setItem(storageKey, "es");
  useElectronicInvoicingStatusStore.setState({ status: null, isLoaded: true, isLoading: false });
  useActiveBranchStore.setState({ branch: { id: "branch-1", name: "Matriz", isMainBranch: true } });
  useAuthStore.setState({
    user: { userId: "user-1", fullName: "Ana Perez", username: "ana", email: "ana@test.com", role: "User", tenantId: "tenant-1" },
    isAuthenticated: true,
    hasHydrated: true,
  });
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true, has: () => true, isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
  vi.mocked(operationalPreferencesLookupFacade.getPreferences).mockResolvedValue({} as OperationalPreferencesDto);
  vi.mocked(cajaService.list).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 25 });
  vi.mocked(cajaService.getMy).mockResolvedValue(null);
  vi.mocked(cajaService.getCashRegisters).mockResolvedValue([register("reg-1", "CAJA-01")]);
  vi.mocked(cajaService.getOpeningReadiness).mockImplementation(async (id) => readiness(id));
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(() => {
  cleanup();
  localStorage.removeItem(storageKey);
  useElectronicInvoicingStatusStore.getState().clear();
});

describe("CajaPage — habilitación preventiva de la caja", () => {
  it("caja electrónica preparada: flujo normal, sin avisos ni pasos adicionales", async () => {
    await openTab();
    await screen.findByText(dict["operationalReadiness.fiscalStatus.verified.label"]);
    expect(cajaService.getOpeningReadiness).toHaveBeenCalledWith("reg-1");
    expect(screen.queryByText(dict["caja.openReadiness.blocked"])).toBeNull();
    await waitFor(() => expect(openButton().disabled).toBe(false));
  });

  it("caja electrónica sin certificado: explica qué falta, por qué y dónde se corrige, y no abre", async () => {
    vi.mocked(cajaService.getOpeningReadiness).mockImplementation(async (id) => withoutCertificate(id));
    await openTab();
    await screen.findByText(dict["caja.openReadiness.blocked"]);
    expect(screen.getByText(dict["operationalReadiness.items.electronicEmission.certificate.label"])).toBeTruthy();
    expect(screen.getByText(dict["operationalReadiness.items.electronicEmission.certificate.missing"])).toBeTruthy();
    expect(screen.getByRole("link", { name: "Facturación electrónica" }).getAttribute("href"))
      .toBe("/sri/configuration/electronic-invoicing");
    expect(openButton().disabled).toBe(true);
    fireEvent.submit(openButton().closest("form")!);
    expect(cajaService.open).not.toHaveBeenCalled();
  });

  it("caja sin punto de emisión activo: requisito de la caja con su pantalla", async () => {
    vi.mocked(cajaService.getOpeningReadiness).mockImplementation(async (id) =>
      readiness(id, {
        canOpen: false, emissionPointId: null, emissionType: null, fiscalStatus: "NotApplicable",
        requirements: [{ code: "cashRegister.withEmissionPoint", severity: "Required", actionTarget: "CashRegisters" }],
      }));
    await openTab();
    await screen.findByText(dict["operationalReadiness.items.cashRegister.withEmissionPoint.label"]);
    expect(screen.getByText(dict["operationalReadiness.fiscalStatus.notapplicable.label"])).toBeTruthy();
    expect(openButton().disabled).toBe(true);
  });

  it("usuario sin acceso a la configuración: pide asistencia en vez de ofrecer el enlace", async () => {
    vi.mocked(cajaService.getOpeningReadiness).mockImplementation(async (id) => withoutCertificate(id));
    await openTab([]);
    await screen.findByText(dict["operationalReadiness.item.noAccess"]);
    expect(screen.queryByRole("link", { name: "Facturación electrónica" })).toBeNull();
    expect(openButton().disabled).toBe(true);
  });

  it("dos cajas de la misma sucursal con resultados distintos: solo se bloquea la no preparada", async () => {
    vi.mocked(cajaService.getCashRegisters).mockResolvedValue([register("reg-1", "CAJA-01"), register("reg-2", "CAJA-02")]);
    vi.mocked(cajaService.getOpeningReadiness).mockImplementation(async (id) =>
      id === "reg-2" ? withoutCertificate(id) : readiness(id));
    await openTab();
    await waitFor(() => expect(openButton().disabled).toBe(false));

    fireEvent.change(screen.getByRole("combobox"), { target: { value: "reg-2" } });
    await screen.findByText(dict["caja.openReadiness.blocked"]);
    expect(openButton().disabled).toBe(true);

    fireEvent.change(screen.getByRole("combobox"), { target: { value: "reg-1" } });
    await waitFor(() => expect(screen.queryByText(dict["caja.openReadiness.blocked"])).toBeNull());
    expect(openButton().disabled).toBe(false);
  });

  it("cambio de sucursal: recarga las cajas y diagnostica la caja de la nueva sucursal", async () => {
    await openTab();
    await screen.findByText(dict["operationalReadiness.fiscalStatus.verified.label"]);

    vi.mocked(cajaService.getCashRegisters).mockResolvedValue([register("reg-n1", "CAJA-N1", "branch-2")]);
    vi.mocked(cajaService.getOpeningReadiness).mockImplementation(async (id) => withoutCertificate(id, "branch-2"));
    act(() => {
      useActiveBranchStore.setState({ branch: { id: "branch-2", name: "Norte", isMainBranch: false } });
    });

    await waitFor(() => expect(cajaService.getOpeningReadiness).toHaveBeenCalledWith("reg-n1"));
    await screen.findByText(dict["caja.openReadiness.blocked"]);
    expect(cajaService.getCashRegisters).toHaveBeenCalledTimes(2);
    expect(openButton().disabled).toBe(true);
  });

  it("descarta un diagnóstico de otra sucursal (no se muestra con datos ajenos)", async () => {
    vi.mocked(cajaService.getOpeningReadiness).mockImplementation(async (id) => withoutCertificate(id, "branch-x"));
    await openTab();
    await waitFor(() => expect(cajaService.getOpeningReadiness).toHaveBeenCalled());
    expect(screen.queryByText(dict["caja.openReadiness.blocked"])).toBeNull();
    expect(screen.queryByText(dict["operationalReadiness.items.electronicEmission.certificate.label"])).toBeNull();
  });

  it("caja física: puede abrir pero nunca se presenta como autorizada fiscalmente", async () => {
    vi.mocked(cajaService.getCashRegisters).mockResolvedValue([{ ...register("reg-1", "CAJA-01"), emissionType: "Physical" }]);
    vi.mocked(cajaService.getOpeningReadiness).mockImplementation(async (id) =>
      readiness(id, { emissionType: "Physical", fiscalStatus: "NotVerified" }));
    await openTab();
    await screen.findByText(dict["operationalReadiness.fiscalStatus.notverified.label"]);
    expect(screen.getByText(dict["operationalReadiness.fiscalStatus.notverified.detail"])).toBeTruthy();
    expect(screen.queryByText(dict["operationalReadiness.fiscalStatus.verified.label"])).toBeNull();
    await waitFor(() => expect(openButton().disabled).toBe(false));
  });

  it("configuración que cambia antes de abrir: el rechazo del backend vuelve a diagnosticar la caja", async () => {
    vi.mocked(cajaService.open).mockRejectedValue(new Error("SRI_EMISSION_REQUIREMENTS_MISSING"));
    await openTab();
    await waitFor(() => expect(openButton().disabled).toBe(false));

    vi.mocked(cajaService.getOpeningReadiness).mockImplementation(async (id) => withoutCertificate(id));
    fireEvent.submit(openButton().closest("form")!);

    await screen.findByText(dict["caja.openReadiness.blocked"]);
    expect(cajaService.open).toHaveBeenCalledTimes(1);
    expect(cajaService.getOpeningReadiness).toHaveBeenCalledTimes(2);
    expect(openButton().disabled).toBe(true);
  });

  it("si el diagnóstico no se puede consultar, avisa y deja que el backend decida", async () => {
    vi.mocked(cajaService.getOpeningReadiness).mockRejectedValue(new Error("network"));
    await openTab();
    await screen.findByText(dict["caja.openReadiness.checkError"]);
    await waitFor(() => expect(openButton().disabled).toBe(false));
  });
});
