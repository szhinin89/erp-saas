import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export interface WithholdingIssuedItem {
  id: string;
  /** V2: businessPartnerId es el ID canónico del proveedor. */
  businessPartnerId: string;
  /** @deprecated Legacy — usar businessPartnerId. */
  supplierId?: string | null;
  accessKey: string;
  status: string;
  totalRetained: number;
  issueDate: string;
}

export const withholdingIssuedService = {
  async list(businessPartnerId?: string): Promise<WithholdingIssuedItem[]> {
    const q = businessPartnerId ? `?proveedorId=${encodeURIComponent(businessPartnerId)}` : '';
    const res = await api.get<ApiResponse<WithholdingIssuedItem[]>>(`/api/purchases/withholding-issued${q}`);
    return res.data.responseObject ?? [];
  },

  async send(id: string): Promise<void> {
    await api.put(`/api/purchases/withholding-issued/${id}/enviar`, {});
  },
};
