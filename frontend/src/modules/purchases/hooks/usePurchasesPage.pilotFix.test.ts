// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import React from "react";
import { AxiosError, AxiosHeaders } from "axios";
import { I18nProvider } from "../../../i18n/i18n";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { usePurchasesPage } from "./usePurchasesPage";
import { purchaseService, type PurchaseInvoiceDto } from "../api/purchaseService";
import { businessPartnerLookupFacade } from "../../masterData/facades/businessPartnerLookupFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";

/**
 * PILOT-PURCHASE-UI-FIX-01 — flujo de confirmación/anulación de la compra:
 * 1. el motivo real del backend (`data.errors`) se muestra antes que el genérico del catálogo;
 * 2. Confirmar se bloquea con cambios locales sin guardar (nunca guarda+confirma);
 * 3. `saving` siempre se libera, incluido el rechazo por proveedor inactivo.
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
  message: { success: vi.fn(), error: vi.fn(), confirm: vi.fn() },
}));

const GENERIC_CATALOG_MESSAGE = "Datos inválidos. Revisa el formulario.";

function apiError(data: unknown, status = 422) {
  return new AxiosError("Request failed with status code " + status, "ERR_BAD_REQUEST", undefined, undefined, {
    status,
    statusText: "Unprocessable Entity",
    headers: {},
    config: { headers: new AxiosHeaders() },
    data,
  });
}

function validationError(...errors: string[]) {
  return apiError({
    code: "VALIDATION_ERROR",
    message: { user: GENERIC_CATALOG_MESSAGE },
    data: { errors },
  });
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
    lines: [
      {
        id: "line-1",
        itemId: "item-1",
        description: "ARROZ 1KG",
        quantity: 10,
        unitPrice: 2,
        vatCode: "4",
        discountPct: 0,
        iceCode: null,
        warehouseId: null,
        notes: null,
      },
    ] as unknown as PurchaseInvoiceDto["lines"],
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
  vi.mocked(purchaseService.retentionPreview).mockResolvedValue({
    lines: [],
    totalRetainedVat: 0,
    totalRetainedIncome: 0,
    totalRetainedIsd: 0,
    totalRetained: 0,
    skipReason: "No aplica",
  });
  // El contexto del ítem llega async y muta la línea (campo runtime): no debe contar como cambio.
  vi.mocked(purchaseService.getItemContext).mockResolvedValue({
    itemId: "item-1",
    sku: "A1",
    shortName: "ARROZ",
    description: "ARROZ 1KG",
    baseUomCode: "UNIT",
    participatesInInventory: true,
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
  const { result } = renderHook(() => usePurchasesPage(), { wrapper });
  await act(async () => {
    await result.current.loadForEdit("purchase-1");
  });
  await waitFor(() => expect(result.current.editing?.id).toBe("purchase-1"));
  return result;
}

describe("PILOT-PURCHASE-UI-FIX-01 — motivo real del backend", () => {
  it("confirmar: muestra el motivo de data.errors, no el mensaje genérico del catálogo", async () => {
    const reason =
      "La condición de pago de esta compra fue desactivada. Edite el borrador y seleccione una condición de pago activa antes de confirmar.";
    vi.mocked(purchaseService.confirm).mockRejectedValue(validationError(reason));
    const result = await setupLoaded();

    await act(async () => {
      await result.current.handleConfirm();
    });

    expect(result.current.saveError).toBe(reason);
    expect(result.current.saveError).not.toBe(GENERIC_CATALOG_MESSAGE);
    expect(result.current.saving).toBe(false);
    expect(message.success).not.toHaveBeenCalled();
  });

  it("confirmar: sin detalle del backend usa el fallback genérico", async () => {
    vi.mocked(purchaseService.confirm).mockRejectedValue(apiError(null, 500));
    const result = await setupLoaded();

    await act(async () => {
      await result.current.handleConfirm();
    });

    expect(result.current.saveError).toBe("Error al confirmar.");
    expect(result.current.saving).toBe(false);
  });

  it("anular: muestra el motivo de data.errors, no el mensaje genérico del catálogo", async () => {
    const reason = "No se puede anular: el período contable está cerrado.";
    vi.mocked(purchaseService.cancel).mockRejectedValue(validationError(reason));
    const result = await setupLoaded({ status: "Confirmed" });

    await act(async () => {
      await result.current.handleCancel("Error de digitación");
    });

    expect(result.current.saveError).toBe(reason);
    expect(result.current.saving).toBe(false);
    expect(message.success).not.toHaveBeenCalled();
  });
});

describe("PILOT-PURCHASE-UI-FIX-01 — Confirmar con cambios sin guardar", () => {
  it("sin cambios (incluido el contexto async del ítem): Confirmar habilitado y confirma", async () => {
    vi.mocked(purchaseService.confirm).mockResolvedValue(buildInvoice({ status: "Confirmed" }));
    const result = await setupLoaded();
    await waitFor(() => expect(result.current.lines[0]?.context?.itemId).toBe("item-1"));

    expect(result.current.hasUnsavedChanges).toBe(false);

    await act(async () => {
      await result.current.handleConfirm();
    });

    expect(purchaseService.confirm).toHaveBeenCalledTimes(1);
    expect(message.success).toHaveBeenCalledWith("Compra confirmada correctamente.");
  });

  it("con cambios locales: Confirmar bloqueado, mensaje claro y no llama al backend ni guarda", async () => {
    const result = await setupLoaded();
    const key = result.current.lines[0]._key;

    act(() => {
      result.current.updateLine(key, "unitPrice", 2.5);
    });

    await waitFor(() => expect(result.current.hasUnsavedChanges).toBe(true));
    expect(result.current.unsavedChangesMessage).toBe(
      "Guarda los cambios antes de confirmar la compra.",
    );

    await act(async () => {
      await result.current.handleConfirm();
    });

    expect(purchaseService.confirm).not.toHaveBeenCalled();
    expect(purchaseService.update).not.toHaveBeenCalled();
    expect(result.current.saveError).toBe("Guarda los cambios antes de confirmar la compra.");
  });

  it("volver al valor guardado deja de marcar cambios", async () => {
    const result = await setupLoaded();
    const key = result.current.lines[0]._key;

    act(() => {
      result.current.updateLine(key, "quantity", 12);
    });
    await waitFor(() => expect(result.current.hasUnsavedChanges).toBe(true));

    act(() => {
      result.current.updateLine(key, "quantity", 10);
    });
    await waitFor(() => expect(result.current.hasUnsavedChanges).toBe(false));
  });

  it("una compra nueva (sin borrador persistido) no marca cambios sin guardar", async () => {
    const { result } = renderHook(() => usePurchasesPage(), { wrapper });

    act(() => {
      result.current.addLine();
    });

    expect(result.current.hasUnsavedChanges).toBe(false);
  });
});

describe("PILOT-PURCHASE-UI-FIX-01 — saving siempre se libera", () => {
  it("proveedor inactivo rechazado por el backend al guardar: muestra el motivo y libera saving", async () => {
    vi.mocked(purchaseService.update).mockRejectedValue(
      validationError("El proveedor 'Proveedor Uno' se encuentra inactivo."),
    );
    const result = await setupLoaded();
    await waitFor(() => expect(result.current.canUseSriDocTypes).toBe(true));

    await act(async () => {
      await result.current.handleSave();
    });

    expect(purchaseService.update).toHaveBeenCalledTimes(1);
    expect(result.current.saveError).toBe("El proveedor 'Proveedor Uno' se encuentra inactivo.");
    expect(result.current.saving).toBe(false);
  });
});
