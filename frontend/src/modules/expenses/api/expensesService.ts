import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';

// Estado enum: 0=Draft, 1=Validated, 2=Approved, 3=Rejected
export type EstadoGasto = 0 | 1 | 2 | 3;

export interface GastoDto {
  id: string;
  accessKey: string | null;
  issueDate: string;
  supplierId: string | null;
  invoiceNumber: string | null;
  description: string;
  expenseCategory: string;
  subtotal: number;
  vatTotal: number;
  total: number;
  status: EstadoGasto;
  xmlPath: string | null;
  notes: string | null;
  journalEntryId: string | null;
  createdAt: string;
}

export interface ExpenseCategoryDto {
  id: string;
  name: string;
  accountCode: string | null;
}

export interface CreateExpenseRequest {
  /** V2: businessPartnerId es el ID canónico del proveedor. supplierId eliminado. */
  businessPartnerId: string | null;
  issueDate: string;
  invoiceNumber: string | null;
  concept: string;
  category: string;
  subtotal: number;
  vatTotal: number | null;
  total: number;
  notes: string | null;
}

function readObj<T>(body: unknown): T {
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if ('responseObject' in o) return o.responseObject as T;
    if ('ResponseObject' in o) return o.ResponseObject as T;
  }
  return body as T;
}

export const expensesService = {
  async list(filtros: { status?: EstadoGasto; proveedorId?: string } = {}): Promise<GastoDto[]> {
    const q = new URLSearchParams();
    if (filtros.status !== undefined) q.set('estado', String(filtros.status));
    if (filtros.proveedorId) q.set('proveedorId', filtros.proveedorId);
    const res = await api.get<ApiResponse<GastoDto[]>>(`/api/expenses?${q.toString()}`);
    return readObj<GastoDto[]>(res.data) ?? [];
  },

  async getCategorias(): Promise<ExpenseCategoryDto[]> {
    const res = await api.get<ApiResponse<ExpenseCategoryDto[]>>(
      '/api/finance/config/gastos'
    );
    return readObj<ExpenseCategoryDto[]>(res.data) ?? [];
  },

  async crearManual(payload: CreateExpenseRequest): Promise<GastoDto> {
    const res = await api.post<ApiResponse<GastoDto>>('/api/expenses/manual', {
      modo: 1, // Manual
      businessPartnerId: payload.businessPartnerId,
      issueDate: payload.issueDate,
      invoiceNumber: payload.invoiceNumber,
      concept: payload.concept,
      category: payload.category,
      subtotal: payload.subtotal,
      vatTotal: payload.vatTotal ?? 0,
      total: payload.total,
      notes: payload.notes,
      xmlContent: null,
      xmlFileName: null,
    });
    return readObj<GastoDto>(res.data);
  },

  async validar(id: string): Promise<void> {
    await api.patch(`/api/expenses/${id}/validar`, {});
  },

  async aprobar(id: string): Promise<void> {
    await api.patch(`/api/expenses/${id}/aprobar`, {});
  },

  async rechazar(id: string, motivo: string): Promise<void> {
    await api.patch(`/api/expenses/${id}/rechazar`, { reason: motivo });
  },
};
