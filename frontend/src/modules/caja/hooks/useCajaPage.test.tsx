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
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { useAuthStore } from "../../../store/authStore";
import { cajaService } from "../api/cajaService";
import type { CashRegisterDto, CashSessionDto } from "../api/cajaService";
import { useCajaPage } from "./useCajaPage";
import { message } from "../../../lib/messages";

vi.mock("../api/cajaService", () => ({
  cajaService: {
    getCashRegisters: vi.fn(),
    list: vi.fn(),
    getMy: vi.fn(),
    getById: vi.fn(),
    open: vi.fn(),
    close: vi.fn(),
    recordMovement: vi.fn(),
  },
}));

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
  vi.mocked(message.confirm).mockResolvedValue(true);
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

describe("useCajaPage", () => {
  it("carga las cajas disponibles de la sucursal activa al montar", async () => {
    const { result } = renderHook(() => useCajaPage());

    await waitFor(() =>
      expect(result.current.cashRegisters).toEqual(registers),
    );
    expect(cajaService.getCashRegisters).toHaveBeenCalledWith(true);
  });

  it("selecciona automáticamente la primera caja disponible", async () => {
    const { result } = renderHook(() => useCajaPage());

    await waitFor(() =>
      expect(result.current.openForm.getValues("cashRegisterId")).toBe("reg-1"),
    );
  });

  it("muestra la sucursal activa desde el store, no desde un lookup propio", () => {
    const { result } = renderHook(() => useCajaPage());

    expect(result.current.branchName).toBe("Quito Norte");
  });

  it("la apertura envía cashRegisterId y nunca emissionPointId", async () => {
    const session = buildSession();
    vi.mocked(cajaService.open).mockResolvedValue(session);
    const { result } = renderHook(() => useCajaPage());

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
    const { result } = renderHook(() => useCajaPage());

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
    const { result } = renderHook(() => useCajaPage());

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
    const { result } = renderHook(() => useCajaPage());
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

describe("useCajaPage — registrar movimiento: confirmación y feedback (CRITICAL-CONFIRMATIONS-CASH-02)", () => {
  async function setupMovementReady() {
    const session = buildSession();
    vi.mocked(cajaService.getMy).mockResolvedValue(session);
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    const { result } = renderHook(() => useCajaPage());
    await waitFor(() => expect(result.current.mySession).toEqual(session));
    await act(async () => {
      await result.current.loadDetail(session.id);
    });
    act(() => {
      result.current.movementForm.setValue("movementType", "ManualIncome");
      result.current.movementForm.setValue("amount", 40);
      result.current.movementForm.setValue("description", "Cambio de caja chica");
    });
    return result;
  }

  it("pide confirmación con tipo/concepto/monto antes de registrar", async () => {
    vi.mocked(cajaService.recordMovement).mockResolvedValue({
      id: "mv-1",
      movementType: "ManualIncome",
      amount: 40,
      description: "Cambio de caja chica",
      createdAt: "2026-07-19T11:00:00Z",
      createdBy: "user-1",
      referenceType: "Manual",
      referenceId: null,
      referenceNumber: null,
    });
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
    expect(screen.getByText(/\$40\.00/)).toBeTruthy();
  });

  it("usa variant danger para egreso manual", async () => {
    vi.mocked(cajaService.recordMovement).mockResolvedValue({
      id: "mv-2",
      movementType: "ManualExpense",
      amount: 15,
      description: "Compra de insumos",
      createdAt: "2026-07-19T11:00:00Z",
      createdBy: "user-1",
      referenceType: "Manual",
      referenceId: null,
      referenceNumber: null,
    });
    const result = await setupMovementReady();
    act(() => {
      result.current.movementForm.setValue("movementType", "ManualExpense");
      result.current.movementForm.setValue("description", "Compra de insumos");
      result.current.movementForm.setValue("amount", 15);
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
    vi.mocked(cajaService.recordMovement).mockResolvedValue({
      id: "mv-1",
      movementType: "ManualIncome",
      amount: 40,
      description: "Cambio de caja chica",
      createdAt: "2026-07-19T11:00:00Z",
      createdBy: "user-1",
      referenceType: "Manual",
      referenceId: null,
      referenceNumber: null,
    });
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

describe("useCajaPage — cerrar turno: confirmación y feedback (CRITICAL-CONFIRMATIONS-CASH-02)", () => {
  async function setupCloseReady(currentBalance = 100) {
    const session = buildSession({ currentBalance });
    vi.mocked(cajaService.getMy).mockResolvedValue(session);
    vi.mocked(cajaService.getById).mockResolvedValue(session);
    const { result } = renderHook(() => useCajaPage());
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

describe("useCajaPage — no usa diálogos nativos", () => {
  it("no llama a window.confirm, window.prompt ni alert al abrir/registrar/cerrar", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});

    vi.mocked(cajaService.open).mockResolvedValue(buildSession());
    const { result } = renderHook(() => useCajaPage());
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
