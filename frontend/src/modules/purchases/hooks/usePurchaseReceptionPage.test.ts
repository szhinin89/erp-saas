// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { usePurchaseReceptionPage } from "./usePurchaseReceptionPage";
import {
  purchaseReceptionService,
  type PurchaseReceptionItem,
  type PurchaseReceptionXmlView,
} from "../api/purchaseReceptionService";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-PURCHASES-EXPENSES-03 — "Importar TXT de recepción": la tabla ya se
 * llenaba correctamente en éxito, pero no había feedback claro. Cubre message.success con conteo
 * de líneas, y que el error inline existente sigue funcionando sin catch vacío.
 */

vi.mock("../api/purchaseReceptionService", () => ({
  purchaseReceptionService: {
    importTxt: vi.fn(),
    downloadXml: vi.fn(),
    getXmlView: vi.fn(),
  },
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

function buildItem(overrides: Partial<PurchaseReceptionItem> = {}): PurchaseReceptionItem {
  return {
    supplierRuc: "0999999999001",
    supplierName: "Proveedor Uno",
    sourceDocType: "INVOICE" as const,
    invoiceNumber: "001-001-000000123",
    modifiedDocumentNumber: null,
    accessKey: "1234567890",
    issueDate: "2026-08-01",
    authorizationDate: "2026-08-01",
    subtotal: 100,
    vatAmount: 15,
    total: 115,
    supplierExists: true,
    supplierId: "supplier-1",
    supplierIsActive: true,
    purchaseExists: false,
    purchaseId: null,
    affectedPurchaseExists: false,
    affectedPurchaseId: null,
    status: "PENDING",
    documentId: "doc-1",
    documentStatus: "IMPORTED",
    processingStatus: "PROCESSED",
    processingNotes: null,
    supplierTradeName: null,
    ...overrides,
  };
}

function buildFile(): File {
  return new File(["contenido"], "recepcion.txt", { type: "text/plain" });
}

function buildXmlView(overrides: Partial<PurchaseReceptionXmlView> = {}): PurchaseReceptionXmlView {
  return {
    documentId: "doc-1",
    documentType: "CREDIT_NOTE",
    documentNumber: "001-001-000010350",
    issueDate: "2026-08-01",
    accessKey: "1234567890",
    authorizationNumber: null,
    authorizationDate: null,
    supplierName: "Proveedor Uno",
    supplierTradeName: null,
    supplierTaxId: "0999999999001",
    referralGuide: null,
    paymentMethodCode: null,
    paymentTerm: null,
    paymentTimeUnit: null,
    modifiedDocumentNumber: "001-001-000031760",
    modifiedDocumentType: null,
    modifiedDocumentDate: null,
    modificationReason: null,
    subtotal: 100,
    discountAmount: 0,
    iceAmount: 0,
    irbpnrAmount: 0,
    vatAmount: 15,
    tipAmount: 0,
    totalAmount: 115,
    lineCalculatedTotal: 115,
    roundingDifference: 0,
    taxSummaries: [],
    lines: [],
    rawXmlAvailable: false,
    rawXml: null,
    affectedPurchaseExists: false,
    affectedPurchaseId: null,
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("usePurchaseReceptionPage — importar TXT: feedback (CRITICAL-CONFIRMATIONS-PURCHASES-EXPENSES-03)", () => {
  it("al importar exitosamente muestra message.success con la cantidad de líneas cargadas", async () => {
    vi.mocked(purchaseReceptionService.importTxt).mockResolvedValue({
      items: [buildItem(), buildItem({ invoiceNumber: "001-001-000000124" })],
      totalParsed: 2,
      parseErrorCount: 0,
      skippedUnsupportedCount: 0,
    });
    const { result } = renderHook(() => usePurchaseReceptionPage());

    await act(async () => {
      await result.current.handleFileSelected(buildFile());
    });

    expect(result.current.result?.items).toHaveLength(2);
    expect(message.success).toHaveBeenCalledWith(
      "Archivo TXT importado correctamente. Se cargaron 2 líneas.",
    );
  });

  it("con cero líneas muestra el mensaje simple sin conteo", async () => {
    vi.mocked(purchaseReceptionService.importTxt).mockResolvedValue({
      items: [],
      totalParsed: 0,
      parseErrorCount: 0,
      skippedUnsupportedCount: 0,
    });
    const { result } = renderHook(() => usePurchaseReceptionPage());

    await act(async () => {
      await result.current.handleFileSelected(buildFile());
    });

    expect(message.success).toHaveBeenCalledWith("Archivo TXT importado correctamente.");
  });

  it("si falla, mantiene el error inline existente, no llama message.success y no deja catch vacío", async () => {
    vi.mocked(purchaseReceptionService.importTxt).mockRejectedValue({
      response: { data: { message: { user: "El archivo no tiene un formato válido." } } },
    });
    const { result } = renderHook(() => usePurchaseReceptionPage());

    await act(async () => {
      await result.current.handleFileSelected(buildFile());
    });

    expect(result.current.error).toBe("El archivo no tiene un formato válido.");
    expect(result.current.result).toBeNull();
    expect(message.success).not.toHaveBeenCalled();
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(purchaseReceptionService.importTxt).mockResolvedValue({
      items: [buildItem()],
      totalParsed: 1,
      parseErrorCount: 0,
      skippedUnsupportedCount: 0,
    });
    const { result } = renderHook(() => usePurchaseReceptionPage());

    await act(async () => {
      await result.current.handleFileSelected(buildFile());
    });

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});

it("does not claim an invoice supplier is registered without a resolved BP id", async () => {
  vi.mocked(purchaseReceptionService.importTxt).mockResolvedValue({
    items: [buildItem({ supplierExists: true, supplierId: null })],
    totalParsed: 1, parseErrorCount: 0, skippedUnsupportedCount: 0,
  });
  const { result } = renderHook(() => usePurchaseReceptionPage());
  await act(async () => { await result.current.handleFileSelected(buildFile()); });
  expect(result.current.items[0].supplierExists).toBe(false);
  act(() => result.current.handleSupplierCreated("0999999999001", ""));
  expect(result.current.items[0].supplierExists).toBe(false);
  act(() => result.current.handleSupplierCreated("0999999999001", "real-bp-id"));
  expect(result.current.items[0].supplierExists).toBe(true);
  expect(result.current.items[0].supplierId).toBe("real-bp-id");
});

describe("usePurchaseReceptionPage — processCreditNote (PURCHASE-CREDIT-NOTE-AFFECTED-INVOICE-RESOLVES-CANCELLED-01)", () => {
  const ncRow = buildItem({
    documentId: "doc-nc-1",
    sourceDocType: "CREDIT_NOTE",
    affectedPurchaseExists: true,
    affectedPurchaseId: "stale-cancelled-invoice-id",
  });

  it("nunca navega con el affectedPurchaseId ya cargado en la fila — siempre re-resuelve vía xml-view antes de abrir", async () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    vi.mocked(purchaseReceptionService.getXmlView).mockResolvedValue(
      buildXmlView({
        affectedPurchaseExists: true,
        affectedPurchaseId: "fresh-confirmed-invoice-id",
      }),
    );
    const { result } = renderHook(() => usePurchaseReceptionPage());

    await act(async () => {
      await result.current.processCreditNote(ncRow);
    });

    expect(purchaseReceptionService.getXmlView).toHaveBeenCalledWith("doc-nc-1");
    expect(open).toHaveBeenCalledWith(
      "/purchases/credit-notes/new?invoiceId=fresh-confirmed-invoice-id&receptionDocumentId=doc-nc-1",
      "_blank",
      "noopener,noreferrer",
    );
    expect(open).not.toHaveBeenCalledWith(
      expect.stringContaining("stale-cancelled-invoice-id"),
      expect.anything(),
      expect.anything(),
    );

    open.mockRestore();
  });

  it("si la resolución fresca no encuentra ninguna factura activa, muestra un mensaje claro y no navega", async () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    vi.mocked(purchaseReceptionService.getXmlView).mockResolvedValue(
      buildXmlView({ affectedPurchaseExists: false, affectedPurchaseId: null }),
    );
    const { result } = renderHook(() => usePurchaseReceptionPage());

    await act(async () => {
      await result.current.processCreditNote(ncRow);
    });

    expect(open).not.toHaveBeenCalled();
    expect(message.error).toHaveBeenCalledWith(
      expect.stringContaining("factura afectada"),
    );

    open.mockRestore();
  });
});
