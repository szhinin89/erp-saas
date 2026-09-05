// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import React from "react";
import { I18nProvider } from "../../../i18n/i18n";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { usePurchasesPage } from "./usePurchasesPage";
import { purchaseService, type PurchaseInvoiceDto } from "../api/purchaseService";
import {
  retentionsService,
  type RetentionDocumentDto,
} from "../../retentions/api/retentionsService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";

/**
 * PURCHASES-RETENTIONS-UI-MIGRATION-05C — reemplaza usePurchasesPage.withholding.test.ts: la
 * emisión de retención desde Compras ya no usa `purchaseService.issueWithholding`/`getWithholding`
 * (flujo legacy `IssuedWithholding`), sino el modelo transversal `RetentionDocument` vía
 * `retentionsService` (`POST/GET /api/v1/purchases/{id}/retention`, que internamente reutilizan
 * `IssueRetentionCommand`/`GetRetentionBySourceQuery`). Cubre: carga de la retención asociada,
 * payload exacto del nuevo endpoint, ausencia de llamadas legacy, y el manejo de los dos tipos de
 * conflicto (RetentionDocument ya emitido vs. IssuedWithholding legacy activo).
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

vi.mock("../../retentions/api/retentionsService", () => ({
  retentionsService: {
    getForPurchase: vi.fn(),
    issueForPurchase: vi.fn(),
    cancelForPurchase: vi.fn(),
    getElectronicXmlBlob: vi.fn(),
    getRidePdfBlob: vi.fn(),
    registerElectronic: vi.fn(),
  },
}));

vi.mock("../../ride/utils/downloadBlob", () => ({
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

vi.mock("../../items/hooks/useItemTypeOptions", () => ({
  useItemTypeOptions: () => ({ data: [], loading: false, error: null }),
}));

vi.mock("../../masterData/api/businessPartnerFacade", () => ({
  businessPartnerFacade: {
    getBusinessPartner: vi.fn().mockRejectedValue(new Error("not needed")),
  },
}));

vi.mock("../../inventory/facades/warehouseLookupFacade", () => ({
  warehouseLookupFacade: {
    list: vi.fn().mockResolvedValue([]),
  },
}));

vi.mock("../../masterData/api/paymentTermService", () => ({
  paymentTermService: {
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
  vi.mocked(retentionsService.getForPurchase).mockResolvedValue(null);
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: () => true,
    has: () => true,
    isAdminRole: false,
  } as unknown as ReturnType<typeof usePermissionsUi>);
});

async function setupWithLoadedInvoice() {
  vi.mocked(purchaseService.getById).mockResolvedValue(buildInvoice());
  const { result } = renderHook(() => usePurchasesPage(), { wrapper });

  await act(async () => {
    await result.current.loadForEdit("purchase-1");
  });

  await waitFor(() => expect(result.current.editing?.id).toBe("purchase-1"));
  return result;
}

describe("usePurchasesPage — carga de la retención asociada (RetentionDocument)", () => {
  it("carga la retención existente vía retentionsService.getForPurchase, no purchaseService.getWithholding", async () => {
    vi.mocked(retentionsService.getForPurchase).mockResolvedValue(buildRetention());
    const result = await setupWithLoadedInvoice();

    await waitFor(() => expect(result.current.retention?.id).toBe("ret-1"));
    expect(retentionsService.getForPurchase).toHaveBeenCalledWith("purchase-1");
  });

  it("sin retención emitida, retention queda null (estado normal, no error)", async () => {
    const result = await setupWithLoadedInvoice();

    expect(result.current.retention).toBeNull();
  });
});

describe("usePurchasesPage — emitir retención vía el modelo transversal RetentionDocument", () => {
  async function setupWithPreview() {
    vi.mocked(purchaseService.retentionPreview).mockResolvedValue({
      lines: [
        {
          taxType: "IVA",
          retentionCode: "725",
          retentionCodeName: "Retención IVA 30%",
          taxableBase: 100,
          retentionPct: 30,
          amountRetained: 30,
        },
      ],
      totalRetainedVat: 30,
      totalRetainedIncome: 0,
      totalRetainedIsd: 0,
      totalRetained: 30,
      skipReason: null,
    });
    const result = await setupWithLoadedInvoice();
    await act(async () => {
      await result.current.handleCalcRetention();
    });
    await waitFor(() => expect(result.current.whPreview?.lines.length).toBe(1));
    return result;
  }

  it("llama al endpoint nuevo (retentionsService.issueForPurchase), nunca purchaseService.issueWithholding", async () => {
    vi.mocked(retentionsService.issueForPurchase).mockResolvedValue(buildRetention());
    const result = await setupWithPreview();

    await act(async () => {
      await result.current.handleIssueRetention("ep-1");
    });

    expect(retentionsService.issueForPurchase).toHaveBeenCalledTimes(1);
    expect(purchaseService as unknown as Record<string, unknown>).not.toHaveProperty(
      "issueWithholding",
    );
  });

  it("el payload usa emissionPointId/issueDate/lines con taxType Vat/Income — nunca retentionNumber", async () => {
    vi.mocked(retentionsService.issueForPurchase).mockResolvedValue(buildRetention());
    const result = await setupWithPreview();

    await act(async () => {
      await result.current.handleIssueRetention("ep-1");
    });

    expect(retentionsService.issueForPurchase).toHaveBeenCalledWith(
      "purchase-1",
      expect.objectContaining({
        emissionPointId: "ep-1",
        issueDate: expect.any(String),
        lines: [
          expect.objectContaining({
            taxType: "Vat",
            retentionCode: "725",
            baseAmount: 100,
            retentionRate: 30,
            retainedAmount: 30,
          }),
        ],
      }),
    );
    const payload = vi.mocked(retentionsService.issueForPurchase).mock.calls[0][1];
    expect(payload).not.toHaveProperty("retentionNumber");
    expect(payload.lines[0]).not.toHaveProperty("retentionNumber");
  });

  it("al emitir correctamente, actualiza retention y muestra message.success", async () => {
    vi.mocked(retentionsService.issueForPurchase).mockResolvedValue(buildRetention());
    const result = await setupWithPreview();

    await act(async () => {
      await result.current.handleIssueRetention("ep-1");
    });

    expect(result.current.retention?.id).toBe("ret-1");
    expect(message.success).toHaveBeenCalledWith("Retención emitida correctamente.");
  });

  it("409 por RetentionDocument ya emitido muestra el mensaje claro esperado", async () => {
    vi.mocked(retentionsService.issueForPurchase).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "Ya existe una retención activa para este documento origen." } },
      },
    });
    const result = await setupWithPreview();

    await act(async () => {
      await result.current.handleIssueRetention("ep-1");
    });

    expect(result.current.saveError).toBe("Esta compra ya tiene una retención emitida.");
    expect(message.success).not.toHaveBeenCalled();
  });

  it("409 por IssuedWithholding legacy activo muestra el mensaje claro de bloqueo legacy", async () => {
    vi.mocked(retentionsService.issueForPurchase).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: {
          message: {
            user: "Esta compra ya tiene una retención emitida por el flujo anterior (IssuedWithholding).",
          },
        },
      },
    });
    const result = await setupWithPreview();

    await act(async () => {
      await result.current.handleIssueRetention("ep-1");
    });

    expect(result.current.saveError).toBe(
      "Esta compra ya tiene una retención legacy activa. No se puede emitir otra retención.",
    );
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
    vi.mocked(retentionsService.getForPurchase).mockResolvedValue(buildRetention());
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

    expect(retentionsService.registerElectronic).not.toHaveBeenCalled();
  });

  it("handleRegisterRetentionElectronic llama al backend cuando el permiso está concedido", async () => {
    vi.mocked(retentionsService.getForPurchase).mockResolvedValue(buildRetention());
    vi.mocked(retentionsService.registerElectronic).mockResolvedValue({
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

    expect(retentionsService.registerElectronic).toHaveBeenCalledWith("ret-1");
  });
});

describe("usePurchasesPage — anular retención (PURCHASES-RETENTIONS-CANCEL-05D)", () => {
  // La condición JSX del botón "Anular" en PurchasesPage.tsx es
  // `ctx.retention && ctx.retention.status === "Issued" && ctx.canUpdatePurchase` — se prueba aquí
  // a nivel de estado del hook (fuente de verdad de esa condición), no montando la página completa
  // (3200+ líneas, fuera de alcance — ver PurchasesPage.withholdingModal.test.tsx).
  it("retention.status refleja 'Cancelled' tras anular — la condición de mostrar el botón deja de cumplirse", async () => {
    vi.mocked(retentionsService.getForPurchase).mockResolvedValue(buildRetention({ status: "Issued" }));
    vi.mocked(retentionsService.cancelForPurchase).mockResolvedValue(
      buildRetention({ status: "Cancelled" }),
    );
    const result = await setupWithLoadedInvoice();
    await waitFor(() => expect(result.current.retention?.status).toBe("Issued"));

    await act(async () => {
      await result.current.handleCancelRetention("Motivo");
    });

    expect(result.current.retention?.status).toBe("Cancelled");
    expect(result.current.retention?.status === "Issued").toBe(false);
  });

  it("sin retención asociada, la condición de mostrar el botón nunca se cumple (retention es null)", async () => {
    const result = await setupWithLoadedInvoice();

    expect(result.current.retention).toBeNull();
  });

  it("expone canUpdatePurchase reflejando el permiso purchases.update", async () => {
    vi.mocked(usePermissionsUi).mockReturnValue({
      canShow: (key: string) => key === "purchases.update",
      has: () => true,
      isAdminRole: false,
    } as unknown as ReturnType<typeof usePermissionsUi>);
    const result = await setupWithLoadedInvoice();

    expect(result.current.canUpdatePurchase).toBe(true);
  });

  it("handleCancelRetention llama retentionsService.cancelForPurchase, nunca purchaseService.cancelWithholding", async () => {
    vi.mocked(retentionsService.getForPurchase).mockResolvedValue(buildRetention());
    vi.mocked(retentionsService.cancelForPurchase).mockResolvedValue(
      buildRetention({ status: "Cancelled", cancelReason: "Error en el cálculo" }),
    );
    const result = await setupWithLoadedInvoice();
    await waitFor(() => expect(result.current.retention?.id).toBe("ret-1"));

    await act(async () => {
      await result.current.handleCancelRetention("Error en el cálculo");
    });

    expect(retentionsService.cancelForPurchase).toHaveBeenCalledWith(
      "purchase-1",
      "ret-1",
      "Error en el cálculo",
    );
    expect(purchaseService as unknown as Record<string, unknown>).not.toHaveProperty(
      "cancelWithholding",
    );
  });

  it("al anular correctamente, refresca retention y muestra message.success", async () => {
    vi.mocked(retentionsService.getForPurchase).mockResolvedValue(buildRetention());
    vi.mocked(retentionsService.cancelForPurchase).mockResolvedValue(
      buildRetention({ status: "Cancelled", cancelReason: "Motivo" }),
    );
    const result = await setupWithLoadedInvoice();
    await waitFor(() => expect(result.current.retention?.id).toBe("ret-1"));

    await act(async () => {
      await result.current.handleCancelRetention("Motivo");
    });

    expect(result.current.retention?.status).toBe("Cancelled");
    expect(message.success).toHaveBeenCalledWith("Retención anulada correctamente.");
  });

  it("no llama al backend si no hay retención cargada", async () => {
    const result = await setupWithLoadedInvoice();

    await act(async () => {
      await result.current.handleCancelRetention("Motivo");
    });

    expect(retentionsService.cancelForPurchase).not.toHaveBeenCalled();
  });

  it("si el backend falla, muestra el error real y no llama message.success", async () => {
    vi.mocked(retentionsService.getForPurchase).mockResolvedValue(buildRetention());
    vi.mocked(retentionsService.cancelForPurchase).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 422,
        data: { message: { user: "No se puede anular la retención: la cuenta por pagar ya tiene pagos aplicados." } },
      },
    });
    const result = await setupWithLoadedInvoice();
    await waitFor(() => expect(result.current.retention?.id).toBe("ret-1"));

    await act(async () => {
      await result.current.handleCancelRetention("Motivo");
    });

    expect(result.current.saveError).toBe(
      "No se puede anular la retención: la cuenta por pagar ya tiene pagos aplicados.",
    );
    expect(message.success).not.toHaveBeenCalled();
  });
});
