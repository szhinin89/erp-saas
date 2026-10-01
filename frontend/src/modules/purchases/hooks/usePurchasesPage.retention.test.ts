// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import React from "react";
import { I18nProvider } from "../../../i18n/i18n";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { usePurchasesPage } from "./usePurchasesPage";
import { purchaseService, type PurchaseInvoiceDto } from "../api/purchaseService";
import {
  purchaseRetentionFacade,
  type RetentionDocumentDto,
} from "../../retentions/facades/purchaseRetentionFacade";
import { emissionPointLookupFacade } from "../../emissionPoints/facades/emissionPointLookupFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";

/**
 * Retención de Compras en el hook de la página. ZH-PURCHASE-RETENTION-CONFIRM-01: la retención se
 * define en el BORRADOR (vista previa automática del backend, montos precargados, intención
 * activable) y viaja como `RetentionIntent` en `purchaseService.confirm` — no existe una emisión
 * posterior. Sobre la compra confirmada quedan: carga de la retención emitida, XML/RIDE/registro
 * SRI y anulación (`purchaseRetentionFacade`).
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
    getForPurchase: vi.fn(),
    getElectronicXmlBlob: vi.fn(),
    getRidePdfBlob: vi.fn(),
    registerElectronic: vi.fn(),
  },
}));

vi.mock("../../emissionPoints/facades/emissionPointLookupFacade", () => ({
  emissionPointLookupFacade: {
    list: vi.fn(),
  },
}));

vi.mock("../../../lib/download", () => ({
  downloadBlob: vi.fn(),
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

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
  businessPartnerLookupFacade: {
    getBusinessPartner: vi.fn().mockRejectedValue(new Error("not needed")),
  },
}));

vi.mock("../../inventory/facades/warehouseLookupFacade", () => ({
  warehouseLookupFacade: {
    list: vi.fn().mockResolvedValue([]),
  },
}));

vi.mock("../../masterData/facades/paymentTermLookupFacade", () => ({
  paymentTermLookupFacade: {
    list: vi.fn().mockResolvedValue([]),
  },
}));

vi.mock("../../items/facades/sriLookupFacade", () => ({
  sriLookupFacade: {
    docTypes: vi.fn().mockResolvedValue([]),
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
    confirm: vi.fn(),
  },
}));

function buildInvoice(overrides: Partial<PurchaseInvoiceDto> = {}): PurchaseInvoiceDto {
  return {
    id: "purchase-1",
    supplierId: "supplier-1",
    supplierName: "Proveedor Uno",
    supplierTaxId: "0999999999001",
    docTypeCode: "01",
    invoiceNumber: "001-001-000000123",
    issueDate: "2026-08-01",
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
    globalWarehouseId: null,
    paymentTermId: "term-1",
    paymentTermName: "Contado",
    paymentTermInstallments: 1,
    paymentTermDaysBetween: 0,
    creditTermDays: 0,
    dueDate: null,
    notes: null,
    status: "Confirmed",
    cancelReason: null,
    cancelledAt: null,
    cancelledBy: null,
    subtotal: 100,
    totalDiscount: 0,
    totalIce: 0,
    totalVat: 15,
    totalFreight: 0,
    totalOtherCosts: 0,
    grandTotal: 115,
    totalIrbpnr: 0,
    lines: [],
    paymentSchedules: [],
    createdAt: "2026-08-01T10:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

function buildRetention(overrides: Partial<RetentionDocumentDto> = {}): RetentionDocumentDto {
  return {
    id: "ret-1",
    companyId: "company-1",
    branchId: "branch-1",
    sourceDocumentType: "PurchaseInvoice",
    sourceDocumentId: "purchase-1",
    subjectBusinessPartnerId: "supplier-1",
    emissionPointId: "ep-1",
    retentionNumber: "001-001-000000045",
    issueDate: "2026-08-15",
    status: "Issued",
    totalRetainedVat: 3,
    totalRetainedIncome: 1,
    totalRetained: 4,
    cancelReason: null,
    cancelledAt: null,
    cancelledBy: null,
    lines: [],
    fiscalPeriod: "08/2026",
    sourceDocumentSriTypeCode: "01",
    sourceDocumentNumber: "001-001-000000123",
    sourceDocumentIssueDate: "2026-08-01",
    sourceDocumentAuthorizationNumber: null,
    sourceDocumentTaxSupportCode: null,
    sourceDocumentSubtotal: 100,
    sourceDocumentTotal: 115,
    ...overrides,
  };
}

const ELIGIBLE_PREVIEW = {
  lines: [
    {
      taxType: "IVA",
      retentionCode: "725",
      retentionCodeName: "Retención IVA 30%",
      taxableBase: 15,
      retentionPct: 30,
      amountRetained: 4.5,
    },
  ],
  totalRetainedVat: 4.5,
  totalRetainedIncome: 0,
  totalRetainedIsd: 0,
  totalRetained: 4.5,
  skipReason: null,
};

function wrapper({ children }: { children: React.ReactNode }) {
  return React.createElement(I18nProvider, null, children);
}

beforeEach(() => {
  vi.clearAllMocks();
  useActiveBranchStore.setState({
    branch: { id: "branch-1", name: "Matriz", isMainBranch: true },
  });
  vi.mocked(purchaseService.list).mockResolvedValue({
    items: [],
    total: 0,
    page: 1,
    pageSize: 25,
  });
  vi.mocked(purchaseRetentionFacade.getForPurchase).mockResolvedValue(null);
  vi.mocked(purchaseService.retentionPreview).mockResolvedValue(ELIGIBLE_PREVIEW);
  vi.mocked(emissionPointLookupFacade.list).mockResolvedValue([
    {
      id: "ep-2",
      establishmentId: "est-1",
      establishmentCode: "001",
      establishmentName: "Matriz",
      branchName: null,
      code: "002",
      name: "Caja 2",
      emissionType: "Electronic",
      isDefault: false,
      isActive: true,
      createdAt: "2026-01-01T00:00:00Z",
    },
    {
      id: "ep-1",
      establishmentId: "est-1",
      establishmentCode: "001",
      establishmentName: "Matriz",
      branchName: null,
      code: "001",
      name: "Principal",
      emissionType: "Electronic",
      isDefault: true,
      isActive: true,
      createdAt: "2026-01-01T00:00:00Z",
    },
  ] as Awaited<ReturnType<typeof emissionPointLookupFacade.list>>);
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
});

async function setupWithLoadedInvoice(overrides: Partial<PurchaseInvoiceDto> = {}) {
  vi.mocked(purchaseService.getById).mockResolvedValue(buildInvoice(overrides));
  const { result } = renderHook(() => usePurchasesPage(), { wrapper });

  await act(async () => {
    await result.current.loadForEdit("purchase-1");
  });

  await waitFor(() => expect(result.current.editing?.id).toBe("purchase-1"));
  return result;
}

describe("usePurchasesPage — carga de la retención asociada (RetentionDocument)", () => {
  it("carga la retención existente vía purchaseRetentionFacade.getForPurchase, no purchaseService.getWithholding", async () => {
    vi.mocked(purchaseRetentionFacade.getForPurchase).mockResolvedValue(buildRetention());
    const result = await setupWithLoadedInvoice();

    await waitFor(() => expect(result.current.retention?.id).toBe("ret-1"));
    expect(purchaseRetentionFacade.getForPurchase).toHaveBeenCalledWith("purchase-1");
  });

  it("sin retención emitida, retention queda null (estado normal, no error)", async () => {
    const result = await setupWithLoadedInvoice();

    expect(result.current.retention).toBeNull();
  });
});

describe("usePurchasesPage — retención definida en el borrador y emitida al confirmar (ZH-PURCHASE-RETENTION-CONFIRM-01)", () => {
  async function setupDraft() {
    const result = await setupWithLoadedInvoice({ status: "Draft" });
    await waitFor(() => expect(result.current.whPreview?.lines.length).toBe(1));
    return result;
  }

  it("en borrador carga la vista previa automáticamente (sin acción 'Calcular') y permite activar la retención", async () => {
    const result = await setupDraft();

    expect(purchaseService.retentionPreview).toHaveBeenCalledWith("purchase-1");
    expect(result.current.canApplyRetention).toBe(true);
    expect(result.current.retentionIntent.appliesRetention).toBe(false);
    expect(result.current.whLoading).toBe(false);
  });

  it("una compra confirmada no pide vista previa (la retención ya se definió al confirmar)", async () => {
    await setupWithLoadedInvoice({ status: "Confirmed" });

    expect(purchaseService.retentionPreview).not.toHaveBeenCalled();
  });

  it("muestra loading mientras se calcula la vista previa", async () => {
    let resolve!: (v: typeof ELIGIBLE_PREVIEW) => void;
    vi.mocked(purchaseService.retentionPreview).mockReturnValue(
      new Promise((r) => {
        resolve = r;
      }),
    );
    const result = await setupWithLoadedInvoice({ status: "Draft" });

    await waitFor(() => expect(result.current.whLoading).toBe(true));
    await act(async () => {
      resolve(ELIGIBLE_PREVIEW);
    });
    await waitFor(() => expect(result.current.whLoading).toBe(false));
    expect(result.current.whPreview?.totalRetained).toBe(4.5);
  });

  it("si la vista previa falla expone el error (sin bloquear la página) y no permite activar la retención", async () => {
    vi.mocked(purchaseService.retentionPreview).mockRejectedValue({
      isAxiosError: true,
      response: { status: 500, data: { message: { user: "Fallo de elegibilidad" } } },
    });
    const result = await setupWithLoadedInvoice({ status: "Draft" });

    await waitFor(() => expect(result.current.whPreviewError).toBe("Fallo de elegibilidad"));
    expect(result.current.canApplyRetention).toBe(false);
  });

  it("sin líneas propuestas (empresa no agente, proveedor exento...) la intención no puede quedar activa", async () => {
    vi.mocked(purchaseService.retentionPreview).mockResolvedValue({
      lines: [],
      totalRetainedVat: 0,
      totalRetainedIncome: 0,
      totalRetainedIsd: 0,
      totalRetained: 0,
      skipReason: "La empresa no está configurada como agente de retención de IVA.",
    });
    const result = await setupWithLoadedInvoice({ status: "Draft" });
    await waitFor(() => expect(result.current.whPreview?.skipReason).toContain("agente de retención"));

    act(() => {
      result.current.updateRetentionIntent({ appliesRetention: true });
    });

    await waitFor(() => expect(result.current.retentionIntent.appliesRetention).toBe(false));
    expect(result.current.canApplyRetention).toBe(false);
  });

  it("confirmar sin intención envía la confirmación de siempre (sin retención)", async () => {
    vi.mocked(purchaseService.confirm).mockResolvedValue(buildInvoice());
    const result = await setupDraft();

    await act(async () => {
      await result.current.handleConfirm();
    });

    // El 2.º argumento es el cronograma de cuotas del borrador (sin relación con la retención).
    expect(purchaseService.confirm).toHaveBeenCalledWith("purchase-1", expect.anything(), undefined);
    expect(message.success).toHaveBeenCalledWith("Compra confirmada correctamente.");
  });

  it("al activar la intención precarga las líneas de la vista previa y preselecciona el punto de emisión por defecto", async () => {
    vi.mocked(purchaseService.confirm).mockResolvedValue(buildInvoice());
    const result = await setupDraft();

    act(() => {
      result.current.updateRetentionIntent({ appliesRetention: true });
    });
    await waitFor(() => expect(result.current.retentionIntent.emissionPointId).toBe("ep-1"));

    await act(async () => {
      await result.current.handleConfirm();
    });

    expect(purchaseService.confirm).toHaveBeenCalledTimes(1);
    const [id, , intent] = vi.mocked(purchaseService.confirm).mock.calls[0];
    expect(id).toBe("purchase-1");
    expect(intent).toEqual({
      appliesRetention: true,
      emissionPointId: "ep-1",
      issueDate: expect.any(String),
      lines: [
        {
          taxType: "Vat",
          retentionCode: "725",
          baseAmount: 15,
          retentionRate: 30,
          retainedAmount: 4.5,
          retentionCodeDescription: "Retención IVA 30%",
        },
      ],
    });
    expect(intent).not.toHaveProperty("retentionNumber");
    expect(message.success).toHaveBeenCalledWith("Compra confirmada y retención emitida correctamente.");
  });

  it("desactivar la intención vuelve a confirmar sin retención", async () => {
    vi.mocked(purchaseService.confirm).mockResolvedValue(buildInvoice());
    const result = await setupDraft();
    act(() => {
      result.current.updateRetentionIntent({ appliesRetention: true });
    });
    await waitFor(() => expect(result.current.retentionIntent.emissionPointId).toBe("ep-1"));
    act(() => {
      result.current.updateRetentionIntent({ appliesRetention: false });
    });

    await act(async () => {
      await result.current.handleConfirm();
    });

    expect(purchaseService.confirm).toHaveBeenCalledWith("purchase-1", expect.anything(), undefined);
  });

  it("intención incompleta (sin punto de emisión) no confirma a medias: muestra el error y no llama al backend", async () => {
    vi.mocked(emissionPointLookupFacade.list).mockResolvedValue([]);
    const result = await setupDraft();
    act(() => {
      result.current.updateRetentionIntent({ appliesRetention: true });
    });
    await waitFor(() => expect(emissionPointLookupFacade.list).toHaveBeenCalled());

    await act(async () => {
      await result.current.handleConfirm();
    });

    expect(purchaseService.confirm).not.toHaveBeenCalled();
    expect(result.current.saveError).toBe(
      "Complete la retención (punto de emisión y fecha) o desactívela antes de confirmar.",
    );
  });

  it("si la confirmación con retención falla, muestra el error del backend y no informa éxito", async () => {
    vi.mocked(purchaseService.confirm).mockRejectedValue({
      response: { data: { message: { user: "La empresa no está configurada como agente de retención de IVA." } } },
    });
    const result = await setupDraft();
    act(() => {
      result.current.updateRetentionIntent({ appliesRetention: true });
    });
    await waitFor(() => expect(result.current.retentionIntent.emissionPointId).toBe("ep-1"));

    await act(async () => {
      await result.current.handleConfirm();
    });

    expect(result.current.saveError).toBe("La empresa no está configurada como agente de retención de IVA.");
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no queda un segundo flujo de emisión posterior: ni acciones en el hook ni en el facade", async () => {
    const result = await setupDraft();

    expect(result.current).not.toHaveProperty("handleIssueRetention");
    expect(result.current).not.toHaveProperty("handleCalcRetention");
    expect(result.current).not.toHaveProperty("modalWhIssue");
    expect(purchaseRetentionFacade).not.toHaveProperty("issueForPurchase");
  });
});

describe("usePurchasesPage — documento electrónico de la retención (XML/RIDE/registro)", () => {
  it("expone el registro electrónico solo si el permiso electronic-documents.retry está concedido", async () => {
    vi.mocked(usePermissionsUi).mockReturnValue({
      canShow: (key: string) => key !== "electronic-documents.retry",
      has: () => true,
      isAdminRole: false,
    } as unknown as ReturnType<typeof usePermissionsUi>);
    const result = await setupWithLoadedInvoice();

    expect(result.current.canRegisterElectronic).toBe(false);
  });

  it("handleRegisterRetentionElectronic no llama al backend si falta el permiso", async () => {
    vi.mocked(purchaseRetentionFacade.getForPurchase).mockResolvedValue(buildRetention());
    vi.mocked(usePermissionsUi).mockReturnValue({
      canShow: () => false,
      has: () => true,
      isAdminRole: false,
    } as unknown as ReturnType<typeof usePermissionsUi>);
    const result = await setupWithLoadedInvoice();
    await waitFor(() => expect(result.current.retention?.id).toBe("ret-1"));

    await act(async () => {
      await result.current.handleRegisterRetentionElectronic();
    });

    expect(purchaseRetentionFacade.registerElectronic).not.toHaveBeenCalled();
  });

  it("handleRegisterRetentionElectronic llama al backend cuando el permiso está concedido", async () => {
    vi.mocked(purchaseRetentionFacade.getForPurchase).mockResolvedValue(buildRetention());
    vi.mocked(purchaseRetentionFacade.registerElectronic).mockResolvedValue({
      id: "ed-1",
      documentType: "07",
      sourceModule: "Retentions",
      sourceEntityId: "ret-1",
      currentState: "Authorized",
      accessKey: null,
      authorizationNumber: null,
      authorizationDate: null,
      retryCount: 0,
      lastAttemptUtc: null,
      createdAt: "2026-08-15T10:00:00Z",
      updatedAt: null,
    });
    const result = await setupWithLoadedInvoice();
    await waitFor(() => expect(result.current.retention?.id).toBe("ret-1"));

    await act(async () => {
      await result.current.handleRegisterRetentionElectronic();
    });

    expect(purchaseRetentionFacade.registerElectronic).toHaveBeenCalledWith("ret-1");
  });
});

describe("usePurchasesPage — sin anulación aislada de la retención (ZH-RETENTION-CANCELLATION-LIFECYCLE-01)", () => {
  it("el hook y el facade no exponen una anulación de la retención separada de la compra", async () => {
    vi.mocked(purchaseRetentionFacade.getForPurchase).mockResolvedValue(buildRetention());
    const result = await setupWithLoadedInvoice();
    await waitFor(() => expect(result.current.retention?.id).toBe("ret-1"));

    expect(result.current).not.toHaveProperty("handleCancelRetention");
    expect(result.current).not.toHaveProperty("modalRetentionCancel");
    expect(purchaseRetentionFacade).not.toHaveProperty("cancelForPurchase");
  });

  it("anular la compra es la única vía: llama purchaseService.cancel y la retención se resuelve en el backend", async () => {
    vi.mocked(purchaseRetentionFacade.getForPurchase).mockResolvedValue(buildRetention());
    vi.mocked(purchaseService.cancel).mockResolvedValue(buildInvoice({ status: "Cancelled" }));
    const result = await setupWithLoadedInvoice();
    await waitFor(() => expect(result.current.retention?.status).toBe("Issued"));

    await act(async () => {
      await result.current.handleCancel("Error de digitación");
    });

    expect(purchaseService.cancel).toHaveBeenCalledWith("purchase-1", "Error de digitación");
    expect(message.success).toHaveBeenCalledWith("Compra anulada correctamente.");
  });
});
