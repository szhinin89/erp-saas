import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';

export interface WithholdingReceivedItem {
  id: string;
  customerId: string;
  accessKey: string;
  issueDate: string;
  retainedAmount: number;
  salesBillId: string | null;
}

export const withholdingReceivedService = {
  async list(): Promise<WithholdingReceivedItem[]> {
    const res = await api.get<ApiResponse<WithholdingReceivedItem[]>>('/api/sales/withholding-received');
    return res.data.responseObject ?? [];
  },
};
