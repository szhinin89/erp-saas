import { describe, expect, it } from "vitest";
import {
  buildExpenseDraftPayload,
  documentToHeader,
} from "./expenseDocumentDraftModel";
import type { ExpenseDocumentHeaderState } from "../components/ExpenseDocumentHeader";
import type { ExpenseDraftLineState } from "../components/ExpenseDocumentLinesEditor";
import type { ExpenseDocumentDetailDto } from "../api/expenseDocumentService";

/**
 * RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H — `buildExpenseDraftPayload` es la misma función que
 * construye tanto el payload de crear borrador como el de actualizar borrador (ver
 * `ExpenseDocumentFormPage.handleSave`, que llama `expenseDocumentService.create`/`.update` con
 * exactamente el mismo payload) — probarla aquí cubre ambos flujos sin duplicar el test.
 */

const BASE_HEADER: ExpenseDocumentHeaderState = {
  supplierId: "sup-1",
  issueDate: "2026-09-01",
  accountingDate: "2026-09-01",
  documentType: "01",
  documentNumber: "001-001-000000001",
  paymentTermId: "",
  dueDate: "",
  authorizationNumber: "",
  authorizationDate: "",
  notes: "",
  taxSupportCode: "",
};

const ONE_LINE: ExpenseDraftLineState[] = [
  {
    key: "line-1",
    expenseSubcategoryId: "sub-1",
    description: "Servicio",
    quantity: "1",
    unitPrice: "100.00",
    discountValue: "0.00",
    vatCode: "2",
    notes: "",
  },
];

describe("buildExpenseDraftPayload — taxSupportCode", () => {
  it("incluye taxSupportCode cuando el usuario lo ingresa (crear o actualizar borrador)", () => {
    const payload = buildExpenseDraftPayload(
      { ...BASE_HEADER, taxSupportCode: "02" },
      ONE_LINE,
    );

    expect(payload.taxSupportCode).toBe("02");
  });

  it("normaliza espacios en blanco alrededor del código, mismo criterio que el resto de códigos opcionales", () => {
    const payload = buildExpenseDraftPayload(
      { ...BASE_HEADER, taxSupportCode: "  02  " },
      ONE_LINE,
    );

    expect(payload.taxSupportCode).toBe("02");
  });

  it("envía null cuando el usuario lo deja vacío — nunca bloquea el payload", () => {
    const payload = buildExpenseDraftPayload(
      { ...BASE_HEADER, taxSupportCode: "" },
      ONE_LINE,
    );

    expect(payload.taxSupportCode).toBeNull();
    // El resto del payload sigue construyéndose con normalidad — un taxSupportCode vacío no
    // afecta ningún otro campo ni impide enviar la solicitud.
    expect(payload.supplierId).toBe("sup-1");
    expect(payload.lines).toHaveLength(1);
  });

  it("envía null cuando el usuario solo ingresa espacios", () => {
    const payload = buildExpenseDraftPayload(
      { ...BASE_HEADER, taxSupportCode: "   " },
      ONE_LINE,
    );

    expect(payload.taxSupportCode).toBeNull();
  });
});

describe("documentToHeader — taxSupportCode", () => {
  const DOCUMENT: ExpenseDocumentDetailDto = {
    id: "exp-1",
    companyId: "company-1",
    branchId: "branch-1",
    supplierId: "sup-1",
    supplierName: "Proveedor Uno S.A.",
    supplierTaxId: "0999999999001",
    issueDate: "2026-09-01",
    accountingDate: "2026-09-01",
    documentType: "01",
    documentNumber: "001-001-000000001",
    authorizationNumber: null,
    authorizationDate: null,
    paymentTermId: "",
    paymentTermName: "",
    dueDate: null,
    subtotal: 100,
    totalDiscount: 0,
    totalTax: 15,
    grandTotal: 115,
    notes: null,
    taxSupportCode: "02",
    status: "Draft",
    lines: [],
    cancelReason: null,
    cancelledAt: null,
    cancelledBy: null,
  };

  it("muestra el taxSupportCode ya guardado al editar el documento", () => {
    const header = documentToHeader(DOCUMENT, () => "");

    expect(header.taxSupportCode).toBe("02");
  });

  it("queda vacío (nunca null/undefined que rompa el <select> controlado) cuando el documento no tiene taxSupportCode", () => {
    const header = documentToHeader({ ...DOCUMENT, taxSupportCode: null }, () => "");

    expect(header.taxSupportCode).toBe("");
  });
});
