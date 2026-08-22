import { describe, expect, it } from "vitest";
import {
  buildReceiptPrintJobRequest,
  PrintAgentError,
  submitReceiptPrintJob,
  type PrintAgentConfig,
} from "./printAgentClient";
import type { SalesReceiptPrintPayloadDto } from "./salesService";

const config: PrintAgentConfig = {
  baseUrl: "http://127.0.0.1:9817",
  apiKey: "test-key",
  printerName: "POS-80",
  receiptEndpoint: "/print-jobs",
};

describe("printAgentClient", () => {
  it("builds deterministic receipt jobs from the official backend payload", () => {
    const request = buildReceiptPrintJobRequest(receiptPayload(), config);

    expect(request.jobId).toBe("invoice-inv-001-receipt");
    expect(request.printerName).toBe("POS-80");
    expect(request.copies).toBe(1);
    expect(request.receipt.merchantName).toBe("ZH Tech");
    expect(request.receipt.headerLines).toContain("Factura: 001-002-000000123");
    expect(request.receipt.headerLines).toContain("Cliente: Cliente POS");
    expect(request.receipt.items).toEqual([
      {
        name: "SKU-001 Producto Test",
        quantity: 2,
        unitPrice: 10,
        total: 20.7,
      },
    ]);
    expect(request.receipt.totals).toEqual([
      { label: "SUBTOTAL", amount: 20 },
      { label: "DESCUENTO", amount: 2 },
      { label: "IVA", amount: 2.7 },
      { label: "TOTAL", amount: 20.7 },
    ]);
    expect(request.receipt.rawLines).toContain("Pago: Efectivo 20.70");
    expect(request.receipt.footerLines).toEqual(["Gracias por su compra"]);
  });

  // SALES-PRESENTATIONS-04: la tirilla debe mostrar la presentación vendida (ej. "CAJA x12")
  // junto al nombre del producto — sin romper la impresión de productos sin presentación.
  it("incluye la presentación en el nombre cuando la línea se vendió por presentación (caja x12)", () => {
    const payload = receiptPayload();
    payload.lines = [
      {
        ...payload.lines[0],
        productName: "Atún",
        sku: "15865",
        quantity: 1,
        unitPrice: 18,
        uomCode: "CAJA",
        conversionFactor: 12,
      },
    ];

    const request = buildReceiptPrintJobRequest(payload, config);

    expect(request.receipt.items[0].name).toBe("15865 Atún — CAJA x12");
    expect(request.receipt.items[0].quantity).toBe(1);
  });

  it("sin presentación (factor 1): el nombre queda igual que antes (comportamiento actual)", () => {
    const request = buildReceiptPrintJobRequest(receiptPayload(), config);
    expect(request.receipt.items[0].name).toBe("SKU-001 Producto Test");
  });

  it("fails before calling the agent when api key is missing", async () => {
    await expect(
      submitReceiptPrintJob(buildReceiptPrintJobRequest(receiptPayload(), config), {
        ...config,
        apiKey: "",
      }),
    ).rejects.toMatchObject({
      kind: "not-configured",
      message: "API key del agente no configurada.",
    } satisfies Partial<PrintAgentError>);
  });
});

function receiptPayload(): SalesReceiptPrintPayloadDto {
  return {
    tenantId: "tenant-001",
    companyId: "company-001",
    branchId: "branch-001",
    companyName: "ZH Technologies S.A.",
    tradeName: "ZH Tech",
    ruc: "1790012345001",
    branchName: "Matriz",
    establishmentCode: "001",
    emissionPointCode: "002",
    cashRegisterName: "Caja Principal",
    cashSessionId: "cash-session-001",
    invoiceId: "inv-001",
    invoiceNumber: "001-002-000000123",
    issuedAt: "2026-08-21",
    customerName: "Cliente POS",
    customerIdentification: "1710034065",
    customerEmail: "cliente@example.com",
    documentType: "01",
    isElectronic: true,
    electronicStatus: "Authorized",
    accessKey: "1234567890123456789012345678901234567890123456789",
    authorizationNumber: "1234567890123456789012345678901234567890123456789",
    authorizationDate: "2026-08-21T14:30:00Z",
    lines: [
      {
        productName: "Producto Test",
        sku: "SKU-001",
        quantity: 2,
        unitPrice: 10,
        discount: 2,
        subtotal: 18,
        vatRate: 15,
        vatAmount: 2.7,
        total: 20.7,
        uomCode: "UNIT",
        conversionFactor: 1,
      },
    ],
    totals: {
      subtotalWithoutTaxes: 20,
      discountTotal: 2,
      vatTotal: 2.7,
      total: 20.7,
    },
    payments: [
      {
        method: "Efectivo",
        amount: 20.7,
        reference: null,
      },
    ],
    cashReceived: null,
    cashChange: null,
    footerMessage: "Gracias por su compra",
  };
}
