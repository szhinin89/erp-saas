// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { cajaService } from "../api/cajaService";
import type { CashRegisterDto, CashSessionDto } from "../api/cajaService";
import { useCajaPage } from "./useCajaPage";

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

beforeEach(() => {
  vi.clearAllMocks();
  useActiveBranchStore.setState({
    branch: { id: "branch-1", name: "Quito Norte", isMainBranch: true },
  });
  vi.mocked(cajaService.getCashRegisters).mockResolvedValue(registers);
  vi.mocked(cajaService.list).mockResolvedValue({
    items: [],
    total: 0,
    page: 1,
    pageSize: 25,
  });
  vi.mocked(cajaService.getMy).mockResolvedValue(null);
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
