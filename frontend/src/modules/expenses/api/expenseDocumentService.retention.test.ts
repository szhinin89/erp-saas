import { describe, it, expect, vi, beforeEach } from "vitest";

/**
 * RETENTIONS-UI-EXPENSES-01F — pruebas a nivel de service para las funciones que no tienen un
 * punto de entrada en la UI todavía (`createConfirmedExpense`) y para el manejo de 404 como
 * estado neutro de `getExpenseRetention` (nunca un error genérico).
 */

const apiGetMock = vi.fn();
const apiPostMock = vi.fn();
const apiPutMock = vi.fn();
const rawApiGetMock = vi.fn();

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: (...args: unknown[]) => apiGetMock(...args),
  apiPost: (...args: unknown[]) => apiPostMock(...args),
  apiPut: (...args: unknown[]) => apiPutMock(...args),
}));

vi.mock("../../lib/api", () => ({
  api: { get: (...args: unknown[]) => rawApiGetMock(...args) },
}));

import { expenseDocumentService } from "./expenseDocumentService";
import type { CreateExpenseDraftPayload, RetentionIntentRequest } from "./expenseDocumentService";

beforeEach(() => {
  apiGetMock.mockReset();
  apiPostMock.mockReset();
  apiPutMock.mockReset();
  rawApiGetMock.mockReset();
});

const PAYLOAD: CreateExpenseDraftPayload = {
  supplierId: "sup-1",
  issueDate: "2026-09-01",
  accountingDate: "2026-09-01",
  documentType: "01",
  documentNumber: "001-001-000000001",
  paymentTermId: null,
  dueDate: null,
  lines: [
    {
      expenseSubcategoryId: "sub-1",
      description: "Servicio",
      quantity: 1,
      unitPrice: 100,
      discountValue: 0,
      vatCode: "2",
    },
  ],
  authorizationNumber: null,
  authorizationDate: null,
  notes: null,
  taxSupportCode: "02",
};

const RETENTION_INTENT: RetentionIntentRequest = {
  appliesRetention: true,
  emissionPointId: "ep-1",
  issueDate: "2026-09-01",
  lines: [
    {
      taxType: "Vat",
      retentionCode: "303",
      baseAmount: 100,
      retentionRate: 30,
      retainedAmount: 30,
      description: null,
    },
  ],
};

describe("expenseDocumentService — confirm (regresión + retención)", () => {
  it("confirm(id) sin segundo argumento sigue enviando el body vacío de siempre", async () => {
    apiPostMock.mockResolvedValue({});
    await expenseDocumentService.confirm("exp-1");
    expect(apiPostMock).toHaveBeenCalledWith(
      "/api/v1/expenses/documents/exp-1/confirm",
      {},
    );
  });

  it("confirm(id, retention) agrega la clave retention al body", async () => {
    apiPostMock.mockResolvedValue({});
    await expenseDocumentService.confirm("exp-1", RETENTION_INTENT);
    expect(apiPostMock).toHaveBeenCalledWith(
      "/api/v1/expenses/documents/exp-1/confirm",
      { retention: RETENTION_INTENT },
    );
  });

  it("RETENTIONS-UI-REMOVE-MANUAL-NUMBER-02F: el body de retention nunca incluye retentionNumber, pero conserva emissionPointId", async () => {
    apiPostMock.mockResolvedValue({});
    await expenseDocumentService.confirm("exp-1", RETENTION_INTENT);
    const [, body] = apiPostMock.mock.calls[0] as [
      string,
      { retention: Record<string, unknown> },
    ];
    expect(body.retention).not.toHaveProperty("retentionNumber");
    expect(body.retention).toHaveProperty("emissionPointId", "ep-1");
  });
});

describe("expenseDocumentService — create/update draft con taxSupportCode", () => {
  it("create(payload) envía taxSupportCode tal cual en el body", async () => {
    apiPostMock.mockResolvedValue({});
    await expenseDocumentService.create(PAYLOAD);
    expect(apiPostMock).toHaveBeenCalledWith(
      "/api/v1/expenses/documents",
      expect.objectContaining({ taxSupportCode: "02" }),
    );
  });

  it("update(id, payload) envía taxSupportCode tal cual en el body", async () => {
    apiPutMock.mockResolvedValue({});
    await expenseDocumentService.update("exp-1", PAYLOAD);
    expect(apiPutMock).toHaveBeenCalledWith(
      "/api/v1/expenses/documents/exp-1",
      expect.objectContaining({ taxSupportCode: "02" }),
    );
  });

  it("create(payload) con taxSupportCode null no rompe el body", async () => {
    apiPostMock.mockResolvedValue({});
    await expenseDocumentService.create({ ...PAYLOAD, taxSupportCode: null });
    const [, body] = apiPostMock.mock.calls[0] as [string, Record<string, unknown>];
    expect(body.taxSupportCode).toBeNull();
    expect(body.supplierId).toBe("sup-1");
  });
});

describe("expenseDocumentService.createConfirmedExpense", () => {
  it("sin retentionIntent, envía solo el payload del borrador (comportamiento por defecto)", async () => {
    apiPostMock.mockResolvedValue({});
    await expenseDocumentService.createConfirmedExpense(PAYLOAD);
    expect(apiPostMock).toHaveBeenCalledWith(
      "/api/v1/expenses/documents/confirmed",
      PAYLOAD,
    );
    const [, body] = apiPostMock.mock.calls[0] as [string, Record<string, unknown>];
    expect(body).not.toHaveProperty("retention");
  });

  it("con retentionIntent, lo incluye en el body junto al payload del borrador", async () => {
    apiPostMock.mockResolvedValue({});
    await expenseDocumentService.createConfirmedExpense(PAYLOAD, RETENTION_INTENT);
    expect(apiPostMock).toHaveBeenCalledWith("/api/v1/expenses/documents/confirmed", {
      ...PAYLOAD,
      retention: RETENTION_INTENT,
    });
  });

  it("el body nunca incluye tenantId/companyId/branchId", async () => {
    apiPostMock.mockResolvedValue({});
    await expenseDocumentService.createConfirmedExpense(PAYLOAD, RETENTION_INTENT);
    const [, body] = apiPostMock.mock.calls[0] as [string, Record<string, unknown>];
    expect(body).not.toHaveProperty("tenantId");
    expect(body).not.toHaveProperty("companyId");
    expect(body).not.toHaveProperty("branchId");
  });

  it("RETENTIONS-UI-REMOVE-MANUAL-NUMBER-02F: el body de retention nunca incluye retentionNumber, pero conserva emissionPointId", async () => {
    apiPostMock.mockResolvedValue({});
    await expenseDocumentService.createConfirmedExpense(PAYLOAD, RETENTION_INTENT);
    const [, body] = apiPostMock.mock.calls[0] as [
      string,
      { retention: Record<string, unknown> },
    ];
    expect(body.retention).not.toHaveProperty("retentionNumber");
    expect(body.retention).toHaveProperty("emissionPointId", "ep-1");
  });

  it("RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H: el body incluye taxSupportCode del payload del borrador", async () => {
    apiPostMock.mockResolvedValue({});
    await expenseDocumentService.createConfirmedExpense(PAYLOAD);
    const [, body] = apiPostMock.mock.calls[0] as [string, Record<string, unknown>];
    expect(body).toHaveProperty("taxSupportCode", "02");
  });

  it("RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H: taxSupportCode ausente/null no rompe el body", async () => {
    apiPostMock.mockResolvedValue({});
    await expenseDocumentService.createConfirmedExpense({
      ...PAYLOAD,
      taxSupportCode: null,
    });
    const [, body] = apiPostMock.mock.calls[0] as [string, Record<string, unknown>];
    expect(body.taxSupportCode).toBeNull();
    expect(body.supplierId).toBe("sup-1");
  });
});

describe("expenseDocumentService.getExpenseRetention", () => {
  it("200 con retención → devuelve el DTO", async () => {
    const dto = { id: "ret-1", retentionNumber: "001" };
    rawApiGetMock.mockResolvedValue({ data: { data: dto } });
    const result = await expenseDocumentService.getExpenseRetention("exp-1");
    expect(result).toEqual(dto);
  });

  it("404 → devuelve null, nunca lanza (estado normal, no un error)", async () => {
    rawApiGetMock.mockRejectedValue({
      isAxiosError: true,
      response: { status: 404 },
    });
    const result = await expenseDocumentService.getExpenseRetention("exp-1");
    expect(result).toBeNull();
  });

  it("otro error HTTP (p. ej. 500) sí se propaga", async () => {
    rawApiGetMock.mockRejectedValue({
      isAxiosError: true,
      response: { status: 500 },
    });
    await expect(expenseDocumentService.getExpenseRetention("exp-1")).rejects.toBeTruthy();
  });
});
