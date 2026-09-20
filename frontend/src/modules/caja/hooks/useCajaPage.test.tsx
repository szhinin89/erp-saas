// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  renderHook,
  waitFor,
  act,
  render,
  screen,
  cleanup,
  fireEvent,
} from "@testing-library/react";
import { I18nProvider, useI18n } from "../../../i18n/i18n";
import { dictionaries, storageKey } from "../../../i18n/dictionaries";
import { CajaPage } from "../pages/CajaPage";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { useAuthStore } from "../../../store/authStore";
import { cajaService } from "../api/cajaService";
import type {
  CashRegisterDto,
  CashSessionDto,
  CashSessionCollectionSummaryDto,
  CashMovementReasonDto,
  CashMovementDto,
} from "../api/cajaService";
import { useCajaPage } from "./useCajaPage";
import { message } from "../../../lib/messages";
import { operationalPreferencesService } from "../../configuracion/operaciones/api/operationalPreferencesService";
import type { OperationalPreferencesDto } from "../../configuracion/operaciones/api/operationalPreferencesService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";

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

vi.mock("../../configuracion/operaciones/api/operationalPreferencesService", () => ({
  operationalPreferencesService: {
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
    inventory: {
      allowNegativeStock: false,
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
      salesInvoiceAuthorizedEnabled: true,
      sendCopyToCompanyEmail: false,
      defaultLanguage: "es",
    },
  };
}

/** Reemplaza el array `closingCounts` completo (no un sub-path anidado) para que RHF `watch()`
 * devuelva una referencia nueva y `countedTotal` (useMemo) recalcule — igual que ocurre en la
 * pantalla real, donde cada input está `register()`-ado y sí dispara ese cambio de referencia. */
function setCountedQuantity(
  result: { current: ReturnType<typeof useCajaPage> },
  index: number,
  quantity: number,
) {
  const current = result.current.closeForm.getValues("closingCounts");
  const next = current.map((c, i) => (i === index ? { ...c, quantity } : c));
  result.current.closeForm.setValue("closingCounts", next);
}

beforeEach(() => {
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
  vi.mocked(operationalPreferencesService.getPreferences).mockResolvedValue(
    buildOperationalPreferences(true),
  );
  grantPermissions("all");
});

afterEach(() => {
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

describe("useCajaPage", () => {
  it("carga las cajas disponibles de la sucursal activa al montar", async () => {
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() =>
      expect(result.current.cashRegisters).toEqual(registers),
    );
    expect(cajaService.getCashRegisters).toHaveBeenCalledWith(true);
  });

  it("selecciona automáticamente la primera caja disponible", async () => {
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() =>
      expect(result.current.openForm.getValues("cashRegisterId")).toBe("reg-1"),
    );
  });

  it("muestra la sucursal activa desde el store, no desde un lookup propio", () => {
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    expect(result.current.branchName).toBe("Quito Norte");
  });

  it("la apertura envía cashRegisterId y nunca emissionPointId", async () => {
    const session = buildSession();
    vi.mocked(cajaService.open).mockResolvedValue(session);
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() =>
      expect(result.current.openForm.getValues("cashRegisterId")).toBe("reg-1"),
    );

    act(() => {
      result.current.openForm.setValue("openingAmount", 100);
      result.current.openForm.setValue("notes", "Apertura de prueba");
    });

    await act(async () => {
      await result.current.handleOpen();
    });

    expect(cajaService.open).toHaveBeenCalledTimes(1);
    const payload = vi.mocked(cajaService.open).mock.calls[0][0];
    expect(payload).toEqual({
      cashRegisterId: "reg-1",
      openingAmount: 100,
      notes: "Apertura de prueba",
    });
    expect(payload).not.toHaveProperty("emissionPointId");
    expect(payload).not.toHaveProperty("branchId");
    expect(payload).not.toHaveProperty("companyId");
    expect(payload).not.toHaveProperty("tenantId");
  });

  it("después de abrir muestra la sesión en el detalle con sus datos de caja/punto de emisión", async () => {
    const session = buildSession();
    vi.mocked(cajaService.open).mockResolvedValue(session);
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() =>
      expect(result.current.openForm.getValues("cashRegisterId")).toBe("reg-1"),
    );
    act(() => result.current.openForm.setValue("openingAmount", 100));

    await act(async () => {
      await result.current.handleOpen();
    });

    expect(result.current.tab).toBe("detalle");
    expect(result.current.viewing).toEqual(session);
    expect(result.current.mySession).toEqual(session);
    expect(result.current.viewing?.cashRegisterCodeSnapshot).toBe("CAJA-01");
    expect(result.current.viewing?.emissionPointCodeSnapshot).toBe("001");
  });

  it("propaga el error del backend sin abrir la sesión si la caja no está disponible", async () => {
    vi.mocked(cajaService.open).mockRejectedValue({
      response: { data: { message: { user: "La caja está deshabilitada." } } },
    });
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() =>
      expect(result.current.openForm.getValues("cashRegisterId")).toBe("reg-1"),
    );
    act(() => result.current.openForm.setValue("openingAmount", 100));

    await act(async () => {
      await result.current.handleOpen();
    });

    expect(result.current.tab).not.toBe("detalle");
    expect(result.current.mySession).toBeNull();
  });
});

describe("useCajaPage — abrir turno: confirmación y feedback (CRITICAL-CONFIRMATIONS-CASH-02)", () => {
  async function setupOpenReady() {
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });
    await waitFor(() =>
      expect(result.current.openForm.getValues("cashRegisterId")).toBe("reg-1"),
    );
    act(() => result.current.openForm.setValue("openingAmount", 250));
    return result;
  }

  it("pide confirmación antes de llamar a cajaService.open, con resumen de caja/usuario/monto", async () => {
    vi.mocked(cajaService.open).mockResolvedValue(buildSession());
    const result = await setupOpenReady();

    await act(async () => {
      await result.current.handleOpen();
    });

    expect(message.confirm).toHaveBeenCalledTimes(1);
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(options.title).toMatch(/Abrir turno de caja/i);

    renderLastConfirmMessage();
    expect(screen.getByText(/CAJA-01/)).toBeTruthy();
    expect(screen.getByText(/Ana Perez/)).toBeTruthy();
    expect(screen.getByText(/\$250\.00/)).toBeTruthy();
  });

  it("si se cancela la confirmación, no llama a cajaService.open", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const result = await setupOpenReady();

    await act(async () => {
      await result.current.handleOpen();
    });

    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(cajaService.open).not.toHaveBeenCalled();
    expect(result.current.tab).not.toBe("detalle");
  });

  it("al abrir exitosamente muestra message.success", async () => {
    vi.mocked(cajaService.open).mockResolvedValue(buildSession());
    const result = await setupOpenReady();

    await act(async () => {
      await result.current.handleOpen();
    });

    expect(message.success).toHaveBeenCalledWith("Caja abierta correctamente.");
  });

  it("si el backend falla, expone el mensaje real vía formatApiRequestError", async () => {
    vi.mocked(cajaService.open).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "La caja ya tiene un turno abierto." } },
      },
    });
    const result = await setupOpenReady();

    await act(async () => {
      await result.current.handleOpen();
    });

    expect(result.current.saveError).toBe("La caja ya tiene un turno abierto.");
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no permite doble submit: una segunda llamada mientras saving=true no repite open", async () => {
    let resolveOpen: (session: CashSessionDto) => void = () => {};
    vi.mocked(cajaService.open).mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveOpen = resolve;
        }),
    );
    const result = await setupOpenReady();

    let firstCall: Promise<void>;
    act(() => {
      firstCall = result.current.handleOpen();
    });
    await waitFor(() => expect(result.current.saving).toBe(true));

    await act(async () => {
      await result.current.handleOpen();
    });

    expect(cajaService.open).toHaveBeenCalledTimes(1);

    await act(async () => {
      resolveOpen(buildSession());
      await firstCall;
    });
  });
});

const manualIncomeReasons: CashMovementReasonDto[] = [
  {
    id: "reason-1",
    code: "CAMBIO_CAJA",
    name: "Cambio de caja chica",
    movementType: "ManualIncome",
    isActive: true,
    sortOrder: 1,
  },
];
const manualExpenseReasons: CashMovementReasonDto[] = [
  {
    id: "reason-2",
    code: "COMPRA_INSUMOS",
    name: "Compra de insumos",
    movementType: "ManualExpense",
    isActive: true,
    sortOrder: 1,
  },
];

function mockMovementDto(
  overrides: Partial<CashMovementDto> & {
    id: string;
    movementType: string;
    amount: number;
    description: string;
  },
): CashMovementDto {
  return {
    createdAt: "2026-07-19T11:00:00Z",
    createdBy: "user-1",
    createdByName: "Ana Perez",
    referenceType: "Manual",
    referenceId: null,
    referenceNumber: null,
    reasonId: "reason-1",
    reasonName: "Cambio de caja chica",
    ...overrides,
  };
}

describe("useCajaPage — registrar movimiento: confirmación y feedback (CRITICAL-CONFIRMATIONS-CASH-02)", () => {
  async function setupMovementReady() {
    const session = buildSession();
    vi.mocked(cajaService.getMy).mockResolvedValue(session);
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    vi.mocked(cajaService.getCashMovementReasons).mockImplementation((movementType) =>
      Promise.resolve(movementType === "ManualExpense" ? manualExpenseReasons : manualIncomeReasons),
    );
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });
    await waitFor(() => expect(result.current.mySession).toEqual(session));
    await act(async () => {
      await result.current.loadDetail(session.id);
    });
    act(() => {
      result.current.movementForm.setValue("movementType", "ManualIncome");
      result.current.movementForm.setValue("amount", 40);
      result.current.movementForm.setValue("description", "Ajuste de fondo fijo");
    });
    await waitFor(() => expect(result.current.reasons).toEqual(manualIncomeReasons));
    act(() => {
      result.current.movementForm.setValue("reasonId", "reason-1");
    });
    return result;
  }

  it("pide confirmación con tipo/concepto/monto antes de registrar", async () => {
    vi.mocked(cajaService.recordMovement).mockResolvedValue(
      mockMovementDto({ id: "mv-1", movementType: "ManualIncome", amount: 40, description: "Ajuste de fondo fijo" }),
    );
    const result = await setupMovementReady();

    await act(async () => {
      await result.current.handleRecordMovement();
    });

    expect(message.confirm).toHaveBeenCalledTimes(1);
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(options.variant).toBe("warning");

    renderLastConfirmMessage();
    expect(screen.getByText(/Ingreso manual/)).toBeTruthy();
    expect(screen.getByText(/Cambio de caja chica/)).toBeTruthy();
    expect(screen.getByText(/Ajuste de fondo fijo/)).toBeTruthy();
    expect(screen.getByText(/\$40\.00/)).toBeTruthy();
  });

  it("usa variant danger para egreso manual", async () => {
    vi.mocked(cajaService.recordMovement).mockResolvedValue(
      mockMovementDto({
        id: "mv-2",
        movementType: "ManualExpense",
        amount: 15,
        description: "Compra de insumos",
        reasonId: "reason-2",
        reasonName: "Compra de insumos",
      }),
    );
    const result = await setupMovementReady();
    act(() => {
      result.current.movementForm.setValue("movementType", "ManualExpense");
      result.current.movementForm.setValue("description", "Compra de insumos");
      result.current.movementForm.setValue("amount", 15);
    });
    await waitFor(() => expect(result.current.reasons).toEqual(manualExpenseReasons));
    act(() => {
      result.current.movementForm.setValue("reasonId", "reason-2");
    });

    await act(async () => {
      await result.current.handleRecordMovement();
    });

    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(options.variant).toBe("danger");
  });

  it("si se cancela la confirmación, no llama a recordMovement", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const result = await setupMovementReady();

    await act(async () => {
      await result.current.handleRecordMovement();
    });

    expect(cajaService.recordMovement).not.toHaveBeenCalled();
  });

  it("al registrar exitosamente muestra message.success", async () => {
    vi.mocked(cajaService.recordMovement).mockResolvedValue(
      mockMovementDto({ id: "mv-1", movementType: "ManualIncome", amount: 40, description: "Cambio de caja chica" }),
    );
    const result = await setupMovementReady();

    await act(async () => {
      await result.current.handleRecordMovement();
    });

    expect(message.success).toHaveBeenCalledWith(
      "Movimiento registrado correctamente.",
    );
  });

  it("si falla, llama a message.error con el mensaje real del backend", async () => {
    vi.mocked(cajaService.recordMovement).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 422,
        data: { data: { errors: ["El monto excede el límite permitido."] } },
      },
    });
    const result = await setupMovementReady();

    await act(async () => {
      await result.current.handleRecordMovement();
    });

    expect(message.error).toHaveBeenCalledWith(
      "El monto excede el límite permitido.",
    );
    expect(message.success).not.toHaveBeenCalled();
  });
});

describe("useCajaPage — modal de registrar movimiento (TREASURY-CASH-MANUAL-MOVEMENT-MODAL-02)", () => {
  async function setupMovementReady() {
    const session = buildSession();
    vi.mocked(cajaService.getMy).mockResolvedValue(session);
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    vi.mocked(cajaService.getCashMovementReasons).mockImplementation((movementType) =>
      Promise.resolve(movementType === "ManualExpense" ? manualExpenseReasons : manualIncomeReasons),
    );
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });
    await waitFor(() => expect(result.current.mySession).toEqual(session));
    await act(async () => {
      await result.current.loadDetail(session.id);
    });
    return result;
  }

  it("el modal inicia cerrado", async () => {
    const result = await setupMovementReady();
    expect(result.current.movementModalOpen).toBe(false);
  });

  it("openMovementModal abre el modal con el formulario limpio", async () => {
    const result = await setupMovementReady();

    act(() => {
      result.current.movementForm.setValue("movementType", "ManualIncome");
      result.current.movementForm.setValue("amount", 40);
    });
    act(() => {
      result.current.openMovementModal();
    });

    expect(result.current.movementModalOpen).toBe(true);
    expect(result.current.movementForm.getValues("movementType")).toBe("");
    expect(result.current.movementForm.getValues("amount")).toBe(0);
  });

  it("closeMovementModal cierra el modal y limpia el formulario (cancelar)", async () => {
    const result = await setupMovementReady();

    act(() => {
      result.current.openMovementModal();
      result.current.movementForm.setValue("movementType", "Withdrawal");
      result.current.movementForm.setValue("description", "Depósito banco");
    });
    act(() => {
      result.current.closeMovementModal();
    });

    expect(result.current.movementModalOpen).toBe(false);
    expect(result.current.movementForm.getValues("movementType")).toBe("");
    expect(result.current.movementForm.getValues("description")).toBe("");
  });

  it("al registrar correctamente, cierra el modal y refresca sesión/movimientos/resumen", async () => {
    vi.mocked(message.confirm).mockResolvedValue(true);
    vi.mocked(cajaService.recordMovement).mockResolvedValue(
      mockMovementDto({ id: "mv-1", movementType: "ManualIncome", amount: 40, description: "Ajuste de fondo fijo" }),
    );
    const result = await setupMovementReady();

    act(() => {
      result.current.openMovementModal();
      result.current.movementForm.setValue("movementType", "ManualIncome");
      result.current.movementForm.setValue("amount", 40);
      result.current.movementForm.setValue("description", "Ajuste de fondo fijo");
    });
    await waitFor(() => expect(result.current.reasons).toEqual(manualIncomeReasons));
    act(() => {
      result.current.movementForm.setValue("reasonId", "reason-1");
    });

    vi.mocked(cajaService.getCollectionSummary).mockClear();
    vi.mocked(cajaService.getById).mockClear();
    vi.mocked(cajaService.getMy).mockClear();

    await act(async () => {
      await result.current.handleRecordMovement();
    });

    expect(result.current.movementModalOpen).toBe(false);
    expect(cajaService.getById).toHaveBeenCalledWith(result.current.viewing!.id);
    expect(cajaService.getCollectionSummary).toHaveBeenCalledWith(result.current.viewing!.id);
    expect(cajaService.getMy).toHaveBeenCalled();
  });

  it("si el registro falla, el modal permanece abierto para mostrar el error", async () => {
    vi.mocked(message.confirm).mockResolvedValue(true);
    vi.mocked(cajaService.recordMovement).mockRejectedValue({
      isAxiosError: true,
      response: { status: 422, data: { data: { errors: ["El motivo seleccionado está inactivo."] } } },
    });
    const result = await setupMovementReady();

    act(() => {
      result.current.openMovementModal();
      result.current.movementForm.setValue("movementType", "ManualIncome");
      result.current.movementForm.setValue("amount", 40);
      result.current.movementForm.setValue("description", "Cambio de caja chica");
    });
    await waitFor(() => expect(result.current.reasons).toEqual(manualIncomeReasons));
    act(() => {
      result.current.movementForm.setValue("reasonId", "reason-1");
    });

    await act(async () => {
      await result.current.handleRecordMovement();
    });

    expect(result.current.movementModalOpen).toBe(true);
    // SALES-MANUAL-CASH-MOVEMENT-INTEGRATION-08 — el error del flujo de movimiento ahora vive en
    // su propio estado (movementSaveError), separado del saveError de abrir/cerrar turno.
    expect(result.current.movementSaveError).toBe("El motivo seleccionado está inactivo.");
  });
});

describe("useCajaPage — AllowManualInOutMovements por empresa (TREASURY-CASH-COMPANY-SETTING-05A: fail-closed en frontend)", () => {
  it("true (ya resuelto) + turno abierto + permiso: expone allowManualMovements=true y permite abrir el modal", async () => {
    vi.mocked(operationalPreferencesService.getPreferences).mockResolvedValue(
      buildOperationalPreferences(true),
    );
    const session = buildSession();
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() => expect(result.current.allowManualMovements).toBe(true));
    await act(async () => {
      await result.current.loadDetail(session.id);
    });
    act(() => result.current.openMovementModal());
    expect(result.current.movementModalOpen).toBe(true);
  });

  it("false (ya resuelto): expone allowManualMovements=false y openMovementModal no abre el modal", async () => {
    vi.mocked(operationalPreferencesService.getPreferences).mockResolvedValue(
      buildOperationalPreferences(false),
    );
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() => expect(result.current.allowManualMovements).toBe(false));
    act(() => result.current.openMovementModal());
    expect(result.current.movementModalOpen).toBe(false);
  });

  it("mientras carga: allowManualMovements empieza en false (oculto), nunca en true", async () => {
    let resolveFn: (dto: ReturnType<typeof buildOperationalPreferences>) => void;
    vi.mocked(operationalPreferencesService.getPreferences).mockReturnValue(
      new Promise((resolve) => {
        resolveFn = resolve;
      }),
    );
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    // Aún no se resolvió la promesa: debe seguir oculto, nunca asumir true mientras carga.
    expect(result.current.allowManualMovements).toBe(false);

    await act(async () => {
      resolveFn(buildOperationalPreferences(true));
      await Promise.resolve();
    });
    await waitFor(() => expect(result.current.allowManualMovements).toBe(true));
  });

  it("si falla la carga de preferencias, queda oculto (false) — nunca se asume true", async () => {
    vi.mocked(operationalPreferencesService.getPreferences).mockRejectedValue(
      new Error("network error"),
    );
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() => expect(operationalPreferencesService.getPreferences).toHaveBeenCalled());
    expect(result.current.allowManualMovements).toBe(false);
    act(() => result.current.openMovementModal());
    expect(result.current.movementModalOpen).toBe(false);
  });

  async function renderDetailView() {
    const session = buildSession();
    const listItem = {
      id: session.id,
      userId: session.userId,
      userName: "Ana Perez",
      cashRegisterId: session.cashRegisterId,
      cashRegisterCodeSnapshot: session.cashRegisterCodeSnapshot,
      cashRegisterNameSnapshot: session.cashRegisterNameSnapshot,
      emissionPointId: session.emissionPointId,
      emissionPointCodeSnapshot: session.emissionPointCodeSnapshot,
      openedAt: session.openedAt,
      openingAmount: session.openingAmount,
      status: session.status,
      currentBalance: session.currentBalance,
      expectedCash: session.currentBalance,
      countedAmount: null,
      movementCount: 0,
      closedAt: null,
      closedBy: null,
      closedByName: null,
      difference: null,
      invoiceCount: 0,
      totalInvoiced: 0,
      saleIncomeCash: 0,
      manualIncomeCash: 0,
      manualExpenseCash: 0,
      byPaymentMethod: [],
      createdAt: session.createdAt,
    };
    vi.mocked(cajaService.getMy).mockResolvedValue(session);
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    vi.mocked(cajaService.list).mockResolvedValue({
      items: [listItem],
      total: 1,
      page: 1,
      pageSize: 25,
    });

    render(
      <I18nProvider>
        <CajaPage />
      </I18nProvider>,
    );

    const viewButton = await screen.findByRole("button", {
      name: /Ver detalle de sesión/,
    });
    fireEvent.click(viewButton);
    await screen.findByText("Sesión de Caja");
  }

  it("botón + Registrar movimiento visible en el detalle del turno cuando la empresa lo permite", async () => {
    vi.mocked(operationalPreferencesService.getPreferences).mockResolvedValue(
      buildOperationalPreferences(true),
    );

    await renderDetailView();

    expect(await screen.findByText("Registrar movimiento")).toBeTruthy();
  });

  it("botón + Registrar movimiento oculto cuando la empresa lo tiene deshabilitado", async () => {
    vi.mocked(operationalPreferencesService.getPreferences).mockResolvedValue(
      buildOperationalPreferences(false),
    );

    await renderDetailView();

    await waitFor(() =>
      expect(operationalPreferencesService.getPreferences).toHaveBeenCalled(),
    );
    await waitFor(() => expect(screen.queryByText("Registrar movimiento")).toBeNull());
    // "Cerrar Caja" (otra acción del turno abierto) sigue disponible — solo se oculta el botón
    // de movimientos manuales.
    expect(screen.getByText("Cerrar Caja")).toBeTruthy();
  });

  it("cambiar de empresa (companySessionVersion) vuelve a pedir la preferencia", async () => {
    vi.mocked(operationalPreferencesService.getPreferences)
      .mockResolvedValueOnce(buildOperationalPreferences(true))
      .mockResolvedValueOnce(buildOperationalPreferences(false));
    const { result, rerender } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() => expect(result.current.allowManualMovements).toBe(true));
    expect(operationalPreferencesService.getPreferences).toHaveBeenCalledTimes(1);

    act(() => {
      useAuthStore.setState((s) => ({ companySessionVersion: s.companySessionVersion + 1 }));
    });
    rerender();

    await waitFor(() => expect(result.current.allowManualMovements).toBe(false));
    expect(operationalPreferencesService.getPreferences).toHaveBeenCalledTimes(2);
  });

  it("botón oculto si el turno está cerrado, aunque la empresa lo permita", async () => {
    vi.mocked(operationalPreferencesService.getPreferences).mockResolvedValue(
      buildOperationalPreferences(true),
    );
    const closedSession = buildSession({
      status: "Closed",
      closedAt: "2026-07-19T18:00:00Z",
      closedBy: "user-1",
    });
    vi.mocked(cajaService.getMy).mockResolvedValue(null);
    vi.mocked(cajaService.getById).mockResolvedValue(closedSession);
    vi.mocked(cajaService.list).mockResolvedValue({
      items: [
        {
          id: closedSession.id,
          userId: closedSession.userId,
          userName: "Ana Perez",
          cashRegisterId: closedSession.cashRegisterId,
          cashRegisterCodeSnapshot: closedSession.cashRegisterCodeSnapshot,
          cashRegisterNameSnapshot: closedSession.cashRegisterNameSnapshot,
          emissionPointId: closedSession.emissionPointId,
          emissionPointCodeSnapshot: closedSession.emissionPointCodeSnapshot,
          openedAt: closedSession.openedAt,
          openingAmount: closedSession.openingAmount,
          status: closedSession.status,
          currentBalance: closedSession.currentBalance,
          expectedCash: closedSession.currentBalance,
          countedAmount: closedSession.currentBalance,
          movementCount: 0,
          closedAt: closedSession.closedAt,
          closedBy: closedSession.closedBy,
          closedByName: "Ana Perez",
          difference: 0,
          invoiceCount: 0,
          totalInvoiced: 0,
          saleIncomeCash: 0,
          manualIncomeCash: 0,
          manualExpenseCash: 0,
          byPaymentMethod: [],
          createdAt: closedSession.createdAt,
        },
      ],
      total: 1,
      page: 1,
      pageSize: 25,
    });

    render(
      <I18nProvider>
        <CajaPage />
      </I18nProvider>,
    );
    const viewButton = await screen.findByRole("button", { name: /Ver detalle de sesión/ });
    fireEvent.click(viewButton);
    await screen.findByText("Sesión de Caja");

    expect(screen.queryByText("Registrar movimiento")).toBeNull();
    // Tampoco "Cerrar Caja" — el turno ya está cerrado, no hay acciones de turno abierto.
    expect(screen.queryByText("Cerrar Caja")).toBeNull();
  });
});

describe("useCajaPage — permiso caja.record (TREASURY-CASH-MANUAL-MOVEMENTS-PERMISSION-06)", () => {
  it("empresa permite + usuario CON caja.record: expone canRecordManualMovements=true y permite abrir el modal", async () => {
    grantPermissions(["caja.record"]);
    const session = buildSession();
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() => expect(result.current.allowManualMovements).toBe(true));
    expect(result.current.canRecordManualMovements).toBe(true);
    await act(async () => {
      await result.current.loadDetail(session.id);
    });
    act(() => result.current.openMovementModal());
    expect(result.current.movementModalOpen).toBe(true);
  });

  it("empresa permite + usuario SIN caja.record: expone canRecordManualMovements=false y openMovementModal no abre el modal", async () => {
    grantPermissions([]); // ningún permiso, ni siquiera caja.view
    const session = buildSession();
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() => expect(result.current.allowManualMovements).toBe(true));
    expect(result.current.canRecordManualMovements).toBe(false);
    await act(async () => {
      await result.current.loadDetail(session.id);
    });
    act(() => result.current.openMovementModal());
    expect(result.current.movementModalOpen).toBe(false);
  });

  it("empresa NO permite + usuario CON caja.record: sigue oculto — ambas condiciones deben cumplirse", async () => {
    grantPermissions(["caja.record"]);
    vi.mocked(operationalPreferencesService.getPreferences).mockResolvedValue(
      buildOperationalPreferences(false),
    );
    const session = buildSession();
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() => expect(result.current.allowManualMovements).toBe(false));
    expect(result.current.canRecordManualMovements).toBe(true);
    await act(async () => {
      await result.current.loadDetail(session.id);
    });
    act(() => result.current.openMovementModal());
    expect(result.current.movementModalOpen).toBe(false);
  });

  it("botón + Registrar movimiento oculto en el detalle si el usuario no tiene caja.record, aunque la empresa lo permita", async () => {
    grantPermissions([]);
    vi.mocked(operationalPreferencesService.getPreferences).mockResolvedValue(
      buildOperationalPreferences(true),
    );
    const session = buildSession();
    const listItem = {
      id: session.id,
      userId: session.userId,
      userName: "Ana Perez",
      cashRegisterId: session.cashRegisterId,
      cashRegisterCodeSnapshot: session.cashRegisterCodeSnapshot,
      cashRegisterNameSnapshot: session.cashRegisterNameSnapshot,
      emissionPointId: session.emissionPointId,
      emissionPointCodeSnapshot: session.emissionPointCodeSnapshot,
      openedAt: session.openedAt,
      openingAmount: session.openingAmount,
      status: session.status,
      currentBalance: session.currentBalance,
      expectedCash: session.currentBalance,
      countedAmount: null,
      movementCount: 0,
      closedAt: null,
      closedBy: null,
      closedByName: null,
      difference: null,
      invoiceCount: 0,
      totalInvoiced: 0,
      saleIncomeCash: 0,
      manualIncomeCash: 0,
      manualExpenseCash: 0,
      byPaymentMethod: [],
      createdAt: session.createdAt,
    };
    vi.mocked(cajaService.getMy).mockResolvedValue(session);
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    vi.mocked(cajaService.list).mockResolvedValue({
      items: [listItem],
      total: 1,
      page: 1,
      pageSize: 25,
    });

    render(
      <I18nProvider>
        <CajaPage />
      </I18nProvider>,
    );
    const viewButton = await screen.findByRole("button", { name: /Ver detalle de sesión/ });
    fireEvent.click(viewButton);
    await screen.findByText("Sesión de Caja");

    await waitFor(() => expect(screen.queryByText("Registrar movimiento")).toBeNull());
    // "Cerrar Caja" no depende de caja.record — sigue visible.
    expect(screen.getByText("Cerrar Caja")).toBeTruthy();
  });

  it("cambiar de usuario/contexto (permisos) actualiza la disponibilidad sin recargar la página", async () => {
    grantPermissions(["caja.record"]);
    const session = buildSession();
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    const { result, rerender } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await waitFor(() => expect(result.current.canRecordManualMovements).toBe(true));

    // Simula un cambio de usuario/rol activo (ej. impersonar, cambiar de perfil) que cambia el
    // conjunto de permisos que devuelve usePermissionsUi — sin recargar el hook desde cero.
    grantPermissions([]);
    rerender();

    expect(result.current.canRecordManualMovements).toBe(false);
  });
});

describe("useCajaPage — cerrar turno: confirmación y feedback (CRITICAL-CONFIRMATIONS-CASH-02)", () => {
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

  it("pide confirmación/modal antes de cerrar y muestra esperado/contado/diferencia", async () => {
    vi.mocked(cajaService.close).mockResolvedValue(
      buildSession({ status: "Closed" }),
    );
    const result = await setupCloseReady(100);

    act(() => {
      setCountedQuantity(result, 0, 1); // $100
    });

    await act(async () => {
      await result.current.handleClose();
    });

    expect(message.confirm).toHaveBeenCalledTimes(1);
    renderLastConfirmMessage();
    expect(screen.getByText(/Esperado:/)).toBeTruthy();
    expect(screen.getByText(/Contado:/)).toBeTruthy();
    expect(screen.getByText(/Diferencia:/)).toBeTruthy();
  });

  it("con descuadre (diferencia != 0) usa variant danger y muestra advertencia", async () => {
    vi.mocked(cajaService.close).mockResolvedValue(
      buildSession({ status: "Closed" }),
    );
    const result = await setupCloseReady(100);

    act(() => {
      setCountedQuantity(result, 4, 1); // $5 contado vs $100 esperado
    });

    await act(async () => {
      await result.current.handleClose();
    });

    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(options.variant).toBe("danger");

    renderLastConfirmMessage();
    expect(
      screen.getByText(/diferencia entre el saldo esperado y lo contado/i),
    ).toBeTruthy();
  });

  it("sin descuadre usa variant warning y no muestra la advertencia de diferencia", async () => {
    vi.mocked(cajaService.close).mockResolvedValue(
      buildSession({ status: "Closed" }),
    );
    const result = await setupCloseReady(100);

    act(() => {
      setCountedQuantity(result, 0, 1); // exacto
    });

    await act(async () => {
      await result.current.handleClose();
    });

    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(options.variant).toBe("warning");

    renderLastConfirmMessage();
    expect(
      screen.queryByText(/diferencia entre el saldo esperado y lo contado/i),
    ).toBeNull();
  });

  it("si se cancela, no llama a cajaService.close", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const result = await setupCloseReady(100);

    await act(async () => {
      await result.current.handleClose();
    });

    expect(cajaService.close).not.toHaveBeenCalled();
  });

  it("al cerrar exitosamente muestra message.success", async () => {
    vi.mocked(cajaService.close).mockResolvedValue(
      buildSession({ status: "Closed" }),
    );
    const result = await setupCloseReady(100);

    act(() => setCountedQuantity(result, 0, 1));

    await act(async () => {
      await result.current.handleClose();
    });

    expect(message.success).toHaveBeenCalledWith("Caja cerrada correctamente.");
  });

  it("si falla, expone el error real del backend sin cerrar la sesión localmente", async () => {
    vi.mocked(cajaService.close).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 500,
        data: { message: { user: "No se pudo procesar el cierre." } },
      },
    });
    const result = await setupCloseReady(100);
    act(() => setCountedQuantity(result, 0, 1));

    await act(async () => {
      await result.current.handleClose();
    });

    expect(result.current.saveError).toBe("No se pudo procesar el cierre.");
    expect(result.current.viewing?.status).toBe("Open");
    expect(message.success).not.toHaveBeenCalled();
  });
});

describe("useCajaPage — resumen de cobros del turno (CASH-SESSION-COLLECTION-SUMMARY-01)", () => {
  it("al cargar el detalle, pide el resumen de cobros de esa sesión y lo expone separado del efectivo físico", async () => {
    const session = buildSession({ totalIncome: 0, currentBalance: 100 });
    const summary: CashSessionCollectionSummaryDto = {
      invoiceCount: 2,
      totalInvoiced: 22.59,
      totalCollected: 2.59,
      totalCredit: 20,
      byPaymentMethod: [
        {
          paymentMethodId: "pm-efectivo",
          paymentMethodCode: "EFECTIVO",
          paymentMethodName: "Efectivo",
          isCreditAllowed: false,
          invoiceCount: 1,
          operationCount: 1,
          amount: 1.59,
          destination: "Caja física",
          details: [
            {
              invoiceId: "inv-1",
              invoiceNumber: "001-001-000000005",
              authorizedAt: "2026-09-18T10:00:00Z",
              customerName: "Cliente Test",
              invoiceTotal: 2.59,
              amount: 1.59,
              isMixedPayment: true,
              reference: null,
              destinationBankName: null,
              destinationAccountMasked: null,
              transferReceiptNumber: null,
              transferDate: null,
            },
          ],
        },
        {
          paymentMethodId: "pm-credito",
          paymentMethodCode: "CREDITO",
          paymentMethodName: "Crédito",
          isCreditAllowed: true,
          invoiceCount: 1,
          operationCount: 1,
          amount: 20,
          destination: "Cuentas por Cobrar",
          details: [],
        },
      ],
    };
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    vi.mocked(cajaService.getCollectionSummary).mockResolvedValue(summary);
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await act(async () => {
      await result.current.loadDetail(session.id);
    });

    expect(cajaService.getCollectionSummary).toHaveBeenCalledWith(session.id);
    expect(result.current.collectionSummary).toEqual(summary);
    // El resumen de cobros nunca debe alterar el efectivo físico de `viewing`.
    expect(result.current.viewing?.totalIncome).toBe(0);
    expect(result.current.viewing?.currentBalance).toBe(100);
  });

  it("si falla la consulta del resumen, no rompe la carga del detalle de la sesión", async () => {
    const session = buildSession();
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    vi.mocked(cajaService.getCollectionSummary).mockRejectedValue(new Error("network error"));
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });

    await act(async () => {
      await result.current.loadDetail(session.id);
    });

    expect(result.current.viewing).toEqual(session);
    expect(result.current.collectionSummary).toBeNull();
  });

  it("al abrir una caja nueva, limpia el resumen de cobros de una sesión anterior", async () => {
    vi.mocked(cajaService.open).mockResolvedValue(buildSession());
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });
    await waitFor(() =>
      expect(result.current.openForm.getValues("cashRegisterId")).toBe("reg-1"),
    );
    act(() => result.current.openForm.setValue("openingAmount", 100));

    await act(async () => {
      await result.current.handleOpen();
    });

    expect(result.current.collectionSummary).toBeNull();
  });
});

describe("useCajaPage — no usa diálogos nativos", () => {
  it("no llama a window.confirm, window.prompt ni alert al abrir/registrar/cerrar", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});

    vi.mocked(cajaService.open).mockResolvedValue(buildSession());
    const { result } = renderHook(() => useCajaPage(), { wrapper: I18nProvider });
    await waitFor(() =>
      expect(result.current.openForm.getValues("cashRegisterId")).toBe("reg-1"),
    );
    act(() => result.current.openForm.setValue("openingAmount", 100));

    await act(async () => {
      await result.current.handleOpen();
    });

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});

describe.each(["en", "qu"] as const)("cash i18n %s", (locale) => {
  it("renders opening, detail, manual movement dialog and closing with localized labels", async () => {
    localStorage.setItem(storageKey, locale);
    const dict = dictionaries[locale];
    vi.mocked(cajaService.open).mockResolvedValue(buildSession());
    render(<I18nProvider><CajaPage /></I18nProvider>);
    fireEvent.click(await screen.findByRole("button", { name: new RegExp(dict["caja.session.open"]) }));
    await screen.findByDisplayValue("Quito Norte");
    expect(screen.getByText(dict["caja.session.activeBranch"])).toBeTruthy();
    await waitFor(() => expect(screen.getByRole("combobox").getAttribute("disabled")).toBeNull());
    fireEvent.click(screen.getByRole("button", { name: dict["caja.session.open"] }));
    await screen.findByRole("heading", { name: dict["caja.session.title"] });
    expect(message.confirm).toHaveBeenCalledWith(expect.objectContaining({
      title: dict["caja.session.openTitle"], cancelLabel: dict["common.cancel"],
    }));
    expect(message.success).toHaveBeenCalledWith(dict["caja.session.openSuccess"]);
    expect(screen.getByText(dict["caja.session.separationNotice"])).toBeTruthy();
    expect(screen.getByRole("heading", { name: dict["caja.session.physicalCash"] })).toBeTruthy();
    expect(screen.getByRole("heading", { name: dict["caja.session.salesCollections"] })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: new RegExp(dict["caja.movements.recordButton"]) }));
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect(screen.getByRole("heading", { name: dict["caja.movements.modal.title"] })).toBeTruthy();
    expect(screen.getByRole("option", { name: dict["caja.movementType.manualIncome"] }).getAttribute("value")).toBe("ManualIncome");
    fireEvent.click(screen.getByRole("button", { name: dict["common.close"] }));
    fireEvent.click(screen.getByRole("button", { name: dict["caja.session.close"] }));
    expect(screen.getByRole("heading", { name: dict["caja.session.closeCount"] })).toBeTruthy();
    expect(screen.getByRole("columnheader", { name: dict["caja.session.denomination"] })).toBeTruthy();
    vi.mocked(message.confirm).mockResolvedValue(false);
    fireEvent.click(screen.getByRole("button", { name: dict["caja.session.confirmClose"] }));
    await waitFor(() => expect(message.confirm).toHaveBeenLastCalledWith(expect.objectContaining({
      title: dict["caja.session.closeTitle"], variant: "danger",
    })));
    expect(cajaService.close).not.toHaveBeenCalled();
  });

  it("updates validation and error messages after a locale switch without translating identifiers", async () => {
    const { result } = renderHook(() => ({ page: useCajaPage(), i18n: useI18n() }), { wrapper: I18nProvider });
    await waitFor(() => expect(result.current.page.cashRegisters).toHaveLength(2));
    act(() => result.current.i18n.setLocale(locale));
    act(() => result.current.page.openForm.setValue("cashRegisterId", ""));
    await act(async () => { await result.current.page.handleOpen(); });
    expect(result.current.page.openForm.getFieldState("cashRegisterId").error?.message)
      .toBe(dictionaries[locale]["caja.validation.register"]);
    vi.mocked(cajaService.getById).mockRejectedValue(new Error("unavailable"));
    await act(async () => { await result.current.page.loadDetail("session-1"); });
    expect(result.current.page.saveError).toBe(dictionaries[locale]["caja.session.loadError"]);
    expect(result.current.page.movementTypes.map(item => item.value)).toEqual(["ManualIncome", "ManualExpense", "Withdrawal"]);
  });
});
