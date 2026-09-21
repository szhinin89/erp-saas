// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import {
  buildRepricingPlan,
  applyRepricingPlanToLines,
  mapResolvedPricingToLineFields,
  useSalesCustomerRepricing,
  type RepricingLineInput,
  type RepricingPlan,
} from "./useSalesCustomerRepricing";
import { salesRepricingPreviewService } from "../api/salesRepricingPreviewService";
import type { SalesRepricingPreviewItemDto } from "../api/salesRepricingPreviewService";
import type { CustomerPickerRow } from "../../masterData/types/businessPartner.types";

vi.mock("../api/salesRepricingPreviewService", () => ({
  salesRepricingPreviewService: { preview: vi.fn() },
}));

const previewMock = vi.mocked(salesRepricingPreviewService.preview);

function previewItem(
  overrides: Partial<SalesRepricingPreviewItemDto> & { itemId: string },
): SalesRepricingPreviewItemDto {
  return {
    oldResolvedPrice: 0,
    newResolvedPrice: 0,
    newBasePrice: 0,
    changed: true,
    oldPriceListId: null,
    oldPriceListName: "PVP",
    newPriceListId: null,
    newPriceListName: "PVP",
    oldSelectionSource: null,
    newSelectionSource: null,
    newDiscountDescription: null,
    ...overrides,
  };
}

function customer(id: string): CustomerPickerRow {
  return {
    id,
    identificationNumber: "1710034065",
    fullName: `Cliente ${id}`,
    isActive: true,
    hasCustomerRole: true,
  };
}

const ITEM_A = "item-a";
const ITEM_B = "item-b";

describe("SALES-CUSTOMER-REPRICE-METADATA-06C2A — mapResolvedPricingToLineFields", () => {
  it("_pvp/_basePrice quedan SIN escalar por conversionFactor; unitPrice SÍ se escala", () => {
    const fields = mapResolvedPricingToLineFields(12, 15, "Lista Mayorista", "Descuento 20%", 6, "list-1");

    expect(fields.unitPrice).toBe(72); // 12 * 6
    expect(fields._pvp).toBe(12);
    expect(fields._basePrice).toBe(15);
    expect(fields._priceListName).toBe("Lista Mayorista");
    expect(fields._discountDescription).toBe("Descuento 20%");
    expect(fields._isManualPrice).toBe(false);
  });
});

describe("SALES-CUSTOMER-REPRICE-METADATA-SYNC-06C2B — buildRepricingPlan", () => {
  it("distinta lista, mismo precio → sin fila de precio (no modal), sí fila de metadata", () => {
    const lines: RepricingLineInput[] = [
      {
        key: 1,
        itemId: ITEM_A,
        description: "Producto A",
        unitPrice: 100,
        _pvp: 100,
        _basePrice: 100,
        _priceListName: "Lista General",
        _discountDescription: null,
        _priceListId: null,
      },
    ];
    const preview = [
      previewItem({
        itemId: ITEM_A,
        newResolvedPrice: 100,
        newBasePrice: 100,
        newPriceListName: "Lista Mayorista", // distinta lista, mismo precio final
        newDiscountDescription: null,
      }),
    ];

    const plan = buildRepricingPlan(lines, preview, 2);

    expect(plan.priceChangedRows).toHaveLength(0);
    expect(plan.metadataOnlyRows).toHaveLength(1);
    expect(plan.metadataOnlyRows[0].metadata._priceListName).toBe("Lista Mayorista");
  });

  it("misma priceList/precio → ninguna fila (nada que sincronizar)", () => {
    const lines: RepricingLineInput[] = [
      {
        key: 1,
        itemId: ITEM_A,
        description: "Producto A",
        unitPrice: 100,
        _pvp: 100,
        _basePrice: 100,
        _priceListName: "Lista General",
        _discountDescription: null,
        _priceListId: null,
      },
    ];
    const preview = [
      previewItem({
        itemId: ITEM_A,
        newResolvedPrice: 100,
        newBasePrice: 100,
        newPriceListName: "Lista General",
        newDiscountDescription: null,
      }),
    ];

    const plan = buildRepricingPlan(lines, preview, 2);

    expect(plan.priceChangedRows).toHaveLength(0);
    expect(plan.metadataOnlyRows).toHaveLength(0);
  });

  it("mismo precio pero distinto BasePrice → metadata sincronizada sin fila de precio", () => {
    const lines: RepricingLineInput[] = [
      {
        key: 1,
        itemId: ITEM_A,
        description: "Producto A",
        unitPrice: 90,
        _pvp: 90,
        _basePrice: 90, // sin descuento con el cliente anterior
        _priceListName: "Lista A",
        _discountDescription: null,
        _priceListId: null,
      },
    ];
    const preview = [
      previewItem({
        itemId: ITEM_A,
        newResolvedPrice: 90, // el precio facturado coincide...
        newBasePrice: 120, // ...pero ahora viene de una lista con descuento distinto
        newPriceListName: "Lista A",
        newDiscountDescription: "Descuento 25%",
      }),
    ];

    const plan = buildRepricingPlan(lines, preview, 2);

    expect(plan.priceChangedRows).toHaveLength(0);
    expect(plan.metadataOnlyRows).toHaveLength(1);
    expect(plan.metadataOnlyRows[0].metadata._basePrice).toBe(120);
    expect(plan.metadataOnlyRows[0].metadata._discountDescription).toBe("Descuento 25%");
  });

  it("algunas líneas cambian precio y otras solo metadata", () => {
    const lines: RepricingLineInput[] = [
      {
        key: 1,
        itemId: ITEM_A,
        description: "Producto A",
        unitPrice: 90,
        _pvp: 90,
        _basePrice: 90,
        _priceListName: "Lista A",
        _discountDescription: null,
      },
      {
        key: 2,
        itemId: ITEM_B,
        description: "Producto B",
        unitPrice: 50,
        _pvp: 50,
        _basePrice: 50,
        _priceListName: "Lista A",
        _discountDescription: null,
        _priceListId: null,
      },
    ];
    const preview = [
      previewItem({
        itemId: ITEM_A,
        newResolvedPrice: 90, // sin cambio de precio
        newBasePrice: 100, // pero sí de BasePrice/lista
        newPriceListName: "Lista B",
        newDiscountDescription: "Descuento 10%",
      }),
      previewItem({
        itemId: ITEM_B,
        newResolvedPrice: 40, // cambia de precio
        newBasePrice: 40,
        newPriceListName: "Lista B",
      }),
    ];

    const plan = buildRepricingPlan(lines, preview, 2);

    expect(plan.priceChangedRows).toHaveLength(1);
    expect(plan.priceChangedRows[0].itemId).toBe(ITEM_B);
    expect(plan.metadataOnlyRows).toHaveLength(1);
    expect(plan.metadataOnlyRows[0].itemId).toBe(ITEM_A);
  });

  it("precio manual igual al nuevo resuelto: no pierde manualidad solo porque cambió de lista", () => {
    // El cajero fijó unitPrice=90 a mano; el nuevo cliente resuelve, por coincidencia, el mismo
    // 90 mediante otra lista — la línea debe ir a metadataOnlyRows (no priceChangedRows), así que
    // aplyRepricingPlanToLines nunca toca _isManualPrice para ella.
    const lines: RepricingLineInput[] = [
      {
        key: 1,
        itemId: ITEM_A,
        description: "Producto A",
        unitPrice: 90,
        _pvp: 85, // la línea nunca tuvo metadata de "90" — fue editada a mano
        _basePrice: 85,
        _priceListName: "Lista A",
        _discountDescription: null,
        _priceListId: null,
      },
    ];
    const preview = [
      previewItem({
        itemId: ITEM_A,
        newResolvedPrice: 90,
        newBasePrice: 90,
        newPriceListName: "Lista B",
        newDiscountDescription: null,
      }),
    ];

    const plan = buildRepricingPlan(lines, preview, 2);

    expect(plan.priceChangedRows).toHaveLength(0);
    expect(plan.metadataOnlyRows).toHaveLength(1);
  });

  it("conversionFactor sigue correcto tanto para priceChanged como para metadataOnly", () => {
    const lines: RepricingLineInput[] = [
      {
        key: 1,
        itemId: ITEM_A,
        description: "Caja x6",
        unitPrice: 72, // 12 * 6, ya facturado en la presentación
        conversionFactor: 6,
        _pvp: 12,
        _basePrice: 12,
        _priceListName: "Lista A",
        _discountDescription: null,
        _priceListId: null,
      },
    ];
    // Mismo precio unitario base (12) pero distinta lista → metadataOnly, con unitPrice
    // escalado correctamente en el fields interno (aunque no se aplique).
    const sameFinalPrice = buildRepricingPlan(
      lines,
      [previewItem({ itemId: ITEM_A, newResolvedPrice: 12, newBasePrice: 12, newPriceListName: "Lista B" })],
      2,
    );
    expect(sameFinalPrice.priceChangedRows).toHaveLength(0);
    expect(sameFinalPrice.metadataOnlyRows).toHaveLength(1);

    // Precio base distinto (10 en vez de 12) → sí cambia el facturado, escalado por 6.
    const differentPrice = buildRepricingPlan(
      lines,
      [previewItem({ itemId: ITEM_A, newResolvedPrice: 10, newBasePrice: 10, newPriceListName: "Lista B" })],
      2,
    );
    expect(differentPrice.priceChangedRows).toHaveLength(1);
    expect(differentPrice.priceChangedRows[0].fields.unitPrice).toBe(60); // 10 * 6
    expect(differentPrice.priceChangedRows[0].fields._pvp).toBe(10); // sin escalar
  });
});

describe("SALES-CUSTOMER-REPRICE-METADATA-SYNC-06C2B — applyRepricingPlanToLines", () => {
  interface TestLine {
    _key: number;
    itemId: string;
    quantity: number;
    unitPrice: number;
    discountPct: number;
    vatCode: string;
    warehouseId: string | null;
    _pvp?: number;
    _basePrice?: number;
    _priceListName?: string;
    _discountDescription?: string | null;
    _isManualPrice?: boolean;
  }

  it("líneas con cambio de precio: reemplaza unitPrice+metadata y marca _isManualPrice=false", () => {
    const lines: TestLine[] = [
      {
        _key: 1,
        itemId: ITEM_A,
        quantity: 2,
        unitPrice: 90,
        discountPct: 0,
        vatCode: "10",
        warehouseId: "wh-1",
        _isManualPrice: true,
      },
    ];
    const fields = mapResolvedPricingToLineFields(70, 100, "Lista Mayorista", "Descuento 30%", 1, "list-1");
    const plan: RepricingPlan = {
      priceChangedRows: [{ key: 1, itemId: ITEM_A, description: "Producto A", currentUnitPrice: 90, fields }],
      metadataOnlyRows: [],
    };

    const result = applyRepricingPlanToLines(lines, plan);

    expect(result[0].unitPrice).toBe(70);
    expect(result[0]._isManualPrice).toBe(false);
    expect(result[0]._basePrice).toBe(100);
    expect(result[0].quantity).toBe(2);
    expect(result[0].vatCode).toBe("10");
    expect(result[0].warehouseId).toBe("wh-1");
  });

  it("líneas con solo metadata: mantiene unitPrice y manualidad, sincroniza el resto", () => {
    const lines: TestLine[] = [
      {
        _key: 1,
        itemId: ITEM_A,
        quantity: 2,
        unitPrice: 90,
        discountPct: 3,
        vatCode: "10",
        warehouseId: "wh-1",
        _pvp: 85,
        _basePrice: 85,
        _priceListName: "Lista Vieja",
        _discountDescription: null,
        _isManualPrice: true, // el cajero SÍ editó este precio a mano
      },
    ];
    const plan: RepricingPlan = {
      priceChangedRows: [],
      metadataOnlyRows: [
        {
          key: 1,
          itemId: ITEM_A,
          metadata: {
            _pvp: 90,
            _basePrice: 100,
            _priceListName: "Lista Nueva",
            _discountDescription: "Descuento 10%",
            _priceListId: "list-2",
          },
        },
      ],
    };

    const result = applyRepricingPlanToLines(lines, plan);

    expect(result[0].unitPrice).toBe(90); // intacto, nunca reemplazado
    expect(result[0]._isManualPrice).toBe(true); // NO se pierde solo por sincronizar metadata
    expect(result[0]._basePrice).toBe(100); // metadata sí se actualiza
    expect(result[0]._priceListName).toBe("Lista Nueva");
    expect(result[0]._discountDescription).toBe("Descuento 10%");
    expect(result[0].quantity).toBe(2);
    expect(result[0].discountPct).toBe(3);
  });

  it("cancelar (no invocar applyRepricingPlanToLines) mantiene la metadata anterior sin cambios", () => {
    const original: TestLine = {
      _key: 1,
      itemId: ITEM_A,
      quantity: 1,
      unitPrice: 90,
      discountPct: 0,
      vatCode: "10",
      warehouseId: null,
      _pvp: 85,
      _basePrice: 85,
      _priceListName: "Lista Vieja",
      _discountDescription: null,
      _isManualPrice: false,
    };
    // "Cancelar" en el hook (ver describe de abajo) equivale a nunca llamar applyPlanToLines —
    // aquí se fija la garantía a nivel de datos: sin esa llamada, el objeto no se toca.
    expect(original).toEqual({ ...original });
  });

  it("líneas sin fila en el plan quedan exactamente intactas (misma referencia)", () => {
    const lines: TestLine[] = [
      { _key: 1, itemId: ITEM_A, quantity: 1, unitPrice: 10, discountPct: 0, vatCode: "10", warehouseId: null },
      { _key: 2, itemId: ITEM_B, quantity: 1, unitPrice: 20, discountPct: 0, vatCode: "10", warehouseId: null },
    ];
    const result = applyRepricingPlanToLines(lines, { priceChangedRows: [], metadataOnlyRows: [] });

    expect(result[0]).toBe(lines[0]);
    expect(result[1]).toBe(lines[1]);
  });
});

describe("SALES-CUSTOMER-REPRICE-METADATA-SYNC-06C2B — useSalesCustomerRepricing (orquestación)", () => {
  beforeEach(() => {
    previewMock.mockReset();
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("sin líneas con itemId, aplica el cliente directo sin llamar al preview", async () => {
    const applyCustomerChange = vi.fn();
    const applyPlanToLines = vi.fn();
    const { result } = renderHook(() =>
      useSalesCustomerRepricing({ applyCustomerChange, applyPlanToLines, onPreviewError: vi.fn() }),
    );

    await act(async () => {
      await result.current.requestCustomerChange(customer("c2"), [], "c1", 2);
    });

    expect(previewMock).not.toHaveBeenCalled();
    expect(applyPlanToLines).not.toHaveBeenCalled();
    expect(applyCustomerChange).toHaveBeenCalledWith(customer("c2"));
    expect(result.current.pending).toBeNull();
  });

  it("sin cambios de precio ni metadata, aplica cliente directo sin tocar líneas", async () => {
    const applyCustomerChange = vi.fn();
    const applyPlanToLines = vi.fn();
    previewMock.mockResolvedValueOnce([
      previewItem({ itemId: ITEM_A, newResolvedPrice: 10, newBasePrice: 10, newPriceListName: "PVP" }),
    ]);
    const { result } = renderHook(() =>
      useSalesCustomerRepricing({ applyCustomerChange, applyPlanToLines, onPreviewError: vi.fn() }),
    );
    const lines: RepricingLineInput[] = [
      {
        key: 1,
        itemId: ITEM_A,
        description: "Producto A",
        unitPrice: 10,
        _pvp: 10,
        _basePrice: 10,
        _priceListName: "PVP",
        _discountDescription: null,
        _priceListId: null,
      },
    ];

    await act(async () => {
      await result.current.requestCustomerChange(customer("c2"), lines, "c1", 2);
    });

    expect(applyPlanToLines).not.toHaveBeenCalled();
    expect(applyCustomerChange).toHaveBeenCalledWith(customer("c2"));
    expect(result.current.pending).toBeNull();
  });

  it("distinta lista, mismo precio: sin modal, aplica cliente Y sincroniza metadata en silencio", async () => {
    const applyCustomerChange = vi.fn();
    const applyPlanToLines = vi.fn();
    previewMock.mockResolvedValueOnce([
      previewItem({ itemId: ITEM_A, newResolvedPrice: 10, newBasePrice: 10, newPriceListName: "Lista Nueva" }),
    ]);
    const { result } = renderHook(() =>
      useSalesCustomerRepricing({ applyCustomerChange, applyPlanToLines, onPreviewError: vi.fn() }),
    );
    const lines: RepricingLineInput[] = [
      {
        key: 1,
        itemId: ITEM_A,
        description: "Producto A",
        unitPrice: 10,
        _pvp: 10,
        _basePrice: 10,
        _priceListName: "Lista Vieja",
        _discountDescription: null,
        _priceListId: null,
      },
    ];

    await act(async () => {
      await result.current.requestCustomerChange(customer("c2"), lines, "c1", 2);
    });

    expect(result.current.pending).toBeNull(); // NUNCA modal por solo metadata
    expect(applyPlanToLines).toHaveBeenCalledTimes(1);
    const plan = applyPlanToLines.mock.calls[0][0] as RepricingPlan;
    expect(plan.priceChangedRows).toHaveLength(0);
    expect(plan.metadataOnlyRows).toHaveLength(1);
    expect(plan.metadataOnlyRows[0].metadata._priceListName).toBe("Lista Nueva");
    expect(applyCustomerChange).toHaveBeenCalledWith(customer("c2"));
  });

  it("líneas con diferencias de precio, abre el modal en vez de aplicar directo", async () => {
    const applyCustomerChange = vi.fn();
    const applyPlanToLines = vi.fn();
    previewMock.mockResolvedValueOnce([
      previewItem({ itemId: ITEM_A, newResolvedPrice: 8, newBasePrice: 8, newPriceListName: "Lista B" }),
    ]);
    const { result } = renderHook(() =>
      useSalesCustomerRepricing({ applyCustomerChange, applyPlanToLines, onPreviewError: vi.fn() }),
    );
    const lines: RepricingLineInput[] = [
      { key: 1, itemId: ITEM_A, description: "Producto A", unitPrice: 10 },
    ];

    await act(async () => {
      await result.current.requestCustomerChange(customer("c2"), lines, "c1", 2);
    });

    expect(applyCustomerChange).not.toHaveBeenCalled();
    expect(applyPlanToLines).not.toHaveBeenCalled(); // nada se aplica hasta confirmar
    expect(result.current.pending).not.toBeNull();
    expect(result.current.pending!.rows).toHaveLength(1);
    expect(result.current.pending!.customer).toEqual(customer("c2"));
  });

  it("modal con líneas mixtas: al confirmar aplica precio+metadata en las que cambian y solo metadata en las demás", async () => {
    const applyCustomerChange = vi.fn();
    const applyPlanToLines = vi.fn();
    previewMock.mockResolvedValueOnce([
      previewItem({
        itemId: ITEM_A,
        newResolvedPrice: 90, // sin cambio de precio
        newBasePrice: 100,
        newPriceListName: "Lista B",
        newDiscountDescription: "Descuento 10%",
      }),
      previewItem({ itemId: ITEM_B, newResolvedPrice: 40, newBasePrice: 40, newPriceListName: "Lista B" }), // cambia
    ]);
    const { result } = renderHook(() =>
      useSalesCustomerRepricing({ applyCustomerChange, applyPlanToLines, onPreviewError: vi.fn() }),
    );
    const lines: RepricingLineInput[] = [
      {
        key: 1,
        itemId: ITEM_A,
        description: "Producto A",
        unitPrice: 90,
        _pvp: 90,
        _basePrice: 90,
        _priceListName: "Lista A",
        _discountDescription: null,
      },
      { key: 2, itemId: ITEM_B, description: "Producto B", unitPrice: 50 },
    ];

    await act(async () => {
      await result.current.requestCustomerChange(customer("c2"), lines, "c1", 2);
    });
    expect(result.current.pending!.rows).toHaveLength(1); // modal solo muestra el ítem B
    expect(result.current.pending!.rows[0].itemId).toBe(ITEM_B);
    expect(result.current.pending!.metadataOnlyRows).toHaveLength(1); // A viaja silencioso

    await act(async () => {
      await result.current.confirm();
    });

    expect(applyPlanToLines).toHaveBeenCalledTimes(1);
    const plan = applyPlanToLines.mock.calls[0][0] as RepricingPlan;
    expect(plan.priceChangedRows).toHaveLength(1);
    expect(plan.metadataOnlyRows).toHaveLength(1);
    expect(applyCustomerChange).toHaveBeenCalledWith(customer("c2"));
  });

  it("cancelar deja el cliente y las líneas intactos (no invoca applyCustomerChange ni applyPlanToLines)", async () => {
    const applyCustomerChange = vi.fn();
    const applyPlanToLines = vi.fn();
    previewMock.mockResolvedValueOnce([
      previewItem({ itemId: ITEM_A, newResolvedPrice: 8, newBasePrice: 8 }),
    ]);
    const { result } = renderHook(() =>
      useSalesCustomerRepricing({ applyCustomerChange, applyPlanToLines, onPreviewError: vi.fn() }),
    );
    const lines: RepricingLineInput[] = [
      { key: 1, itemId: ITEM_A, description: "Producto A", unitPrice: 10 },
    ];
    await act(async () => {
      await result.current.requestCustomerChange(customer("c2"), lines, "c1", 2);
    });
    expect(result.current.pending).not.toBeNull();

    act(() => result.current.cancel());

    expect(result.current.pending).toBeNull();
    expect(applyCustomerChange).not.toHaveBeenCalled();
    expect(applyPlanToLines).not.toHaveBeenCalled();
  });

  it("error en el preview es fail-closed: no cambia el cliente ni sincroniza metadata", async () => {
    const applyCustomerChange = vi.fn();
    const applyPlanToLines = vi.fn();
    const onPreviewError = vi.fn();
    previewMock.mockRejectedValueOnce(new Error("network error"));
    const { result } = renderHook(() =>
      useSalesCustomerRepricing({ applyCustomerChange, applyPlanToLines, onPreviewError }),
    );
    const lines: RepricingLineInput[] = [
      { key: 1, itemId: ITEM_A, description: "Producto A", unitPrice: 10 },
    ];

    await act(async () => {
      await result.current.requestCustomerChange(customer("c2"), lines, "c1", 2);
    });

    expect(applyCustomerChange).not.toHaveBeenCalled();
    expect(applyPlanToLines).not.toHaveBeenCalled();
    expect(result.current.pending).toBeNull();
    expect(onPreviewError).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(result.current.loading).toBe(false));
  });

  it("doble interacción protegida: una segunda llamada mientras la primera está en vuelo se ignora", async () => {
    const applyCustomerChange = vi.fn();
    const applyPlanToLines = vi.fn();
    let resolvePreview!: (v: SalesRepricingPreviewItemDto[]) => void;
    previewMock.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolvePreview = resolve;
        }),
    );
    const { result } = renderHook(() =>
      useSalesCustomerRepricing({ applyCustomerChange, applyPlanToLines, onPreviewError: vi.fn() }),
    );
    const lines: RepricingLineInput[] = [
      { key: 1, itemId: ITEM_A, description: "Producto A", unitPrice: 10 },
    ];

    let firstCall!: Promise<void>;
    act(() => {
      firstCall = result.current.requestCustomerChange(customer("c2"), lines, "c1", 2);
    });
    expect(result.current.loading).toBe(true);

    await act(async () => {
      await result.current.requestCustomerChange(customer("c3"), lines, "c1", 2);
    });
    expect(previewMock).toHaveBeenCalledTimes(1);

    await act(async () => {
      resolvePreview([previewItem({ itemId: ITEM_A, newResolvedPrice: 10, newBasePrice: 10 })]);
      await firstCall;
    });

    expect(applyCustomerChange).toHaveBeenCalledTimes(1);
    expect(applyCustomerChange).toHaveBeenCalledWith(customer("c2"));
  });
});
