import { apiGet, apiPost } from '../../lib/apiEnvelope';
import type { KitDto, KitLineDto } from '../../../types/items';

export const kitService = {
  getByItem: (itemId: string) =>
    apiGet<KitDto>(`/api/kits/by-item/${itemId}`),

  create: (itemId: string, kitType: string) =>
    apiPost<KitDto>('/api/kits', { itemId, kitType }),

  addLine: (
    kitId: string,
    line: {
      componentItemId: string;
      uomCode: string;
      quantity: number;
      componentVariantId?: string | null;
      isOptional?: boolean;
      sortOrder?: number;
    }
  ) => apiPost<KitLineDto>(`/api/kits/${kitId}/lines`, line),
};
