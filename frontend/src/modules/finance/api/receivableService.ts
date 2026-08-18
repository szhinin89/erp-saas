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

/**
 * FINANCE-RECEIVABLES-LIST-ENTERPRISE-01 — DTO enriquecido con los datos de la factura origen
 * (número, cliente, sucursal, usuario que facturó) que el backend ya resuelve — el frontend
 * nunca debe mostrar `customerId` crudo ni armar el estado a partir de un literal genérico.
 * `branchName`/`createdByName` pueden venir `null` si el registro referenciado ya no existe —
 * usar un fallback textual, nunca el GUID.
 */
export interface SalesReceivableDto {
  id: string;
  invoiceId: string;
  invoiceNumber: string;
  customerId: string;
  customerName: string;
  customerIdentification: string;
  branchId: string;
  branchName: string | null;
  createdByUserId: string | null;
  createdByName: string | null;
  invoiceIssuedAt: string;
  invoiceCreatedAt: string;
  dueDate: string | null;
  originalAmount: number;
  paidAmount: number;
  balanceDue: number;
  status: string;
  statusLabel: string;
  installmentCount: number;
  overdueDays: number | null;
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
