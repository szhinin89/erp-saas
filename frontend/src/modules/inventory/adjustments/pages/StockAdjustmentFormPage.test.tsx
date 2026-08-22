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
import { StockAdjustmentFormPage } from "./StockAdjustmentFormPage";
import { stockAdjustmentsService } from "../api/stockAdjustmentsService";
import { inventoryAdjustmentReasonsService } from "../../adjustmentReasons/api/inventoryAdjustmentReasonsService";
import { warehouseService } from "../../warehouses/api/warehouseService";
import { stockService } from "../../stock/api/stockService";
import { itemLookupFacade } from "../../../items/facades/itemLookupFacade";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import type { StockAdjustmentDto } from "../types";
import type { InventoryAdjustmentReasonDto } from "../../adjustmentReasons/types";

const routeParams: { id?: string } = {};

vi.mock("react-router-dom", async () => {
  const actual =
    await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return {
    ...actual,
    useNavigate: () => vi.fn(),
    useParams: () => routeParams,
  };
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

vi.mock("../../stock/api/stockService", () => ({
  stockService: { getStock: vi.fn() },
}));

vi.mock("../../../items/facades/itemLookupFacade", () => ({
  itemLookupFacade: { search: vi.fn(), getById: vi.fn() },
}));

vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}));

const REASON_INGRESO: InventoryAdjustmentReasonDto = {
  id: "rsn-in",
  code: "SOBRA",
  name: "Sobrante de conteo",
  allowedMovementType: "Ingreso",
  requiresNotes: false,
  isActive: true,
  sortOrder: 1,
};

const REASON_EGRESO: InventoryAdjustmentReasonDto = {
  id: "rsn-eg",
  code: "MERMA",
  name: "Merma",
  allowedMovementType: "Egreso",
  requiresNotes: true,
  isActive: true,
  sortOrder: 2,
};

const ITEM = {
  id: "item-1",
  sku: "SKU-1",
  shortName: "Arroz 1kg",
  description: "Arroz",
  tracksStock: true,
  defaultUomCode: "UN",
};

const CAJA_X12 = {
  id: "pk-12",
  name: "CAJA X12",
  level: 2,
  baseQuantity: 12,
  uomCode: "CJ",
  uomAbbrev: "CJ",
  barcode: null,
  weight: null,
  isBaseUnit: false,
  isPurchaseDefault: false,
  isSaleDefault: false,
  isActive: true,
};

function grant(granted: string[] | "all" = "all") {
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
        <StockAdjustmentFormPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

/** Agrega una línea a través del picker real (búsqueda → selección del resultado). */
async function addProductLine() {
  fireEvent.change(
    screen.getByLabelText("Buscar producto por SKU o nombre..."),
    { target: { value: "arroz" } },
  );
  fireEvent.click(await screen.findByText("Arroz 1kg", {}, { timeout: 3000 }));
  await screen.findByText("SKU-1");
}

function adjustment(over: Partial<StockAdjustmentDto>): StockAdjustmentDto {
  return {
    id: "adj-1",
    adjustmentNumber: "AJU-000001",
    warehouseId: "wh-1",
    warehouseName: "Bodega Central",
    movementType: "Ingreso",
    reasonId: "rsn-in",
    reasonName: "Sobrante de conteo",
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

beforeEach(() => {
  delete routeParams.id;
  grant("all");
  vi.mocked(warehouseService.list).mockResolvedValue([
    { id: "wh-1", name: "Bodega Central", branchId: "b1", isActive: true },
  ] as unknown as Awaited<ReturnType<typeof warehouseService.list>>);
  vi.mocked(inventoryAdjustmentReasonsService.list).mockResolvedValue([
    REASON_INGRESO,
    REASON_EGRESO,
  ]);
  vi.mocked(itemLookupFacade.search).mockResolvedValue({
    items: [ITEM],
    totalCount: 1,
    pageNumber: 1,
    pageSize: 12,
  } as unknown as Awaited<ReturnType<typeof itemLookupFacade.search>>);
  vi.mocked(itemLookupFacade.getById).mockResolvedValue({
    ...ITEM,
    packagingLevels: [CAJA_X12],
  } as unknown as Awaited<ReturnType<typeof itemLookupFacade.getById>>);
  vi.mocked(stockService.getStock).mockResolvedValue([
    {
      id: "s1",
      itemId: "item-1",
      warehouseId: "wh-1",
      quantity: 5,
      reservedQuantity: 0,
      availableQuantity: 5,
      totalStockValue: 10,
      averageCost: 2,
      lastUpdatedAt: "2026-08-01T00:00:00Z",
    },
  ]);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("StockAdjustmentFormPage — creación", () => {
  it("filtra los motivos por el tipo de movimiento seleccionado", async () => {
    renderPage();
    // Ingreso: solo el motivo de Ingreso (los de Egreso no deben ser seleccionables).
    expect(await screen.findByText("SOBRA — Sobrante de conteo")).toBeTruthy();
    expect(screen.queryByText("MERMA — Merma")).toBeNull();

    fireEvent.change(screen.getByLabelText("Tipo de movimiento"), {
      target: { value: "Egreso" },
    });
    expect(await screen.findByText("MERMA — Merma")).toBeTruthy();
    expect(screen.queryByText("SOBRA — Sobrante de conteo")).toBeNull();
  });

  it("exige motivo antes de guardar el borrador", async () => {
    renderPage();
    await screen.findByText("SOBRA — Sobrante de conteo");

    fireEvent.change(screen.getByLabelText("Bodega"), {
      target: { value: "wh-1" },
    });
    fireEvent.click(screen.getByText("Guardar borrador"));

    expect(
      await screen.findByText("Seleccione un motivo de ajuste."),
    ).toBeTruthy();
    expect(stockAdjustmentsService.create).not.toHaveBeenCalled();
  });

  it("Ingreso: exige costo unitario base y luego crea con el payload correcto", async () => {
    vi.mocked(stockAdjustmentsService.create).mockResolvedValue(
      adjustment({ id: "adj-new" }),
    );
    renderPage();
    await screen.findByText("SOBRA — Sobrante de conteo");

    fireEvent.change(screen.getByLabelText("Bodega"), {
      target: { value: "wh-1" },
    });
    fireEvent.change(screen.getByLabelText("Motivo"), {
      target: { value: "rsn-in" },
    });
    await addProductLine();

    // El costo se preselecciona desde el costo promedio del stock (2) — se borra para
    // comprobar que sin costo el guardado se detiene antes de llamar al backend.
    const costInput = screen.getByLabelText("Costo unitario base Arroz 1kg");
    fireEvent.change(costInput, { target: { value: "" } });
    fireEvent.blur(costInput);
    fireEvent.click(screen.getByText("Guardar borrador"));
    expect(
      await screen.findByText(
        "En un Ingreso, cada línea requiere un costo unitario base mayor a cero.",
      ),
    ).toBeTruthy();
    expect(stockAdjustmentsService.create).not.toHaveBeenCalled();

    fireEvent.change(costInput, { target: { value: "3.50" } });
    fireEvent.blur(costInput);
    fireEvent.click(screen.getByText("Guardar borrador"));

    await waitFor(() =>
      expect(stockAdjustmentsService.create).toHaveBeenCalledTimes(1),
    );
    const payload = vi.mocked(stockAdjustmentsService.create).mock.calls[0][0];
    expect(payload).toMatchObject({
      warehouseId: "wh-1",
      warehouseName: "Bodega Central",
      movementType: "Ingreso",
      reasonId: "rsn-in",
      notes: null,
    });
    expect(payload.lines).toEqual([
      {
        itemId: "item-1",
        itemName: "Arroz 1kg",
        packagingLevelId: null,
        quantity: 1,
        unitCostBase: 3.5,
        lineNotes: null,
      },
    ]);
    // Nunca se envía contexto de tenant/empresa/sucursal: lo resuelve el backend.
    expect(payload).not.toHaveProperty("companyId");
    expect(payload).not.toHaveProperty("branchId");
    expect(payload).not.toHaveProperty("tenantId");
  });

  it("Egreso: sin input de costo, avisa stock insuficiente y no envía costo manual", async () => {
    vi.mocked(stockAdjustmentsService.create).mockResolvedValue(
      adjustment({ id: "adj-eg", movementType: "Egreso" }),
    );
    renderPage();
    await screen.findByText("SOBRA — Sobrante de conteo");

    fireEvent.change(screen.getByLabelText("Tipo de movimiento"), {
      target: { value: "Egreso" },
    });
    fireEvent.change(screen.getByLabelText("Bodega"), {
      target: { value: "wh-1" },
    });
    await screen.findByText("MERMA — Merma");
    fireEvent.change(screen.getByLabelText("Motivo"), {
      target: { value: "rsn-eg" },
    });
    await addProductLine();

    // En Egreso el costo NO es editable: el backend lo deriva del promedio móvil.
    expect(screen.queryByLabelText("Costo unitario base Arroz 1kg")).toBeNull();
    expect(screen.getByText("Lo calcula el sistema")).toBeTruthy();

    // Stock actual = 5; con 10 unidades el aviso aparece, pero no bloquea el guardado.
    const qty = screen.getByLabelText("Cantidad Arroz 1kg");
    fireEvent.change(qty, { target: { value: "10" } });
    fireEvent.blur(qty);
    expect(await screen.findByText("Stock insuficiente")).toBeTruthy();
    expect(
      screen.getByText(
        "Hay líneas con stock insuficiente en la bodega seleccionada.",
      ),
    ).toBeTruthy();

    // El motivo exige observación (requiresNotes) — se completa para poder guardar.
    fireEvent.change(screen.getByLabelText("Observaciones"), {
      target: { value: "Producto vencido" },
    });
    fireEvent.click(screen.getByText("Guardar borrador"));

    await waitFor(() =>
      expect(stockAdjustmentsService.create).toHaveBeenCalledTimes(1),
    );
    const payload = vi.mocked(stockAdjustmentsService.create).mock.calls[0][0];
    expect(payload.lines[0].unitCostBase).toBeNull();
    expect(payload.notes).toBe("Producto vencido");
  });

  it("exige observación cuando el motivo tiene requiresNotes", async () => {
    renderPage();
    await screen.findByText("SOBRA — Sobrante de conteo");

    fireEvent.change(screen.getByLabelText("Tipo de movimiento"), {
      target: { value: "Egreso" },
    });
    fireEvent.change(screen.getByLabelText("Bodega"), {
      target: { value: "wh-1" },
    });
    await screen.findByText("MERMA — Merma");
    fireEvent.change(screen.getByLabelText("Motivo"), {
      target: { value: "rsn-eg" },
    });
    await addProductLine();

    fireEvent.click(screen.getByText("Guardar borrador"));

    expect(
      await screen.findByText(
        "El motivo seleccionado exige registrar una observación.",
      ),
    ).toBeTruthy();
    expect(stockAdjustmentsService.create).not.toHaveBeenCalled();
  });

  it("con presentación CAJA X12 y cantidad 1 muestra la equivalencia en 12 unidades base", async () => {
    renderPage();
    await screen.findByText("SOBRA — Sobrante de conteo");

    fireEvent.change(screen.getByLabelText("Bodega"), {
      target: { value: "wh-1" },
    });
    await addProductLine();

    // Sin presentación: 1 unidad base.
    expect(screen.getByText(/Equivale a 1\.00 unidades base/)).toBeTruthy();

    fireEvent.change(screen.getByLabelText("Presentación Arroz 1kg"), {
      target: { value: "pk-12" },
    });

    expect(
      await screen.findByText(/Equivale a 12\.00 unidades base/),
    ).toBeTruthy();
  });

  it("aplica al campo el error de validación del backend, sin mensaje genérico", async () => {
    vi.mocked(stockAdjustmentsService.create).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 422,
        data: {
          data: { errors: { reasonId: ["El motivo está inactivo."] } },
        },
      },
    });
    renderPage();
    await screen.findByText("SOBRA — Sobrante de conteo");

    fireEvent.change(screen.getByLabelText("Bodega"), {
      target: { value: "wh-1" },
    });
    fireEvent.change(screen.getByLabelText("Motivo"), {
      target: { value: "rsn-in" },
    });
    await addProductLine();
    fireEvent.click(screen.getByText("Guardar borrador"));

    expect(await screen.findByText("El motivo está inactivo.")).toBeTruthy();
  });
});

describe("StockAdjustmentFormPage — documento existente", () => {
  it("queda en solo lectura cuando el ajuste está Ejecutado", async () => {
    routeParams.id = "adj-1";
    vi.mocked(stockAdjustmentsService.getById).mockResolvedValue(
      adjustment({
        status: "Executed",
        executedAt: "2026-08-02T10:00:00Z",
        lines: [
          {
            id: "l1",
            itemId: "item-1",
            itemName: "Arroz 1kg",
            packagingLevelId: null,
            uomCode: "UN",
            baseUomCode: "UN",
            conversionFactor: 1,
            quantity: 2,
            quantityInBaseUom: 2,
            unitCostBase: 4,
            totalCost: 8,
            currentStockBefore: 5,
            currentStockAfter: 7,
            lineNotes: null,
          },
        ],
      }),
    );

    renderPage();

    await screen.findByText(
      "Este ajuste ya no es un borrador: se muestra en modo consulta.",
    );
    // Sin picker, sin guardar, sin quitar línea; cabecera y línea deshabilitadas.
    expect(
      screen.queryByLabelText("Buscar producto por SKU o nombre..."),
    ).toBeNull();
    expect(screen.queryByText("Guardar borrador")).toBeNull();
    expect(screen.queryByText("Quitar línea")).toBeNull();
    expect(
      (screen.getByLabelText("Bodega") as HTMLSelectElement).disabled,
    ).toBe(true);
    expect(
      (screen.getByLabelText("Observaciones") as HTMLTextAreaElement).disabled,
    ).toBe(true);
    expect(
      (screen.getByLabelText("Cantidad Arroz 1kg") as HTMLInputElement).disabled,
    ).toBe(true);
    // Ejecutado: se ofrece Anular, nunca Ejecutar de nuevo.
    expect(screen.getByText("Anular")).toBeTruthy();
    expect(screen.queryByText("Ejecutar")).toBeNull();
  });

  it("queda en solo lectura cuando el ajuste está Anulado", async () => {
    routeParams.id = "adj-2";
    vi.mocked(stockAdjustmentsService.getById).mockResolvedValue(
      adjustment({
        status: "Cancelled",
        cancelledAt: "2026-08-03T10:00:00Z",
        cancelledReason: "Error de digitación",
      }),
    );

    renderPage();

    await screen.findByText(
      "Este ajuste ya no es un borrador: se muestra en modo consulta.",
    );
    expect(screen.queryByText("Guardar borrador")).toBeNull();
    expect(screen.queryByText("Ejecutar")).toBeNull();
    expect(screen.queryByText("Anular")).toBeNull();
  });

  it("Ejecutar desde Borrador confirma y llama a execute(id)", async () => {
    routeParams.id = "adj-1";
    vi.mocked(stockAdjustmentsService.getById).mockResolvedValue(
      adjustment({ status: "Draft" }),
    );
    vi.mocked(stockAdjustmentsService.execute).mockResolvedValue(
      adjustment({ status: "Executed" }),
    );

    renderPage();

    fireEvent.click(await screen.findByText("Ejecutar"));
    expect(stockAdjustmentsService.execute).not.toHaveBeenCalled();
    fireEvent.click(await screen.findByText("Sí, ejecutar"));

    await waitFor(() =>
      expect(stockAdjustmentsService.execute).toHaveBeenCalledWith("adj-1"),
    );
  });
});
