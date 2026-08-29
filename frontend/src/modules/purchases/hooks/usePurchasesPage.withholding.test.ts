// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import React from "react";
import { I18nProvider } from "../../../i18n/i18n";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { usePurchasesPage } from "./usePurchasesPage";
import {
  purchaseService,
  type PurchaseInvoiceDto,
  type IssuedWithholdingDto,
} from "../api/purchaseService";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-PURCHASES-EXPENSES-03 — cubre exclusivamente el flujo de emisión de
 * retención (handleIssueWithholding): éxito muestra message.success, fallo usa
 * formatApiRequestError con el mensaje real del backend. No ejercita el resto de la superficie
 * de usePurchasesPage (fuera de alcance de este ticket).
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
    getWithholding: vi.fn(),
    issueWithholding: vi.fn(),
    getWithholdingById: vi.fn(),
    cancelWithholding: vi.fn(),
    getItemContext: vi.fn(),
    supplierReport: vi.fn(),
    getTaxSummaries: vi.fn(),
  },
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
    status: "Draft",
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
});

describe("usePurchasesPage — emitir retención: feedback (CRITICAL-CONFIRMATIONS-PURCHASES-EXPENSES-03)", () => {
  async function setupWithLoadedInvoice() {
    vi.mocked(purchaseService.getById).mockResolvedValue(buildInvoice());
    const { result } = renderHook(() => usePurchasesPage(), { wrapper });

    await act(async () => {
      await result.current.loadForEdit("purchase-1");
    });

    await waitFor(() => expect(result.current.editing?.id).toBe("purchase-1"));
    return result;
  }

  it("al emitir exitosamente llama al endpoint existente y muestra message.success", async () => {
    vi.mocked(purchaseService.issueWithholding).mockResolvedValue({
      id: "wh-1",
      purchaseInvoiceId: "purchase-1",
      supplierId: "supplier-1",
      emissionPointId: "ep-1",
      withholdingNumber: "001-001-000000045",
      issueDate: "2026-08-15",
      accessKey: null,
      totalRetainedVat: 3,
      totalRetainedIncome: 1,
      totalRetainedIsd: 0,
      totalRetained: 4,
      status: "Issued",
      details: [],
      createdAt: "2026-08-15T10:00:00Z",
      updatedAt: null,
    });
    const result = await setupWithLoadedInvoice();

    await act(async () => {
      await result.current.handleIssueWithholding("ep-1");
    });

    expect(purchaseService.issueWithholding).toHaveBeenCalledWith(
      "purchase-1",
      "ep-1",
      expect.any(String),
    );
    expect(message.success).toHaveBeenCalledWith("Retención emitida correctamente.");
    expect(result.current.withholding?.id).toBe("wh-1");
  });

  it("si el backend falla, no llama message.success y expone el error real vía formatApiRequestError", async () => {
    vi.mocked(purchaseService.issueWithholding).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "Ya existe una retención emitida para esta compra." } },
      },
    });
    const result = await setupWithLoadedInvoice();

    await act(async () => {
      await result.current.handleIssueWithholding("ep-1");
    });

    expect(message.success).not.toHaveBeenCalled();
    expect(result.current.saveError).toBe(
      "Ya existe una retención emitida para esta compra.",
    );
  });

  it("no permite doble submit mientras la emisión anterior sigue en curso", async () => {
    let resolveIssue: (wh: IssuedWithholdingDto) => void = () => {};
    vi.mocked(purchaseService.issueWithholding).mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveIssue = resolve;
        }),
    );
    const result = await setupWithLoadedInvoice();

    let firstCall: Promise<void>;
    act(() => {
      firstCall = result.current.handleIssueWithholding("ep-1");
    });
    await waitFor(() => expect(result.current.whLoading).toBe(true));

    await act(async () => {
      await result.current.handleIssueWithholding("ep-1");
    });

    expect(purchaseService.issueWithholding).toHaveBeenCalledTimes(1);

    await act(async () => {
      resolveIssue({
        id: "wh-1",
        purchaseInvoiceId: "purchase-1",
        supplierId: "supplier-1",
        emissionPointId: "ep-1",
        withholdingNumber: "001-001-000000045",
        issueDate: "2026-08-15",
        accessKey: null,
        totalRetainedVat: 3,
        totalRetainedIncome: 1,
        totalRetainedIsd: 0,
        totalRetained: 4,
        status: "Issued",
        details: [],
        createdAt: "2026-08-15T10:00:00Z",
        updatedAt: null,
      });
      await firstCall;
    });
  });
});
