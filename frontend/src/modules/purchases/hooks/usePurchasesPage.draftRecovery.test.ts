// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import React from "react";
import { I18nProvider } from "../../../i18n/i18n";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { usePurchasesPage } from "./usePurchasesPage";
import { purchaseService, type PurchaseInvoiceDto } from "../api/purchaseService";
import type { PurchaseDraftLineDto } from "../api/purchaseReceptionService";
import { businessPartnerLookupFacade } from "../../masterData/facades/businessPartnerLookupFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";

/**
 * PURCHASE-DRAFT-RECOVERY — un borrador se guarda incompleto, se cierra y se reabre sin perder
 * resolución de productos, evidencia XML, cuotas manuales ni intención de retención.
 */

vi.mock("../api/purchaseService", () => ({
  purchaseService: {
    list: vi.fn(),
    getById: vi.fn(),
    getByAccessKey: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    applyDiscount: vi.fn(),
    allocateFreight: vi.fn(),
    recalculate: vi.fn(),
    distributeCost: vi.fn(),
    confirm: vi.fn(),
    cancel: vi.fn(),
    retentionPreview: vi.fn(),
    getItemContext: vi.fn(),
    supplierReport: vi.fn(),
    getTaxSummaries: vi.fn(),
  },
}));

vi.mock("../../retentions/facades/purchaseRetentionFacade", () => ({
  purchaseRetentionFacade: {
    getForPurchase: vi.fn().mockResolvedValue(null),
    getElectronicXmlBlob: vi.fn(),
    getRidePdfBlob: vi.fn(),
  },
  SRI_ANNULMENT_REQUIRED_CODE: "ELECTRONIC_DOCUMENT_REQUIRES_SRI_ANNULMENT",
}));

vi.mock("../../emissionPoints/facades/emissionPointLookupFacade", () => ({
  emissionPointLookupFacade: { list: vi.fn().mockResolvedValue([]) },
}));

vi.mock("../../../lib/download", () => ({ downloadBlob: vi.fn() }));

vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: vi.fn() }));

vi.mock("../api/purchaseReceptionService", () => ({
  purchaseReceptionService: {
    importTxt: vi.fn(),
    downloadXml: vi.fn(),
    getXmlView: vi.fn(),
    getLineMatch: vi.fn(),
    createDraft: vi.fn(),
  },
}));

vi.mock("../../items/facades/itemLookupFacade", () => ({
  itemLookupFacade: {
    search: vi.fn().mockResolvedValue({ items: [], total: 0 }),
    getById: vi.fn(),
  },
}));

vi.mock("../../items/facades/itemTypeLookupFacade", () => ({
  useItemTypeOptions: () => ({ data: [], loading: false, error: null }),
}));

vi.mock("../../masterData/facades/businessPartnerLookupFacade", () => ({
  businessPartnerLookupFacade: { getBusinessPartner: vi.fn() },
}));

vi.mock("../../masterData/facades/businessPartnerPurchaseSettingsFacade", () => ({
  businessPartnerPurchaseSettingsFacade: {
    getPurchaseSettings: vi.fn().mockResolvedValue(null),
  },
}));

vi.mock("../../inventory/facades/warehouseLookupFacade", () => ({
  warehouseLookupFacade: { list: vi.fn().mockResolvedValue([]) },
}));

vi.mock("../../masterData/facades/paymentTermLookupFacade", () => ({
  paymentTermLookupFacade: { list: vi.fn().mockResolvedValue([]), getById: vi.fn() },
}));

vi.mock("../../items/facades/sriLookupFacade", () => ({
  sriLookupFacade: {
    docTypes: vi
      .fn()
      .mockResolvedValue([{ code: "01", name: "Factura", shortName: "FAC", isElectronic: true }]),
    uoms: vi.fn().mockResolvedValue([]),
    vatRates: vi.fn().mockResolvedValue([]),
    iceRates: vi.fn().mockResolvedValue([]),
    retentionCodes: vi.fn().mockResolvedValue([]),
    taxSupportCodes: vi.fn().mockResolvedValue([]),
    paymentMethods: vi.fn().mockResolvedValue([]),
    idTypes: vi.fn().mockResolvedValue([]),
    supplierTypes: vi.fn().mockResolvedValue([]),
    taxRegimes: vi.fn().mockResolvedValue([]),
  },
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    info: vi.fn(),
    confirm: vi.fn(),
  },
}));

function xmlSource(overrides: Partial<PurchaseDraftLineDto> = {}): PurchaseDraftLineDto {
  return {
    purchaseReceptionLineId: "rl-1",
    itemId: null,
    itemMatchStatus: "PENDING",
    description: "GALLETA XML",
    quantity: 2,
    unitPrice: 10,
    vatCode: "4",
    warehouseId: null,
    notes: null,
    discountPct: 0,
    iceCode: null,
    supplierCode: "PROV-001",
    supplierAuxCode: "7861234567890",
    discount: 0,
    lineSubtotal: 20,
    taxCode: "2",
    vatPercentage: 15,
    taxValue: 3,
    totalLine: 20,
    packagingLevelId: null,
    uomCode: "UNIT",
    baseUomCode: "UNIT",
    conversionFactor: 1,
    quantityInBaseUom: 2,
    taxes: [{ taxCode: "2", taxRateCode: "4", tarifa: 15, taxableBase: 20, taxAmount: 3 }],
    additionalFields: [{ name: "Lote", value: "L-01", sortOrder: 1 }],
    ...overrides,
  } as PurchaseDraftLineDto;
}

function invoiceLine(overrides: Record<string, unknown> = {}) {
  return {
    id: "line-1",
    itemId: null,
    description: "GALLETA XML",
    quantity: 2,
    unitPrice: 10,
    vatCode: "4",
    discountPct: 0,
    iceCode: null,
    warehouseId: null,
    notes: null,
    taxes: [],
    purchaseReceptionLineId: "rl-1",
    xmlSource: xmlSource(),
    ...overrides,
  };
}

function buildInvoice(overrides: Partial<PurchaseInvoiceDto> = {}): PurchaseInvoiceDto {
  return {
    id: "purchase-1",
    supplierId: "supplier-1",
    supplierName: "Proveedor Uno",
    supplierTaxId: "0999999999001",
    docTypeCode: "01",
    invoiceNumber: "001-001-000000123",
    issueDate: "2026-10-01",
    accessKey: null,
    authorizationNumber: null,
    authorizationDate: null,
    taxSupportCode: null,
    sriPaymentMethodCode: null,
    sriPaymentMethodName: null,
    currencyCode: "USD",
    exchangeRate: 1,
    purchaseOrderId: null,
    purchaseOrderNumber: null,
    globalWarehouseId: "wh-1",
    paymentTermId: "term-1",
    paymentTermName: "Contado",
    paymentTermInstallments: 1,
    paymentTermDaysBetween: 0,
    creditTermDays: 0,
    dueDate: null,
    notes: null,
    status: "Draft",
    cancelReason: null,
    cancelledAt: null,
    cancelledBy: null,
    subtotal: 20,
    totalDiscount: 0,
    totalIce: 0,
    totalVat: 3,
    totalFreight: 0,
    totalOtherCosts: 0,
    grandTotal: 23,
    totalIrbpnr: 0,
    lines: [invoiceLine()] as unknown as PurchaseInvoiceDto["lines"],
    paymentSchedules: [],
    createdAt: "2026-10-01T10:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

function wrapper({ children }: { children: React.ReactNode }) {
  return React.createElement(I18nProvider, null, children);
}

beforeEach(() => {
  vi.clearAllMocks();
  useActiveBranchStore.setState({
    branch: { id: "branch-1", name: "Matriz", isMainBranch: true },
  });
  vi.mocked(purchaseService.list).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 25 });
  vi.mocked(purchaseService.getByAccessKey).mockResolvedValue({
    exists: false,
    purchaseId: null,
    invoiceNumber: null,
    supplierName: null,
    accessKey: null,
  });
  vi.mocked(purchaseService.retentionPreview).mockResolvedValue({
    lines: [],
    totalRetainedVat: 0,
    totalRetainedIncome: 0,
    totalRetainedIsd: 0,
    totalRetained: 0,
    skipReason: "No aplica",
  });
  vi.mocked(purchaseService.getItemContext).mockResolvedValue({
    itemId: "item-1",
    sku: "A1",
    shortName: "ARROZ",
    description: "ARROZ 1KG",
    baseUomCode: "UNIT",
    participatesInInventory: false,
    packagingLevels: [],
    supplierCode: null,
    currentStock: 0,
    availableStock: 0,
    reservedStock: 0,
    averageCost: 0,
    lastPurchaseCost: 0,
    pvp: 0,
    previousPrice: 0,
    maxDiscountPercent: 0,
    purchaseVatCode: "4",
    vatPercent: 15,
    exciseTaxCode: null,
    icePercent: 0,
    hasVat: true,
    hasIce: false,
    costMargin: 0,
    costMarginPercent: 0,
  });
  vi.mocked(businessPartnerLookupFacade.getBusinessPartner).mockResolvedValue({
    id: "supplier-1",
    identificationNumber: "0999999999001",
    legalName: "Proveedor Uno",
    tradeName: null,
    isActive: true,
    roles: [],
  } as unknown as Awaited<ReturnType<typeof businessPartnerLookupFacade.getBusinessPartner>>);
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
});

async function setupLoaded(overrides: Partial<PurchaseInvoiceDto> = {}) {
  vi.mocked(purchaseService.getById).mockResolvedValue(buildInvoice(overrides));
  vi.mocked(purchaseService.update).mockResolvedValue(buildInvoice(overrides));
  const { result } = renderHook(() => usePurchasesPage(), { wrapper });
  await act(async () => {
    await result.current.loadForEdit("purchase-1");
  });
  await waitFor(() => expect(result.current.editing?.id).toBe("purchase-1"));
  await waitFor(() => expect(result.current.canUseSriDocTypes).toBe(true));
  return result;
}

describe("PURCHASE-DRAFT-RECOVERY B1/F1 — guardar con pendientes y reabrir", () => {
  const resolvedLine = invoiceLine({
    id: "line-ok",
    itemId: "item-1",
    description: "ARROZ 1KG",
    purchaseReceptionLineId: "rl-ok",
    xmlSource: xmlSource({
      purchaseReceptionLineId: "rl-ok",
      itemId: "item-1",
      itemMatchStatus: "MANUALLY_MATCHED",
      supplierCode: "PROV-OK",
    }),
  });

  it("reabre mostrando solo los pendientes reales, con su evidencia XML original", async () => {
    const result = await setupLoaded({
      lines: [resolvedLine, invoiceLine()] as unknown as PurchaseInvoiceDto["lines"],
    });

    expect(result.current.pendingProductSources).toHaveLength(1);
    const pending = result.current.pendingProductSources[0];
    expect(pending.rowId).toBe("rl-1");
    expect(pending.supplierCode).toBe("PROV-001");
    expect(pending.supplierAuxCode).toBe("7861234567890");
    expect(result.current.lines[0].itemId).toBe("item-1");
    expect(result.current.lines[1].xmlAdditionalFields).toEqual([{ name: "Lote", value: "L-01" }]);
    expect(purchaseService.getById).toHaveBeenCalledTimes(1);
  });

  it("Guardar no se bloquea por productos pendientes; Confirmar sí", async () => {
    const result = await setupLoaded({
      lines: [resolvedLine, invoiceLine()] as unknown as PurchaseInvoiceDto["lines"],
    });

    expect(result.current.hasLineSaveBlockers).toBe(false);
    expect(result.current.hasLineReadinessBlockers).toBe(true);

    await act(async () => {
      await result.current.handleSave();
    });
    expect(purchaseService.update).toHaveBeenCalledTimes(1);
    const payload = vi.mocked(purchaseService.update).mock.calls[0][1];
    expect(payload.lines[1].itemId ?? null).toBeNull();
    expect(payload.lines[1].purchaseReceptionLineId).toBe("rl-1");
  });

  it("Confirmar aplica su propia regla: un borrador incompleto no llama al backend", async () => {
    const result = await setupLoaded({
      lines: [resolvedLine, invoiceLine()] as unknown as PurchaseInvoiceDto["lines"],
    });

    await act(async () => {
      await result.current.handleConfirm();
    });

    expect(purchaseService.confirm).not.toHaveBeenCalled();
    expect(result.current.saveError).toBe(result.current.confirmBlockedTitle);
  });
});

describe("PURCHASE-DRAFT-RECOVERY D1 — match aprendido después de guardar", () => {
  it("recupera el producto y la presentación aprendidos como cambio sin guardar", async () => {
    const result = await setupLoaded({
      lines: [
        invoiceLine({
          xmlSource: xmlSource({
            itemId: "item-1",
            itemMatchStatus: "AUTO_MATCHED",
            packagingLevelId: "pkg-caja",
            uomCode: "CAJA",
            conversionFactor: 12,
          }),
        }),
      ] as unknown as PurchaseInvoiceDto["lines"],
    });

    const line = result.current.lines[0];
    expect(line.itemId).toBe("item-1");
    expect(line.packagingLevelId).toBe("pkg-caja");
    expect(line.conversionFactor).toBe(12);
    expect(line.quantityInBaseUom).toBe(24);
    expect(result.current.pendingProductSources).toHaveLength(0);
    await waitFor(() => expect(result.current.hasUnsavedChanges).toBe(true));
  });

  it("nunca sobrescribe una resolución ya guardada en la compra ni adopta sugerencias por revisar", async () => {
    const result = await setupLoaded({
      lines: [
        invoiceLine({
          id: "line-a",
          itemId: "item-1",
          xmlSource: xmlSource({ itemId: "item-otro", itemMatchStatus: "MANUALLY_MATCHED" }),
        }),
        invoiceLine({
          id: "line-b",
          purchaseReceptionLineId: "rl-2",
          xmlSource: xmlSource({
            purchaseReceptionLineId: "rl-2",
            itemId: "item-sugerido",
            itemMatchStatus: "NEEDS_REVIEW",
          }),
        }),
      ] as unknown as PurchaseInvoiceDto["lines"],
    });

    expect(result.current.lines[0].itemId).toBe("item-1");
    expect(result.current.lines[1].itemId ?? null).toBeNull();
    expect(result.current.hasUnsavedChanges).toBe(false);
  });
});

describe("PURCHASE-DRAFT-RECOVERY B2 — editar → guardar → reabrir → volver al XML", () => {
  it("la línea guardada editada conserva la evidencia original y al volver usa los montos del XML", async () => {
    const result = await setupLoaded({
      totalVat: 4.5,
      grandTotal: 34.5,
      lines: [
        invoiceLine({
          itemId: "item-1",
          quantity: 3,
          taxes: [
            {
              taxCode: "2",
              taxRateCode: "4",
              taxName: "IVA 15%",
              rate: 15,
              calculationType: "Percentage",
              taxableBase: 30,
              taxAmount: 4.5,
              source: "Calculated",
            },
          ],
        }),
      ] as unknown as PurchaseInvoiceDto["lines"],
    });

    const key = result.current.lines[0]._key;
    expect(result.current.lines[0].xmlQuantity).toBe(2);
    expect(result.current.lines[0].xmlTaxValue).toBe(3);

    act(() => {
      result.current.updateLine(key, "quantity", 2);
    });

    await waitFor(() => expect(result.current.summaryTotals.vat).toBe(3));
    expect(result.current.summaryTotals.total).toBe(23);
  });
});

describe("PURCHASE-DRAFT-RECOVERY F2 — cuotas manuales del borrador", () => {
  const manualSchedule = [
    { id: "s-1", installmentNumber: 1, dueDate: "2026-10-15", amount: 10, notes: "Anticipo" },
    { id: "s-2", installmentNumber: 2, dueDate: "2026-11-15", amount: 13, notes: null },
  ];

  it("sobreviven al reabrir: se muestran editables y se reenvían al guardar", async () => {
    const result = await setupLoaded({
      lines: [invoiceLine({ itemId: "item-1" })] as unknown as PurchaseInvoiceDto["lines"],
      paymentSchedules: manualSchedule,
    });

    expect(result.current.hasPersistedSchedule).toBe(false);
    expect(result.current.ptRows).toEqual([
      { number: 1, dueDate: "2026-10-15", amount: 10, notes: "Anticipo" },
      { number: 2, dueDate: "2026-11-15", amount: 13, notes: "" },
    ]);
    expect(result.current.hasUnsavedChanges).toBe(false);

    await act(async () => {
      await result.current.handleSave();
    });
    const payload = vi.mocked(purchaseService.update).mock.calls[0][1];
    expect(payload.schedule).toEqual([
      { installmentNumber: 1, dueDate: "2026-10-15", amount: 10, notes: "Anticipo" },
      { installmentNumber: 2, dueDate: "2026-11-15", amount: 13, notes: null },
    ]);
  });

  it("editar una cuota cuenta como cambio sin guardar", async () => {
    const result = await setupLoaded({
      lines: [invoiceLine({ itemId: "item-1" })] as unknown as PurchaseInvoiceDto["lines"],
      paymentSchedules: manualSchedule,
    });

    act(() => {
      result.current.updateScheduleRow(0, "notes", "Anticipo 50%");
    });

    await waitFor(() => expect(result.current.hasUnsavedChanges).toBe(true));
  });

  it("si el total cambia, Guardar conserva las cuotas tal cual y solo Confirmar las bloquea", async () => {
    const result = await setupLoaded({
      lines: [invoiceLine({ itemId: "item-1" })] as unknown as PurchaseInvoiceDto["lines"],
      paymentSchedules: manualSchedule,
    });
    const key = result.current.lines[0]._key;

    act(() => {
      result.current.updateLine(key, "quantity", 5);
    });
    await waitFor(() => expect(result.current.ptMismatch).toBe(true));
    expect(result.current.scheduleBlocksConfirm).toBe(true);

    await act(async () => {
      await result.current.handleSave();
    });

    expect(purchaseService.update).toHaveBeenCalledTimes(1);
    const payload = vi.mocked(purchaseService.update).mock.calls[0][1];
    expect(payload.schedule).toEqual([
      { installmentNumber: 1, dueDate: "2026-10-15", amount: 10, notes: "Anticipo" },
      { installmentNumber: 2, dueDate: "2026-11-15", amount: 13, notes: null },
    ]);
  });

  it("reabrir con cuotas guardadas desfasadas las conserva, advierte y bloquea Confirmar", async () => {
    const result = await setupLoaded({
      lines: [invoiceLine({ itemId: "item-1" })] as unknown as PurchaseInvoiceDto["lines"],
      paymentSchedules: [manualSchedule[0]],
    });

    expect(result.current.ptRows).toEqual([
      { number: 1, dueDate: "2026-10-15", amount: 10, notes: "Anticipo" },
    ]);
    expect(result.current.ptMismatch).toBe(true);
    expect(result.current.scheduleBlocksConfirm).toBe(true);
    expect(result.current.hasUnsavedChanges).toBe(false);

    await act(async () => {
      await result.current.handleConfirm();
    });

    expect(purchaseService.confirm).not.toHaveBeenCalled();
    expect(result.current.saveError).toBe(result.current.scheduleMismatchConfirmMessage);
  });

  it("con las cuotas corregidas Confirmar vuelve a enviarlas", async () => {
    vi.mocked(purchaseService.confirm).mockResolvedValue(buildInvoice({ status: "Confirmed" }));
    const result = await setupLoaded({
      lines: [invoiceLine({ itemId: "item-1" })] as unknown as PurchaseInvoiceDto["lines"],
      paymentSchedules: manualSchedule,
    });

    expect(result.current.scheduleBlocksConfirm).toBe(false);
    await act(async () => {
      await result.current.handleConfirm();
    });

    expect(purchaseService.confirm).toHaveBeenCalledTimes(1);
    expect(vi.mocked(purchaseService.confirm).mock.calls[0][1]).toHaveLength(2);
  });

  it("sin cuotas personalizadas, guardar envía [] (se generan desde la condición de pago)", async () => {
    const result = await setupLoaded({
      lines: [invoiceLine({ itemId: "item-1" })] as unknown as PurchaseInvoiceDto["lines"],
    });

    await act(async () => {
      await result.current.handleSave();
    });
    const payload = vi.mocked(purchaseService.update).mock.calls[0][1];
    expect(payload.schedule).toEqual([]);
  });
});

describe("PURCHASE-DRAFT-RECOVERY F3 — intención de retención", () => {
  it("sobrevive al reabrir y se reenvía al guardar (sin líneas ni montos)", async () => {
    vi.mocked(purchaseService.retentionPreview).mockResolvedValue({
      lines: [
        {
          taxType: "RENTA",
          retentionCode: "312",
          retentionCodeName: "Bienes",
          taxableBase: 20,
          retentionPct: 1.75,
          amountRetained: 0.35,
        },
      ],
      totalRetainedVat: 0,
      totalRetainedIncome: 0.35,
      totalRetainedIsd: 0,
      totalRetained: 0.35,
      skipReason: null,
    } as unknown as Awaited<ReturnType<typeof purchaseService.retentionPreview>>);
    const result = await setupLoaded({
      lines: [invoiceLine({ itemId: "item-1" })] as unknown as PurchaseInvoiceDto["lines"],
      retentionIntent: { appliesRetention: true, emissionPointId: "ep-1", issueDate: "2026-10-05" },
    });

    await waitFor(() => expect(result.current.whPreview).not.toBeNull());
    expect(result.current.retentionIntent).toEqual({
      appliesRetention: true,
      emissionPointId: "ep-1",
      issueDate: "2026-10-05",
    });
    expect(result.current.hasUnsavedChanges).toBe(false);

    await act(async () => {
      await result.current.handleSave();
    });
    const payload = vi.mocked(purchaseService.update).mock.calls[0][1];
    expect(payload.retentionIntent).toEqual({
      appliesRetention: true,
      emissionPointId: "ep-1",
      issueDate: "2026-10-05",
    });
  });

  it("cambiar la intención cuenta como cambio sin guardar", async () => {
    const result = await setupLoaded({
      lines: [invoiceLine({ itemId: "item-1" })] as unknown as PurchaseInvoiceDto["lines"],
    });

    act(() => {
      result.current.updateRetentionIntent({ issueDate: "2026-12-31" });
    });

    await waitFor(() => expect(result.current.hasUnsavedChanges).toBe(true));
  });
});

describe("PURCHASE-DRAFT-RECOVERY D3 — salir con cambios sin guardar", () => {
  it("pide confirmación antes de descartar y solo sale si el usuario lo confirma", async () => {
    const result = await setupLoaded({
      lines: [invoiceLine({ itemId: "item-1" })] as unknown as PurchaseInvoiceDto["lines"],
    });
    const key = result.current.lines[0]._key;
    act(() => {
      result.current.updateLine(key, "unitPrice", 11);
    });
    await waitFor(() => expect(result.current.hasUnsavedWork).toBe(true));

    const leave = vi.fn();
    act(() => {
      result.current.requestLeave(leave);
    });
    expect(leave).not.toHaveBeenCalled();
    expect(result.current.pendingLeave).toBe(true);

    act(() => {
      result.current.confirmLeave();
    });
    expect(leave).toHaveBeenCalledTimes(1);
    expect(result.current.pendingLeave).toBe(false);
  });

  it("sin cambios sale directamente", async () => {
    const result = await setupLoaded({
      lines: [invoiceLine({ itemId: "item-1" })] as unknown as PurchaseInvoiceDto["lines"],
    });
    const leave = vi.fn();

    act(() => {
      result.current.requestLeave(leave);
    });

    expect(leave).toHaveBeenCalledTimes(1);
  });
});
