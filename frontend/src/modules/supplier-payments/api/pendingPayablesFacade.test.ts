import { describe, it, expect, vi, beforeEach } from "vitest";

const listMock = vi.fn();
const getByIdMock = vi.fn();

vi.mock("../../payables/api/payablesService", () => ({
  payablesService: {
    list: (...args: unknown[]) => listMock(...args),
    getById: (...args: unknown[]) => getByIdMock(...args),
  },
}));

import { pendingPayablesFacade } from "./pendingPayablesFacade";

describe("pendingPayablesFacade.listPendingInstallments", () => {
  beforeEach(() => {
    listMock.mockReset();
    getByIdMock.mockReset();
  });

  it("combina pendientes/parciales, excluye pagadas/anuladas y ordena por vencimiento ascendente", async () => {
    listMock.mockImplementation((filters: { status: string }) =>
      Promise.resolve({
        items: filters.status === "pending" ? [{ id: "pay-2" }] : [{ id: "pay-1" }],
        total: 1,
        page: 1,
        pageSize: 100,
      }),
    );
    getByIdMock.mockImplementation((id: string) => {
      if (id === "pay-1") {
        return Promise.resolve({
          id: "pay-1",
          documentType: "FAC",
          documentNumber: "001-001-000031760",
          issueDate: "2026-09-01",
          installments: [
            {
              installmentId: "inst-1",
              installmentNumber: 1,
              dueDate: "2026-09-03",
              amount: 47.37,
              paidAmount: 3.84,
              outstandingAmount: 43.53,
              status: "partiallypaid",
            },
            {
              installmentId: "inst-1b",
              installmentNumber: 2,
              dueDate: "2026-10-01",
              amount: 10,
              paidAmount: 10,
              outstandingAmount: 0,
              status: "paid",
            },
          ],
        });
      }
      return Promise.resolve({
        id: "pay-2",
        documentType: "FAC",
        documentNumber: "001-001-000031700",
        issueDate: "2026-08-01",
        installments: [
          {
            installmentId: "inst-2",
            installmentNumber: 1,
            dueDate: "2026-08-15",
            amount: 20,
            paidAmount: 0,
            outstandingAmount: 20,
            status: "pending",
          },
          {
            installmentId: "inst-2b",
            installmentNumber: 2,
            dueDate: "2026-08-20",
            amount: 5,
            paidAmount: 0,
            outstandingAmount: 5,
            status: "cancelled",
          },
        ],
      });
    });

    const result = await pendingPayablesFacade.listPendingInstallments("sup-1");

    expect(result.map((r) => r.installmentId)).toEqual(["inst-2", "inst-1"]);
    expect(result[0]).toMatchObject({
      documentNumber: "001-001-000031700",
      issueDate: "2026-08-01",
      dueDate: "2026-08-15",
      totalAmount: 20,
      paidAmount: 0,
      outstandingAmount: 20,
      status: "pending",
    });
    expect(result[1]).toMatchObject({
      documentNumber: "001-001-000031760",
      issueDate: "2026-09-01",
      dueDate: "2026-09-03",
      totalAmount: 47.37,
      paidAmount: 3.84,
      outstandingAmount: 43.53,
      status: "partiallypaid",
    });
  });

  it("devuelve arreglo vacío si el proveedor no tiene CxP pendientes", async () => {
    listMock.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 100 });
    const result = await pendingPayablesFacade.listPendingInstallments("sup-2");
    expect(result).toEqual([]);
    expect(getByIdMock).not.toHaveBeenCalled();
  });
});
