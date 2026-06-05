import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export interface WithholdingIssuedItem {
  id: string;
  supplierId: string;
  accessKey: string;
  status: string;
  totalRetained: number;
  issueDate: string;
}

export const withholdingIssuedService = {
  async list(supplierId?: string): Promise<WithholdingIssuedItem[]> {
    const q = supplierId ? `?proveedorId=${encodeURIComponent(supplierId)}` : '';
    const res = await api.get<ApiResponse<WithholdingIssuedItem[]>>(`/api/purchases/withholding-issued${q}`);
    return res.data.responseObject ?? [];
  },

  async send(id: string): Promise<void> {
    await api.put(`/api/purchases/withholding-issued/${id}/enviar`, {});
  },
};
