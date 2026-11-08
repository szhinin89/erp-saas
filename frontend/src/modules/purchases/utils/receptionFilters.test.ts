import { describe, expect, it } from "vitest";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import {
  EMPTY_RECEPTION_FILTERS,
  buildReceptionFilterPredicate,
  countActiveAdvancedFilters,
  parseReceptionFilterAmount,
  receptionRequiresAttention,
  receptionSupplierState,
  receptionXmlState,
  type ReceptionFilters,
  type ReceptionXmlSessionMap,
} from "./receptionFilters";
import { receptionSummaryBucket, summarizeReceptionItems } from "./receptionErpRecord";
import { resolveReceptionDocumentBuckets } from "./receptionNormalize";
import { buildReceptionRow as row } from "./reception.testFixtures";

const stored = { documentStatus: "VERIFIED", processingStatus: "PROCESSED" } as const;

// Lote representativo: cada fila cubre una situación real distinta.
const rows: PurchaseReceptionItem[] = [
  row({ documentId: "ready", ...stored, supplierName: "Ácido Norte", supplierRuc: "111", total: 50 }),
  row({ documentId: "confirmed", ...stored, purchaseState: "CONFIRMED", status: "IMPORTED", issueDate: "2026-08-10" }),
  row({ documentId: "expense", ...stored, expenseExists: true, total: 300 }),
  row({ documentId: "cancelled", ...stored, purchaseState: "CANCELLED", cancelledPurchaseId: "p-old" }),
  row({ documentId: "noXml", issueDate: "2026-07-15", accessKey: "0108202601999" }),
  row({
    documentId: "newSupplier",
    supplierExists: false,
    supplierId: null,
    supplierIsActive: null,
    status: "NEW_SUPPLIER",
    supplierName: "Nuevo SA",
    supplierRuc: "222",
  }),
  row({ documentId: "inactive", ...stored, supplierIsActive: false }),
  row({ documentId: "failed", documentStatus: "VERIFIED", processingStatus: "FAILED" }),
  row({ documentId: "warnings", documentStatus: "VERIFIED", processingStatus: "PROCESSED_WITH_WARNINGS" }),
  row({
    documentId: "nc",
    sourceDocType: "CREDIT_NOTE",
    documentStatus: "PROCESSED",
    processingStatus: "PROCESSED",
    creditNoteExists: true,
    creditNoteId: "cn-1",
    invoiceNumber: "001-002-000000777",
  }),
];

function ids(filters: Partial<ReceptionFilters>, session: ReceptionXmlSessionMap = {}) {
  const matches = buildReceptionFilterPredicate({ ...EMPTY_RECEPTION_FILTERS, ...filters }, session);
  return rows.filter(matches).map((r) => r.documentId);
}

describe("receptionFilters — estados derivados", () => {
  it("XML: almacenado según documentStatus; consultando/error solo desde la sesión", () => {
    expect(receptionXmlState({ documentStatus: "VERIFIED" }, "error")).toBe("stored");
    expect(receptionXmlState({ documentStatus: "PROCESSED" }, undefined)).toBe("stored");
    expect(receptionXmlState({ documentStatus: "IMPORTED" }, undefined)).toBe("pending");
    expect(receptionXmlState({ documentStatus: "IMPORTED" }, "loading")).toBe("loading");
    expect(receptionXmlState({ documentStatus: "IMPORTED" }, "error")).toBe("error");
  });

  it("proveedor: misma regla que la columna (Factura exige supplierId; NC solo supplierExists)", () => {
    expect(receptionSupplierState(row())).toBe("registered");
    expect(receptionSupplierState(row({ supplierIsActive: false }))).toBe("inactive");
    expect(receptionSupplierState(row({ supplierId: null }))).toBe("notRegistered");
    expect(
      receptionSupplierState(row({ sourceDocType: "CREDIT_NOTE", supplierId: null })),
    ).toBe("registered");
    expect(receptionSupplierState(row({ supplierExists: false }))).toBe("notRegistered");
  });

  it("montos: vacío o no numérico no limita", () => {
    expect(parseReceptionFilterAmount("")).toBeNull();
    expect(parseReceptionFilterAmount("  ")).toBeNull();
    expect(parseReceptionFilterAmount("abc")).toBeNull();
    expect(parseReceptionFilterAmount("10.50")).toBe(10.5);
  });
});

describe("receptionFilters — filtros individuales", () => {
  it("sin criterios devuelve todo", () => {
    expect(ids({})).toHaveLength(rows.length);
  });

  it("búsqueda por RUC, proveedor (sin tildes/mayúsculas), número y clave de acceso", () => {
    expect(ids({ search: "222" })).toEqual(["newSupplier"]);
    expect(ids({ search: "acido norte" })).toEqual(["ready"]);
    expect(ids({ search: "000000777" })).toEqual(["nc"]);
    expect(ids({ search: "0108202601999" })).toEqual(["noXml"]);
  });

  it("selector de proveedores por RUC", () => {
    expect(ids({ supplierRucs: ["111", "222"] })).toEqual(["ready", "newSupplier"]);
  });

  it("situación ERP: registrados, sin registrar (incluye historial) e historial anulado", () => {
    expect(ids({ erpSituation: "registered" })).toEqual(["confirmed", "expense", "nc"]);
    expect(ids({ erpSituation: "unregistered" })).toEqual([
      "ready",
      "cancelled",
      "noXml",
      "newSupplier",
      "inactive",
      "failed",
      "warnings",
    ]);
    expect(ids({ erpSituation: "cancelledHistory" })).toEqual(["cancelled"]);
  });

  it("estado del proveedor", () => {
    expect(ids({ supplierState: "inactive" })).toEqual(["inactive"]);
    expect(ids({ supplierState: "notRegistered" })).toEqual(["newSupplier"]);
  });

  it("XML: almacenado / pendiente / estados de sesión", () => {
    expect(ids({ xmlState: "pending" })).toEqual(["noXml", "newSupplier"]);
    expect(ids({ xmlState: "error" }, { noXml: "error" })).toEqual(["noXml"]);
    expect(ids({ xmlState: "loading" }, { newSupplier: "loading" })).toEqual(["newSupplier"]);
    expect(ids({ xmlState: "pending" }, { noXml: "error" })).toEqual(["newSupplier"]);
    expect(ids({ xmlState: "stored" })).not.toContain("noXml");
  });

  it("recepción, lectura y tipo", () => {
    expect(ids({ documentStatus: "PROCESSED" })).toEqual(["nc"]);
    expect(ids({ processingStatus: "FAILED" })).toEqual(["failed"]);
    expect(ids({ docType: "CREDIT_NOTE" })).toEqual(["nc"]);
  });

  it("fecha de emisión desde/hasta (inclusive)", () => {
    expect(ids({ issueDateFrom: "2026-08-10" })).toEqual(["confirmed"]);
    expect(ids({ issueDateTo: "2026-07-15" })).toEqual(["noXml"]);
    expect(ids({ issueDateFrom: "2026-07-15", issueDateTo: "2026-07-15" })).toEqual(["noXml"]);
  });

  it("total mínimo/máximo (inclusive)", () => {
    expect(ids({ totalMax: "50" })).toEqual(["ready"]);
    expect(ids({ totalMin: "300" })).toEqual(["expense"]);
  });

  it("situación de tarjeta: filtra exactamente su partición", () => {
    for (const bucket of ["registered", "toProcess", "pendingSupplier"] as const) {
      expect(ids({ erpSituation: bucket })).toEqual(
        rows.filter((r) => receptionSummaryBucket(r) === bucket).map((r) => r.documentId),
      );
    }
    const counts = summarizeReceptionItems(rows);
    expect(ids({ erpSituation: "registered" })).toHaveLength(counts.registered);
    expect(ids({ erpSituation: "toProcess" })).toHaveLength(counts.toProcess);
    expect(ids({ erpSituation: "pendingSupplier" })).toHaveLength(counts.pendingSupplier);
    // "Sin registrar" = Por procesar + Pendientes de proveedor.
    expect(ids({ erpSituation: "unregistered" })).toHaveLength(counts.toProcess + counts.pendingSupplier);
    expect(counts.registered + counts.toProcess + counts.pendingSupplier).toBe(rows.length);
  });
});

describe("receptionFilters — requieren atención", () => {
  it("excluye registrados y documentos listos; incluye proveedor, XML y lectura con problemas", () => {
    expect(ids({ attentionOnly: true })).toEqual([
      "noXml",
      "newSupplier",
      "inactive",
      "failed",
      "warnings",
    ]);
  });

  it("no equivale a 'sin registrar': listo para registrar e historial anulado limpio quedan fuera", () => {
    const attention = ids({ attentionOnly: true });
    const unregistered = ids({ erpSituation: "unregistered" });
    expect(unregistered).toContain("ready");
    expect(unregistered).toContain("cancelled");
    expect(attention).not.toContain("ready");
    expect(attention).not.toContain("cancelled");
  });

  it("el error de XML de la sesión cuenta como atención; un registro activo nunca", () => {
    const confirmedNoXml = row({ purchaseState: "CONFIRMED", status: "IMPORTED" });
    expect(receptionRequiresAttention(confirmedNoXml, "error")).toBe(false);
    expect(receptionRequiresAttention(row({ ...stored }), "error")).toBe(false);
    expect(receptionRequiresAttention(row(), "error")).toBe(true);
  });
});

describe("receptionFilters — En revisión", () => {
  it("situación por comprobante: revisión aislada de tarjetas, Sin registrar e Historial anulado", () => {
    const review = [
      row({ documentId: "r", total: 1, purchaseState: "CANCELLED", cancelledPurchaseId: "p" }),
      row({ documentId: "r", total: 2, purchaseState: "CANCELLED", cancelledPurchaseId: "p" }),
    ];
    const lot = [...rows, ...review];
    const bucketOf = resolveReceptionDocumentBuckets(lot);
    const pick = (filters: Partial<ReceptionFilters>) =>
      lot
        .filter(buildReceptionFilterPredicate({ ...EMPTY_RECEPTION_FILTERS, ...filters }, {}, bucketOf))
        .map((r) => `${r.documentId}:${r.total}`);
    expect(pick({ erpSituation: "review" })).toEqual(["r:1", "r:2"]);
    for (const situation of ["registered", "toProcess", "pendingSupplier", "unregistered", "cancelledHistory"] as const) {
      expect(pick({ erpSituation: situation }).some((id) => id.startsWith("r:"))).toBe(false);
    }
    // Se combina con criterios independientes.
    expect(pick({ erpSituation: "review", totalMax: "1" })).toEqual(["r:1"]);
  });
});

describe("receptionFilters — combinaciones", () => {
  it("todos los criterios se combinan con Y lógico", () => {
    expect(ids({ attentionOnly: true, processingStatus: "FAILED" })).toEqual(["failed"]);
    expect(ids({ erpSituation: "toProcess", xmlState: "pending" })).toEqual(["noXml"]);
    // Una sola situación a la vez; se combina con criterios independientes (proveedor, XML…).
    expect(ids({ erpSituation: "pendingSupplier", supplierState: "notRegistered" })).toEqual([
      "newSupplier",
    ]);
    expect(ids({ docType: "INVOICE", erpSituation: "registered", totalMin: "200" })).toEqual([
      "expense",
    ]);
  });

  it("cuenta solo los criterios avanzados activos", () => {
    expect(countActiveAdvancedFilters(EMPTY_RECEPTION_FILTERS)).toBe(0);
    expect(
      countActiveAdvancedFilters({
        ...EMPTY_RECEPTION_FILTERS,
        search: "x",
        attentionOnly: true,
        docType: "INVOICE",
        totalMin: "10",
      }),
    ).toBe(2);
  });
});
