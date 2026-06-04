import { apiGet, apiPost } from '../../lib/apiEnvelope';
import type { LotDto, SerialDto } from '../../../types/items';

export interface RegisterLotRequest {
  itemId: string;
  warehouseId: string;
  lotNumber: string;
  initialQty: number;
  variantId?: string | null;
  manufactureDate?: string | null;
  expirationDate?: string | null;
  notes?: string | null;
}

export interface RegisterSerialRequest {
  itemId: string;
  warehouseId: string;
  serials: string[];
  variantId?: string | null;
  lotId?: string | null;
  notes?: string | null;
}

export const inventoryItemService = {
  getLots: (itemId: string, warehouseId?: string, status?: string) => {
    const q = new URLSearchParams({ itemId });
    if (warehouseId) q.set('warehouseId', warehouseId);
    if (status) q.set('status', status);
    return apiGet<LotDto[]>(`/api/inventory/lots?${q.toString()}`).then((d) => d ?? []);
  },

  registerLot: (request: RegisterLotRequest) =>
    apiPost<LotDto>('/api/inventory/lots', request),

  getSerials: (itemId: string, warehouseId?: string, status?: string) => {
    const q = new URLSearchParams({ itemId });
    if (warehouseId) q.set('warehouseId', warehouseId);
    if (status) q.set('status', status);
    return apiGet<SerialDto[]>(`/api/inventory/serials?${q.toString()}`).then((d) => d ?? []);
  },

  registerSerials: (request: RegisterSerialRequest) =>
    apiPost<SerialDto[]>('/api/inventory/serials', request),
};
