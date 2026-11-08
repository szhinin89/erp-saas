import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";

/** Fila base de Recepción para pruebas: factura con proveedor activo, sin XML ni registro ERP. */
export function buildReceptionRow(
  overrides: Partial<PurchaseReceptionItem> = {},
): PurchaseReceptionItem {
  return {
    purchaseState: "NONE",
    supplierRuc: "0999999999001",
    supplierName: "Proveedor Uno",
    sourceDocType: "INVOICE",
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
    purchaseId: null,
    affectedPurchaseExists: false,
    affectedPurchaseId: null,
    status: "PENDING",
    documentId: "doc-1",
    documentStatus: "IMPORTED",
    processingStatus: "PENDING",
    processingNotes: null,
    supplierTradeName: null,
    ...overrides,
  };
}
