// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  renderHook,
  waitFor,
  act,
  render,
  screen,
  cleanup,
} from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { storageKey } from "../../../i18n/dictionaries";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { useAuthStore } from "../../../store/authStore";
import { cajaService } from "../api/cajaService";
import type {
  CashRegisterDto,
  CashSessionDto,
} from "../api/cajaService";
import { useCajaPage } from "./useCajaPage";
import { message } from "../../../lib/messages";
import { operationalPreferencesLookupFacade } from "../../configuracion/facades/operationalPreferencesLookupFacade";
import type { OperationalPreferencesDto } from "../../configuracion/facades/operationalPreferencesLookupFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useElectronicInvoicingStatusStore } from "../../../store/electronicInvoicingStatusStore";

// ZH-CASH-FUNDING-REQUEST-UI-FINAL-02E-EF — el aviso de solicitudes pendientes tiene su propio test
// (CashFundingRequestsPendingNotice.test.tsx); aquí se aísla para no depender del Router.
vi.mock("../components/CashFundingRequestsPendingNotice", () => ({
  CashFundingRequestsPendingNotice: () => null,
}));

vi.mock("../api/cajaService", () => ({
  cajaService: {
    getCashRegisters: vi.fn(),
    list: vi.fn(),
    getMy: vi.fn(),
    getById: vi.fn(),
    open: vi.fn(),
    close: vi.fn(),
    recordMovement: vi.fn(),
    getCollectionSummary: vi.fn(),
    getCashMovementReasons: vi.fn(),
  },
}));

vi.mock("../../configuracion/facades/operationalPreferencesLookupFacade", () => ({
  operationalPreferencesLookupFacade: {
    getPreferences: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

/** TREASURY-CASH-MANUAL-MOVEMENTS-PERMISSION-06 — por defecto el usuario de prueba tiene todos
 * los permisos ("caja.record" incluido); los tests que necesiten simular su ausencia llaman a
 * `grantPermissions([...])` con una lista explícita. */
function grantPermissions(granted: string[] | "all" = "all") {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (key: string) => granted === "all" || granted.includes(key),
    has: () => true,
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
}

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

/** Renderiza el `message: ReactNode` que recibió la última llamada a message.confirm, para
 * verificar su contenido (resumen de caja/usuario/monto, esperado/contado/diferencia, etc.) sin
 * depender de que sea un string plano. */
function renderLastConfirmMessage() {
  const calls = vi.mocked(message.confirm).mock.calls;
  render(<>{calls[calls.length - 1][0].message}</>);
}

const registers: CashRegisterDto[] = [
  {
    id: "reg-1",
    branchId: "branch-1",
    branchName: "Matriz",
    branchCode: "001",
    emissionPointId: "ep-1",
    establishmentCode: "001",
    emissionPointCode: "001",
    emissionPointName: null,
    code: "CAJA-01",
    name: "Caja Principal",
    notes: null,
    isActive: true,
    hasHistory: false,
    accountingAccountId: null,
    defaultWarehouseId: null,
    defaultWarehouseCode: null,
    defaultWarehouseName: null,
    defaultCustomerId: null,
    defaultCustomerName: null,
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: null,
  },
  {
    id: "reg-2",
    branchId: "branch-1",
    branchName: "Matriz",
    branchCode: "001",
    emissionPointId: "ep-1",
    establishmentCode: "001",
    emissionPointCode: "001",
    emissionPointName: null,
    code: "CAJA-02",
    name: "Caja Secundaria",
    notes: null,
    isActive: true,
    hasHistory: false,
    accountingAccountId: null,
    defaultWarehouseId: null,
    defaultWarehouseCode: null,
    defaultWarehouseName: null,
    defaultCustomerId: null,
    defaultCustomerName: null,
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: null,
  },
];

function buildSession(overrides: Partial<CashSessionDto> = {}): CashSessionDto {
  return {
    id: "session-1",
    companyId: "company-1",
    branchId: "branch-1",
    userId: "user-1",
    cashRegisterId: "reg-1",
    cashRegisterCodeSnapshot: "CAJA-01",
    cashRegisterNameSnapshot: "Caja Principal",
    emissionPointId: "ep-1",
    emissionPointCodeSnapshot: "001",
    emissionType: "Electronic",
    defaultWarehouseId: null,
    defaultWarehouseName: null,
    defaultCustomerId: null,
    defaultCustomerName: null,
    openedAt: "2026-07-19T10:00:00Z",
    openingAmount: 100,
    status: "Open",
    notes: null,
    closedAt: null,
    closedBy: null,
    closeNotes: null,
    expectedAmount: null,
    countedAmount: null,
    difference: null,
    totalIncome: 0,
    totalExpense: 0,
    currentBalance: 100,
    movements: [],
    closingCounts: [],
    createdAt: "2026-07-19T10:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

/** DTO mínimo de settings.operations — solo `cash.allowManualInOutMovements` importa a Caja;
 * el resto se rellena con valores neutros para satisfacer el tipo del mock. */
function buildOperationalPreferences(
  allowManualInOutMovements = true,
): OperationalPreferencesDto {
  return {
    salesPos: {
      requireOpenCashSession: true,
      allowManualPrice: false,
      allowManualDiscount: true,
      maxDiscountPercent: 0,
      requireCustomerAboveAmount: null,
      allowSellWithoutStock: false,
      askBeforeIssue: false,
      defaultPriceListId: null,
      defaultCustomerId: null,
    },
    cash: {
      requireOpeningAmount: true,
      allowCloseWithDifference: true,
      maxAllowedDifference: 0,
      requireReasonForDifference: true,
      allowManualInOutMovements,
      requireReasonForMovements: true,
    },
    purchases: {
      defaultWarehouseId: null,
      allowConfirmWithoutReceptionXml: true,
      updateCostOnConfirm: true,
      allowManualCostChange: true,
      requireReasonForCostChange: false,
    },
    payables: { allowSupplierPaymentWithoutPayable: false },
    inventory: {
      allowNegativeStock: false,
      stockControlEnabled: true,
      requireReasonForAdjustment: true,
      requireApprovalForLargeAdjustment: false,
      largeAdjustmentThresholdAmount: 0,
    },
    printing: {
      salesReceiptMode: "AskBeforePrint",
      salesReceiptCopies: 1,
      salesReceiptPaperWidth: "80mm",
      salesReceiptIncludeLogo: false,
      salesReceiptIncludeAccessKey: true,
      salesReceiptIncludeCashier: true,
      salesReceiptOpenCashDrawer: false,
    },
    electronicDocuments: {
      autoRetryEnabled: true,
      maxRetryAttempts: 3,
      generateRideOnAuthorization: true,
      emailOnAuthorization: true,
    },
    notifications: {
      sendCopyToCompanyEmail: false,
      defaultLanguage: "es",
    },
  };
}

beforeEach(() => {
  useElectronicInvoicingStatusStore.setState({ status: null, isLoaded: true, isLoading: false });
  localStorage.removeItem(storageKey);
  vi.clearAllMocks();
  useActiveBranchStore.setState({
    branch: { id: "branch-1", name: "Quito Norte", isMainBranch: true },
  });
  useAuthStore.setState({
    user: {
      userId: "user-1",
      fullName: "Ana Perez",
      username: "ana",
      email: "ana@test.com",
      role: "User",
      tenantId: "tenant-1",
    },
    isAuthenticated: true,
    hasHydrated: true,
  });
  vi.mocked(cajaService.getCashRegisters).mockResolvedValue(registers);
  vi.mocked(cajaService.list).mockResolvedValue({
    items: [],
    total: 0,
    page: 1,
    pageSize: 25,
  });
  vi.mocked(cajaService.getMy).mockResolvedValue(null);
  vi.mocked(cajaService.getCollectionSummary).mockResolvedValue({
    invoiceCount: 0,
    totalInvoiced: 0,
    totalCollected: 0,
    totalCredit: 0,
    byPaymentMethod: [],
  });
  vi.mocked(cajaService.getCashMovementReasons).mockResolvedValue([]);
  vi.mocked(message.confirm).mockResolvedValue(true);
  vi.mocked(operationalPreferencesLookupFacade.getPreferences).mockResolvedValue(
    buildOperationalPreferences(true),
  );
  grantPermissions("all");
});

afterEach(() => {
  useElectronicInvoicingStatusStore.getState().clear();
  cleanup();
  localStorage.removeItem(storageKey);
  useAuthStore.setState({
    user: null,
    isAuthenticated: false,
    hasHydrated: false,
    token: null,
    companySessionVersion: 0,
  });
});

describe("useCajaPage closing counts regression R3", () => {
  async function setupCloseReady(currentBalance = 100) {
    const session = buildSession({ currentBalance });
    vi.mocked(cajaService.getMy).mockResolvedValue(session);
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });
    await waitFor(() => expect(result.current.mySession).toEqual(session));
    await act(async () => {
      await result.current.loadDetail(session.id);
    });
    act(() => result.current.startClose());
    return result;
  }

  it("recalcula el total contado al cambiar una denominación por su ruta (como register), sin reemplazar el array", async () => {
    vi.mocked(cajaService.close).mockResolvedValue(
      buildSession({ status: "Closed" }),
    );
    const result = await setupCloseReady(100);

    // Mismo efecto que escribir en el input registrado: RHF muta el array en su lugar y watch()
    // devuelve la misma referencia (R3, 2026-10-28: el total quedaba en $0.00 en Chromium).
    act(() => {
      result.current.closeForm.setValue("closingCounts.3.quantity", 1); // $10
      result.current.closeForm.setValue("closingCounts.4.quantity", 1); // $5
    });

    expect(result.current.countedTotal).toBe(15);

    await act(async () => {
      await result.current.handleClose();
    });
    renderLastConfirmMessage();
    expect(screen.getByText("$15.00")).toBeTruthy();
    expect(screen.getByText("$-85.00")).toBeTruthy();
  });

});
