// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../../i18n/i18n";
import { StockAdjustmentsPage } from "./StockAdjustmentsPage";
import { stockAdjustmentsService } from "../api/stockAdjustmentsService";
import { inventoryAdjustmentReasonsService } from "../../adjustmentReasons/api/inventoryAdjustmentReasonsService";
import { warehouseService } from "../../warehouses/api/warehouseService";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import type { StockAdjustmentDto } from "../types";

vi.mock("react-router-dom", async () => {
  const actual =
    await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return { ...actual, useNavigate: () => vi.fn() };
});

vi.mock("../api/stockAdjustmentsService", () => ({
  stockAdjustmentsService: {
    list: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    execute: vi.fn(),
    cancel: vi.fn(),
  },
}));

vi.mock("../../adjustmentReasons/api/inventoryAdjustmentReasonsService", () => ({
  inventoryAdjustmentReasonsService: {
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    toggle: vi.fn(),
  },
}));

vi.mock("../../warehouses/api/warehouseService", () => ({
  warehouseService: { list: vi.fn() },
}));

vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}));

function adjustment(over: Partial<StockAdjustmentDto>): StockAdjustmentDto {
  return {
    id: "adj-1",
    adjustmentNumber: "AJU-000001",
    warehouseId: "wh-1",
    warehouseName: "Bodega Central",
    movementType: "Ingreso",
    reasonId: "rsn-1",
    reasonName: "Merma",
    notes: null,
    adjustmentDate: "2026-08-01T00:00:00Z",
    status: "Draft",
    executedAt: null,
    executedBy: null,
    cancelledAt: null,
    cancelledBy: null,
    cancelledReason: null,
    lines: [],
    ...over,
  };
}

function grantAll(granted: string[] | "all" = "all") {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (key: string) => granted === "all" || granted.includes(key),
    has: () => true,
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
}

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <StockAdjustmentsPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  grantAll("all");
  vi.mocked(warehouseService.list).mockResolvedValue([]);
  vi.mocked(inventoryAdjustmentReasonsService.list).mockResolvedValue([]);
  vi.mocked(stockAdjustmentsService.list).mockResolvedValue({
    items: [],
    pageNumber: 1,
    pageSize: 20,
    totalCount: 0,
  });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("StockAdjustmentsPage", () => {
  it("renderiza los ajustes devueltos por el servicio", async () => {
    vi.mocked(stockAdjustmentsService.list).mockResolvedValue({
      items: [
        adjustment({
          status: "Executed",
          lines: [
            {
              id: "l1",
              itemId: "i1",
              itemName: "Arroz",
              packagingLevelId: null,
              uomCode: "UN",
              baseUomCode: "UN",
              conversionFactor: 1,
              quantity: 2,
              quantityInBaseUom: 2,
              unitCostBase: 5,
              totalCost: 10,
              currentStockBefore: 100,
              currentStockAfter: 102,
              lineNotes: null,
            },
          ],
        }),
      ],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
    });

    renderPage();

    expect(await screen.findByText("AJU-000001")).toBeTruthy();
    expect(screen.getByText("Bodega Central")).toBeTruthy();
    expect(screen.getByText("Merma")).toBeTruthy();
    // "Ejecutado" aparece también como opción del filtro de estado — basta con que exista
    // el badge de la fila además de esa opción.
    expect(screen.getAllByText("Ejecutado").length).toBeGreaterThan(1);
    // Costo total = suma de TotalCost, visible solo en un ajuste ya ejecutado.
    expect(screen.getByText("10.00")).toBeTruthy();
  });

  it("muestra Editar/Ejecutar solo en Borrador y Anular solo en Ejecutado", async () => {
    vi.mocked(stockAdjustmentsService.list).mockResolvedValue({
      items: [
        adjustment({ id: "adj-1", adjustmentNumber: "AJU-1", status: "Draft" }),
        adjustment({
          id: "adj-2",
          adjustmentNumber: "AJU-2",
          status: "Executed",
        }),
        adjustment({
          id: "adj-3",
          adjustmentNumber: "AJU-3",
          status: "Cancelled",
        }),
      ],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 3,
    });

    renderPage();

    await screen.findByText("AJU-1");
    // Un solo Borrador → una sola acción Editar y una sola Ejecutar.
    expect(screen.getAllByText("Editar")).toHaveLength(1);
    expect(screen.getAllByText("Ejecutar")).toHaveLength(1);
    // Un solo Ejecutado → una sola acción Anular. El Anulado no ofrece ninguna.
    expect(screen.getAllByText("Anular")).toHaveLength(1);
    expect(screen.getAllByText("Ver")).toHaveLength(3);
  });

  it("oculta las acciones cuando faltan los permisos, aunque el estado las permita", async () => {
    grantAll(["inventory.adjustments.view"]);
    vi.mocked(stockAdjustmentsService.list).mockResolvedValue({
      items: [
        adjustment({ id: "adj-1", status: "Draft" }),
        adjustment({ id: "adj-2", status: "Executed" }),
      ],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 2,
    });

    renderPage();

    await screen.findAllByText("Ver");
    expect(screen.queryByText("Editar")).toBeNull();
    expect(screen.queryByText("Ejecutar")).toBeNull();
    expect(screen.queryByText("Anular")).toBeNull();
    expect(screen.queryByText("Nuevo ajuste")).toBeNull();
  });

  it("Ejecutar abre la confirmación y luego llama a execute(id)", async () => {
    vi.mocked(stockAdjustmentsService.list).mockResolvedValue({
      items: [adjustment({ id: "adj-9", status: "Draft" })],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
    });
    vi.mocked(stockAdjustmentsService.execute).mockResolvedValue(
      adjustment({ id: "adj-9", status: "Executed" }),
    );

    renderPage();

    fireEvent.click(await screen.findByText("Ejecutar"));
    // El servicio no se llama hasta confirmar en el modal.
    expect(stockAdjustmentsService.execute).not.toHaveBeenCalled();

    fireEvent.click(await screen.findByText("Sí, ejecutar"));

    await waitFor(() =>
      expect(stockAdjustmentsService.execute).toHaveBeenCalledWith("adj-9"),
    );
  });

  it("Anular exige el motivo en el modal y luego llama a cancel(id, reason)", async () => {
    vi.mocked(stockAdjustmentsService.list).mockResolvedValue({
      items: [adjustment({ id: "adj-7", status: "Executed" })],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
    });
    vi.mocked(stockAdjustmentsService.cancel).mockResolvedValue(
      adjustment({ id: "adj-7", status: "Cancelled" }),
    );

    renderPage();

    fireEvent.click(await screen.findByText("Anular"));
    // Confirmar sin motivo no llama al backend: el motivo es obligatorio en el contrato.
    fireEvent.click(await screen.findByText("Sí, anular"));
    await screen.findByText("Indique el motivo de la anulación.");
    expect(stockAdjustmentsService.cancel).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText("Motivo de anulación"), {
      target: { value: "Error de digitación" },
    });
    fireEvent.click(screen.getByText("Sí, anular"));

    await waitFor(() =>
      expect(stockAdjustmentsService.cancel).toHaveBeenCalledWith(
        "adj-7",
        "Error de digitación",
      ),
    );
  });

  it("muestra el mensaje específico del backend cuando falla Ejecutar", async () => {
    vi.mocked(stockAdjustmentsService.list).mockResolvedValue({
      items: [adjustment({ id: "adj-3", status: "Draft" })],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
    });
    // Forma real de un error 4xx de la API (ver apiError.ts: data.errors tiene prioridad).
    vi.mocked(stockAdjustmentsService.execute).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 400,
        data: {
          data: { errors: ["Stock insuficiente en la bodega seleccionada."] },
          message: { user: "Ocurrió un error." },
        },
      },
    });

    renderPage();

    fireEvent.click(await screen.findByText("Ejecutar"));
    fireEvent.click(await screen.findByText("Sí, ejecutar"));

    // El detalle específico, no el mensaje genérico del catálogo.
    expect(
      await screen.findByText("Stock insuficiente en la bodega seleccionada."),
    ).toBeTruthy();
    expect(screen.queryByText("Ocurrió un error.")).toBeNull();
  });
});

describe("StockAdjustmentsPage — ZH-LISTING-MAIN-ROW-NUMBER-FIX-07, showRowNumber", () => {
  it('muestra la columna "N°" primero, antes de "N.º", sin reemplazar el número funcional', async () => {
    vi.mocked(stockAdjustmentsService.list).mockResolvedValue({
      items: [adjustment({ status: "Draft" })],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
    });

    renderPage();
    await screen.findByText("AJU-000001");

    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers[0]).toBe("N°");
    expect(headers.indexOf("N°")).toBeLessThan(headers.indexOf("N.º"));
    // El número funcional del documento se conserva intacto.
    expect(screen.getByText("AJU-000001")).toBeTruthy();
  });

  it('la primera fila muestra "1" en la columna N°', async () => {
    vi.mocked(stockAdjustmentsService.list).mockResolvedValue({
      items: [adjustment({ status: "Draft" })],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
    });

    renderPage();
    await screen.findByText("AJU-000001");

    const rows = screen.getAllByRole("row").slice(1);
    const firstCell = rows[0].querySelectorAll("td")[0];
    expect(firstCell.textContent).toBe("1");
  });
});
