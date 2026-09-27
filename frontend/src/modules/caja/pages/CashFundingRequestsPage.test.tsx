// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { CashFundingRequestsPage } from "./CashFundingRequestsPage";
import {
  cashFundingRequestService,
  type CashFundingRequestListItemDto,
} from "../api/cashFundingRequestService";
import { cajaService } from "../api/cajaService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";

vi.mock("../api/cashFundingRequestService", () => ({
  cashFundingRequestService: { list: vi.fn(), listMine: vi.fn() },
}));
vi.mock("../api/cajaService", () => ({ cajaService: { getCashRegisters: vi.fn() } }));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));
vi.mock("../../../lib/messages", () => ({ message: { success: vi.fn(), error: vi.fn() } }));

const ROW: CashFundingRequestListItemDto = {
  id: "3f1c2d4e-0000-4000-9000-000000000001",
  supplierId: "3f1c2d4e-0000-4000-9000-000000000002",
  supplierName: "Distribuidora Andina",
  requestedAtUtc: "2026-09-26T15:00:00Z",
  requestedByUserId: "3f1c2d4e-0000-4000-9000-000000000003",
  requestedByName: "Sergio Compras",
  cashRegisterId: "3f1c2d4e-0000-4000-9000-000000000004",
  cashRegisterName: "Caja Principal",
  cashAmount: 80,
  totalAmount: 200,
  status: "Pending",
  resolvedAtUtc: null,
  resolvedByName: null,
  supplierPaymentId: null,
};

const page = (items: CashFundingRequestListItemDto[]) => ({
  items,
  pageNumber: 1,
  pageSize: 25,
  totalCount: items.length,
});

function grant(...keys: string[]) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (k: string) => keys.includes(k),
    has: (k: string) => keys.includes(k),
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
}

const renderPage = () =>
  render(
    <I18nProvider>
      <MemoryRouter>
        <CashFundingRequestsPage />
      </MemoryRouter>
    </I18nProvider>,
  );

beforeEach(() => {
  vi.mocked(cashFundingRequestService.list).mockResolvedValue(page([ROW]));
  vi.mocked(cashFundingRequestService.listMine).mockResolvedValue(page([ROW]));
  vi.mocked(cajaService.getCashRegisters).mockResolvedValue([
    { id: ROW.cashRegisterId, name: "Caja Principal" } as Awaited<ReturnType<typeof cajaService.getCashRegisters>>[number],
  ]);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("CashFundingRequestsPage", () => {
  it("2. el cajero con view ve Pendientes/Historial y Pendientes filtra por Pending en el servidor", async () => {
    grant("caja.funding-requests.view");
    renderPage();

    expect(await screen.findByRole("tab", { name: "Pendientes" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Historial" })).toBeTruthy();
    expect(screen.queryByRole("tab", { name: "Mis solicitudes" })).toBeNull();
    await waitFor(() =>
      expect(cashFundingRequestService.list).toHaveBeenCalledWith(1, 25, { status: "Pending", cashRegisterId: null }),
    );
    expect(await screen.findByText("Distribuidora Andina")).toBeTruthy();
    expect(screen.getByText("Pendiente")).toBeTruthy();
    for (const header of ["Proveedor", "Caja", "Solicitado por", "Efectivo solicitado", "Pago total", "Fecha", "Estado", "Acción"])
      expect(screen.getAllByText(header).length).toBeGreaterThan(0);
  });

  it("Historial filtra por estado y caja en el servidor (sin filtrar en memoria)", async () => {
    grant("caja.funding-requests.view");
    renderPage();
    fireEvent.click(await screen.findByRole("tab", { name: "Historial" }));
    await waitFor(() =>
      expect(cashFundingRequestService.list).toHaveBeenLastCalledWith(1, 25, { status: null, cashRegisterId: null }),
    );

    fireEvent.change(await screen.findByLabelText("Estado"), { target: { value: "Rejected" } });
    await waitFor(() =>
      expect(cashFundingRequestService.list).toHaveBeenLastCalledWith(1, 25, { status: "Rejected", cashRegisterId: null }),
    );
    fireEvent.change(screen.getByLabelText("Caja"), { target: { value: ROW.cashRegisterId } });
    await waitFor(() =>
      expect(cashFundingRequestService.list).toHaveBeenLastCalledWith(1, 25, {
        status: "Rejected",
        cashRegisterId: ROW.cashRegisterId,
      }),
    );
  });

  it("3–4. el solicitante sin view solo ve Mis solicitudes y nunca pide la bandeja general", async () => {
    grant("supplier-payments.create");
    renderPage();

    expect(await screen.findByRole("tab", { name: "Mis solicitudes" })).toBeTruthy();
    expect(screen.queryByRole("tab", { name: "Pendientes" })).toBeNull();
    expect(screen.queryByRole("tab", { name: "Historial" })).toBeNull();
    await waitFor(() => expect(cashFundingRequestService.listMine).toHaveBeenCalledWith(1, 25, null));
    expect(await screen.findByText("Distribuidora Andina")).toBeTruthy();
    expect(cashFundingRequestService.list).not.toHaveBeenCalled();
    expect(cajaService.getCashRegisters).not.toHaveBeenCalled();
  });

  it("sin ningún permiso no hay bandeja", async () => {
    grant();
    renderPage();
    expect(screen.queryByRole("tab")).toBeNull();
    expect(cashFundingRequestService.list).not.toHaveBeenCalled();
    expect(cashFundingRequestService.listMine).not.toHaveBeenCalled();
  });

  it("16. ningún GUID visible", async () => {
    grant("caja.funding-requests.view", "supplier-payments.create");
    renderPage();
    await screen.findByText("Distribuidora Andina");
    expect(document.body.textContent).not.toMatch(/[0-9a-f]{8}-[0-9a-f]{4}-/i);
  });
});
