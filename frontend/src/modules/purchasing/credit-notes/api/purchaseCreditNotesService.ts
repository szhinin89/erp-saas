import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export interface PurchaseCreditNote {
  id: string;
  /** V2: businessPartnerId es el ID canónico del proveedor. */
  businessPartnerId: string;
  /** @deprecated Legacy — usar businessPartnerId. */
  supplierId?: string | null;
  purchBillId: string | null;
  expenseInvoiceId: string | null;
  noteType: string;
  reason: string;
  accessKey: string;
  issueDate: string;
  estabCode: string;
  emPointCode: string;
  sequential: string;
  subtotal: number;
  vatTotal: number;
  total: number;
  status: string;
  xmlPath: string | null;
  journalEntryId: string | null;
  createdAt: string;
}

export const purchaseCreditNotesService = {
  async list(params: { status?: string } = {}): Promise<PurchaseCreditNote[]> {
    const q = new URLSearchParams();
    if (params.status) q.set('estado', params.status);
    const qs = q.toString();
    const res = await api.get<ApiResponse<PurchaseCreditNote[]>>(
      `/api/purchases/credit-notes${qs ? `?${qs}` : ''}`,
    );
    return res.data.responseObject ?? [];
  },

  async importXml(file: File): Promise<PurchaseCreditNote> {
    const form = new FormData();
    form.append('xmlFile', file);
    const res = await api.post<ApiResponse<PurchaseCreditNote>>('/api/purchases/credit-notes', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    if (!res.data.responseObject) throw new Error('Importación sin respuesta del servidor.');
    return res.data.responseObject;
  },

  async approve(id: string, body?: { authorizationNumber?: string; authorizationDate?: string }): Promise<void> {
    await api.put(`/api/purchases/credit-notes/${id}/aprobar`, body ?? {});
  },
};
