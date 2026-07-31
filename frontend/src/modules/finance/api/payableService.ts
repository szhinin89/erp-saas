import { apiGet } from "../../lib/apiEnvelope";

// ── DTOs (espejo exacto de PurchasePayableDto / PurchasePayableInstallmentDto — backend) ──

export interface PurchasePayableInstallmentDto {
  id: string;
  installmentNumber: number;
  dueDate: string;
  amount: number;
  paidAmount: number;
  status: string;
}

export interface PurchasePayableDto {
  id: string;
  purchaseId: string;
  supplierId: string;
  totalAmount: number;
  paidAmount: number;
  totalRetained: number;
  balanceDue: number;
  status: string;
  installmentCount: number;
  installments: PurchasePayableInstallmentDto[];
  createdAt: string;
  updatedAt: string | null;
}

export interface PayablesListResponse {
  items: PurchasePayableDto[];
  total: number;
  page: number;
  pageSize: number;
}

const BASE = "/api/v1/purchase-payables";

/** P0-03 — consulta de CxP pendientes para seleccionar qué pagar (ver FinancePaymentsController para el registro del pago). */
export const payableService = {
  list: (status?: string, pageNumber = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    if (status) params.set("status", status);
    params.set("pageNumber", String(pageNumber));
    params.set("pageSize", String(pageSize));
    return apiGet<PayablesListResponse>(`${BASE}?${params.toString()}`);
  },
};
