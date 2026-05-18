import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';

export interface SalesNoteDto {
  id: string;
  originalInvoiceId: string;
  noteType: string;
  status: string;
  accessKey: string;
  total: number;
  issueDate: string;
}

export interface CreateNoteItemDto {
  productId: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateCreditNoteRequest {
  originalBillId: string;
  noteType: string;
  reason: string;
  items: CreateNoteItemDto[];
}

function extractId(body: unknown): string {
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if (typeof o['responseObject'] === 'string') return o['responseObject'];
    if (typeof o['ResponseObject'] === 'string') return o['ResponseObject'];
  }
  return String(body ?? '');
}

export const creditNotesService = {
  async list(params: { invoiceId?: string; status?: string } = {}): Promise<SalesNoteDto[]> {
    const q = new URLSearchParams();
    if (params.invoiceId) q.set('facturaId', params.invoiceId);
    if (params.status)    q.set('estado', params.status);
    const res = await api.get<ApiResponse<SalesNoteDto[]>>(`/api/sales/credit-notes?${q.toString()}`);
    return res.data.responseObject ?? [];
  },

  async create(payload: CreateCreditNoteRequest): Promise<string> {
    const res = await api.post<ApiResponse<unknown>>('/api/sales/credit-notes', payload);
    return extractId(res.data);
  },

  async send(id: string): Promise<void> {
    await api.put(`/api/sales/credit-notes/${id}/enviar`, {});
  },
};
