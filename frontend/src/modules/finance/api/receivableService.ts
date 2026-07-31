import { apiGet } from "../../lib/apiEnvelope";

// ── DTOs (espejo exacto de SalesReceivableDto / SalesReceivableInstallmentDto — backend) ──

export interface SalesReceivableInstallmentDto {
  id: string;
  installmentNumber: number;
  dueDate: string;
  amount: number;
  paidAmount: number;
  status: string;
}

export interface SalesReceivableDto {
  id: string;
  invoiceId: string;
  customerId: string;
  originalAmount: number;
  paidAmount: number;
  balanceDue: number;
  status: string;
  installmentCount: number;
  installments: SalesReceivableInstallmentDto[];
  createdAt: string;
  updatedAt: string | null;
}

export interface ReceivablesListResponse {
  items: SalesReceivableDto[];
  total: number;
  page: number;
  pageSize: number;
}

const BASE = "/api/v1/sales-receivables";

/** P0-03 — consulta de CxC pendientes para seleccionar qué cobrar (ver FinancePaymentsController para el registro del cobro). */
export const receivableService = {
  list: (status?: string, pageNumber = 1, pageSize = 25) => {
    const params = new URLSearchParams();
    if (status) params.set("status", status);
    params.set("pageNumber", String(pageNumber));
    params.set("pageSize", String(pageSize));
    return apiGet<ReceivablesListResponse>(`${BASE}?${params.toString()}`);
  },
};
