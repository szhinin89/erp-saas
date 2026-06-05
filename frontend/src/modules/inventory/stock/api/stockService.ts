import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export interface StockListItem {
  id: string;
  productId: string;
  warehouseId: string;
  quantity: number;
  reservedQty: number;
  availableQuantity: number;
  ultimaActualizacion: string;
}

export interface StockFilter {
  warehouseId: string;
  productId?: string;
}

export const stockService = {
  async getByWarehouse(filter: StockFilter): Promise<StockListItem[]> {
    const q = new URLSearchParams({ warehouseId: filter.warehouseId });
    if (filter.productId) q.set('productId', filter.productId);

    const res = await api.get<ApiResponse<StockListItem[]>>(
      `/api/inventory/stock/stock-actual?${q.toString()}`
    );
    return res.data.responseObject ?? [];
  },
};
