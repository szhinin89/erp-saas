// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, screen, cleanup, waitFor, within, act, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../../i18n/i18n";
import { NavigationAccessContext, buildNavigationAccess } from "../../../../components/navigationAccess";
import type { MainMenuGroup } from "../../../../components/useAppLayoutNavigation";
import { useActiveBranchStore } from "../../../../store/activeBranchStore";
import type {
  CompanyOperationalReadiness,
  CompanyOperationalReadinessCashRegister,
} from "../../../../types/companyProfile";

// READINESS-2B-2 — la sección solo presenta el contrato del backend (2B-1): sucursal activa,
// cajas por punto de emisión y requisitos referenciados por requirementCodes[].

const getOperationalReadiness = vi.fn();
vi.mock("../api/companyProfileService", () => ({
  companyProfileService: {
    getOperationalReadiness: () => getOperationalReadiness(),
  },
}));
vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => true }),
}));

import { OperationalReadinessSection } from "./OperationalReadinessSection";

const MATRIZ = { id: "branch-matriz", name: "Matriz", isMainBranch: true };
const NORTE = { id: "branch-norte", name: "Norte", isMainBranch: false };

const MENU: MainMenuGroup[] = [
  {
    id: "treasury",
    label: "Tesorería",
    isActive: false,
    items: [{ id: "regs", to: "/treasury/cash/registers", label: "Cajas registradoras" }],
  },
  {
    id: "settings",
    label: "Configuración",
    isActive: false,
    items: [{ id: "company", to: "/settings/company", label: "Datos de la empresa" }],
  },
];

function register(
  overrides: Partial<CompanyOperationalReadinessCashRegister> &
    Pick<CompanyOperationalReadinessCashRegister, "code">,
): CompanyOperationalReadinessCashRegister {
  return {
    cashRegisterId: `id-${overrides.code}`,
    name: `Caja ${overrides.code}`,
    emissionPointId: "ep",
    establishmentCode: "001",
    emissionPointCode: "002",
    emissionType: "Physical",
    operationalStatus: "Ready",
    fiscalStatus: "NotVerified",
    requirementCodes: [],
    ...overrides,
  };
}

function readiness(overrides: Partial<CompanyOperationalReadiness> = {}): CompanyOperationalReadiness {
  return {
    branchId: MATRIZ.id,
    overallStatus: "Missing",
    canSell: true,
    canIssueElectronicInvoices: false,
    canUseInventory: true,
    canUseCashRegister: true,
    sections: [
      {
        code: "identity",
        status: "Ready",
        items: [
          {
            code: "identity.taxId",
            status: "Ready",
            severity: "Blocking",
            blockingAreas: [],
            actionTarget: "CompanyProfile",
          },
        ],
      },
      {
        code: "cashRegister",
        status: "Ready",
        items: [
          {
            code: "cashRegister.withEmissionPoint",
            status: "Ready",
            severity: "Blocking",
            blockingAreas: [],
            actionTarget: "CashRegisters",
          },
        ],
      },
      {
        code: "electronicInvoicing",
        status: "Missing",
        items: [
          {
            code: "electronicEmission.certificate",
            status: "Missing",
            severity: "Blocking",
            blockingAreas: ["ElectronicInvoicing"],
            actionTarget: "ElectronicInvoicingSettings",
          },
        ],
      },
    ],
    cashRegisters: [
      register({ code: "CAJA-01" }),
      register({
        code: "CAJA-02",
        emissionPointCode: "003",
        emissionType: "Electronic",
        operationalStatus: "Missing",
        fiscalStatus: "Pending",
        requirementCodes: ["electronicEmission.certificate"],
      }),
      register({
        code: "CAJA-03",
        emissionPointCode: "004",
        emissionType: "Electronic",
        operationalStatus: "Missing",
        fiscalStatus: "Pending",
        requirementCodes: ["electronicEmission.certificate"],
      }),
    ],
    ...overrides,
  };
}

function renderSection(menu: MainMenuGroup[] = MENU) {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <NavigationAccessContext.Provider value={buildNavigationAccess(menu, true)}>
          <OperationalReadinessSection />
        </NavigationAccessContext.Provider>
      </MemoryRouter>
    </I18nProvider>,
  );
}

function rowOf(code: string): HTMLElement {
  return screen.getByText(code).closest("tr") as HTMLElement;
}

beforeEach(() => {
  useActiveBranchStore.setState({ branch: MATRIZ });
});

afterEach(() => {
  cleanup();
  getOperationalReadiness.mockReset();
  useActiveBranchStore.setState({ branch: null });
});

describe("OperationalReadinessSection — sucursal activa", () => {
  it("sin sucursal activa pide seleccionarla y no consulta", () => {
    useActiveBranchStore.setState({ branch: null });
    renderSection();
    expect(screen.getByText(/Selecciona una sucursal/)).toBeTruthy();
    expect(getOperationalReadiness).not.toHaveBeenCalled();
  });

  it("muestra el nombre de la sucursal del diagnóstico", async () => {
    getOperationalReadiness.mockResolvedValue(readiness());
    renderSection();
    expect(await screen.findByText("Diagnóstico de la sucursal Matriz")).toBeTruthy();
  });

  it("al cambiar de sucursal vuelve a consultar y muestra la nueva", async () => {
    getOperationalReadiness.mockResolvedValueOnce(readiness());
    renderSection();
    await screen.findByText("CAJA-01");

    getOperationalReadiness.mockResolvedValueOnce(
      readiness({ branchId: NORTE.id, cashRegisters: [register({ code: "NORTE-01" })] }),
    );
    act(() => useActiveBranchStore.setState({ branch: NORTE }));

    expect(await screen.findByText("NORTE-01")).toBeTruthy();
    expect(screen.getByText("Diagnóstico de la sucursal Norte")).toBeTruthy();
    expect(screen.queryByText("CAJA-01")).toBeNull();
    expect(getOperationalReadiness).toHaveBeenCalledTimes(2);
  });

  it("nunca muestra una respuesta de otra sucursal", async () => {
    useActiveBranchStore.setState({ branch: NORTE });
    getOperationalReadiness.mockResolvedValue(readiness({ branchId: MATRIZ.id }));
    renderSection();
    await waitFor(() => expect(getOperationalReadiness).toHaveBeenCalled());
    await act(async () => {});
    expect(screen.queryByText("CAJA-01")).toBeNull();
    expect(screen.queryByText("Vender")).toBeNull();
  });

  it("al regresar de otra pantalla (nuevo montaje) vuelve a consultar", async () => {
    getOperationalReadiness.mockResolvedValue(readiness());
    const first = renderSection();
    await screen.findByText("CAJA-01");
    first.unmount();
    renderSection();
    await screen.findByText("CAJA-01");
    expect(getOperationalReadiness).toHaveBeenCalledTimes(2);
  });

  it("«Volver a verificar» vuelve a consultar", async () => {
    getOperationalReadiness.mockResolvedValue(readiness());
    renderSection();
    await screen.findByText("CAJA-01");
    fireEvent.click(screen.getByRole("button", { name: /Volver a verificar/ }));
    await waitFor(() => expect(getOperationalReadiness).toHaveBeenCalledTimes(2));
  });
});

describe("OperationalReadinessSection — cajas y puntos de emisión", () => {
  it("muestra caja, establecimiento-punto y modalidad", async () => {
    getOperationalReadiness.mockResolvedValue(readiness());
    renderSection();
    const row = rowOf(await screen.findByText("CAJA-02").then(() => "CAJA-02"));
    expect(within(row).getByText("Caja CAJA-02")).toBeTruthy();
    expect(within(row).getByText("001-003")).toBeTruthy();
    expect(within(row).getByText("Electrónica")).toBeTruthy();
    expect(within(rowOf("CAJA-01")).getByText("Física")).toBeTruthy();
  });

  it("distingue estado operativo y fiscal; NotVerified nunca se presenta como verificado", async () => {
    getOperationalReadiness.mockResolvedValue(
      readiness({
        cashRegisters: [
          register({ code: "CAJA-01" }),
          register({ code: "CAJA-02", emissionType: "Electronic", fiscalStatus: "Verified" }),
          register({
            code: "CAJA-03",
            emissionPointId: null,
            establishmentCode: null,
            emissionPointCode: null,
            emissionType: null,
            operationalStatus: "Missing",
            fiscalStatus: "NotApplicable",
            requirementCodes: ["cashRegister.withEmissionPoint"],
          }),
        ],
      }),
    );
    renderSection();
    await screen.findByText("CAJA-01");

    const physical = rowOf("CAJA-01");
    expect(within(physical).getByText("Listo")).toBeTruthy();
    expect(within(physical).getByText("No verificado")).toBeTruthy();
    expect(within(physical).queryByText("Verificado")).toBeNull();
    expect(within(physical).getByText(/La autorización del SRI y su uso excepcional no se verifican/)).toBeTruthy();

    expect(within(rowOf("CAJA-02")).getByText("Verificado")).toBeTruthy();

    const withoutPoint = rowOf("CAJA-03");
    expect(within(withoutPoint).getByText("Falta")).toBeTruthy();
    expect(within(withoutPoint).getByText("Sin punto de emisión")).toBeTruthy();
    expect(within(withoutPoint).getByText("No aplica")).toBeTruthy();
  });

  it("una caja correcta no oculta los pendientes de otra: cuenta listas y pendientes", async () => {
    getOperationalReadiness.mockResolvedValue(readiness());
    renderSection();
    await screen.findByText("CAJA-01");
    expect(screen.getByText("1 de 3")).toBeTruthy();
    expect(screen.getByText("2 de 3")).toBeTruthy();
    expect(screen.getByText(/2 de 3 cajas todavía tienen requisitos pendientes/)).toBeTruthy();
    // La capacidad sigue siendo la del backend: disponible aunque no todo esté listo.
    expect(screen.getByText(/no que toda la configuración esté completa/)).toBeTruthy();
  });

  it("sin cajas en la sucursal muestra el estado vacío", async () => {
    getOperationalReadiness.mockResolvedValue(readiness({ canSell: false, cashRegisters: [] }));
    renderSection();
    expect(await screen.findByText(/Esta sucursal no tiene cajas activas/)).toBeTruthy();
    expect(screen.queryByText(/todavía tienen requisitos pendientes/)).toBeNull();
  });
});

describe("OperationalReadinessSection — requisitos y destinos", () => {
  it("un requisito compartido aparece una sola vez e indica las cajas afectadas", async () => {
    getOperationalReadiness.mockResolvedValue(readiness());
    renderSection();
    await screen.findByText("CAJA-01");

    const items = document.querySelectorAll('[id="readiness-item-electronicEmission.certificate"]');
    expect(items).toHaveLength(1);
    const item = items[0] as HTMLElement;
    expect(within(item).getByText("Cajas afectadas: CAJA-02 (001-003), CAJA-03 (001-004)")).toBeTruthy();
    expect(within(item).getByText("Operaciones afectadas: Facturar electrónicamente")).toBeTruthy();

    // Cada caja enlaza al requisito, sin copiar su explicación.
    const links = document.querySelectorAll('a[href="#readiness-item-electronicEmission.certificate"]');
    expect(links).toHaveLength(2);
    expect(within(rowOf("CAJA-01")).getByText("Ninguno")).toBeTruthy();
  });

  it("enlaza al destino disponible en el menú autenticado con su nombre", async () => {
    getOperationalReadiness.mockResolvedValue(readiness());
    renderSection();
    await screen.findByText("CAJA-01");

    const item = document.getElementById("readiness-item-cashRegister.withEmissionPoint")!;
    const link = within(item).getByRole("link", { name: "Cajas registradoras" });
    expect(link.getAttribute("href")).toBe("/treasury/cash/registers");

    const identity = document.getElementById("readiness-item-identity.taxId")!;
    expect(
      within(identity).getByRole("link", { name: "Datos de la empresa" }).getAttribute("href"),
    ).toBe("/settings/company?tab=profile");
  });

  it("sin acceso al destino pide asistencia a un administrador y no muestra enlace", async () => {
    getOperationalReadiness.mockResolvedValue(readiness());
    renderSection();
    await screen.findByText("CAJA-01");

    const item = document.getElementById("readiness-item-electronicEmission.certificate")!;
    expect(within(item).queryByRole("link")).toBeNull();
    expect(within(item).getByText(/Pide a un administrador/)).toBeTruthy();
  });

  it("con el menú aún sin resolver no ofrece enlaces ni avisos de acceso", async () => {
    getOperationalReadiness.mockResolvedValue(readiness());
    render(
      <I18nProvider>
        <MemoryRouter>
          <NavigationAccessContext.Provider value={buildNavigationAccess(MENU, false)}>
            <OperationalReadinessSection />
          </NavigationAccessContext.Provider>
        </MemoryRouter>
      </I18nProvider>,
    );
    await screen.findByText("CAJA-01");
    const item = document.getElementById("readiness-item-cashRegister.withEmissionPoint")!;
    expect(within(item).queryByRole("link")).toBeNull();
    expect(screen.queryByText(/Pide a un administrador/)).toBeNull();
  });
});
