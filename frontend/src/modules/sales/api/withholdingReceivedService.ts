import { api } from '../../lib/api';
import { apiPost } from '../../lib/apiEnvelope';
import type { ApiResponse } from '../../../types/api';

export interface WithholdingReceivedItem {
  id: string;
  customerId: string;
  accessKey: string;
  issueDate: string;
  retainedAmount: number;
  salesBillId: string | null;
}

export interface RegisterWithholdingBody {
  salesBillId: string;
  xmlContent: string;
}

export const withholdingReceivedService = {
  async list(): Promise<WithholdingReceivedItem[]> {
    const res = await api.get<ApiResponse<WithholdingReceivedItem[]>>('/api/sales/withholding-received');
    return res.data.responseObject ?? [];
  },

  create(body: RegisterWithholdingBody): Promise<string> {
    return apiPost<string>('/api/sales/withholding-received', body);
  },
};
