import { apiGet } from "../../../lib/apiEnvelope";

export type CurrentStockDto = {
  id: string;
  itemId: string;
  warehouseId: string;
  quantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  totalStockValue: number;
  averageCost: number;
  lastUpdatedAt: string;
};

export type StockMovementDto = {
  id: string;
  itemId: string;
  warehouseId: string;
  movementType: number;
  movementTypeName: string;
  quantity: number;
  uomCode: string;
  previousQuantity: number;
  resultQuantity: number;
  sequenceNumber: number;
  unitCost: number | null;
  totalCost: number | null;
  runningAverageCost: number;
  runningStockValue: number;
  effectiveDate: string;
  reference: string | null;
  sourceDocId: string | null;
  sourceDocType: string | null;
  createdBy: string;
  createdAt: string;
  createdByName: string | null;
};

export type AggregatedStockDto = {
  itemId: string;
  totalQuantity: number;
  totalStockValue: number;
  averageCost: number;
};

export type ItemWarehouseAvailabilityDto = {
  warehouseId: string;
  warehouseName: string;
  available: number;
  reserved: number;
  canSell: boolean;
};

export type StockReportStatus = "SinStock" | "Bajo" | "Disponible";

export type StockReportRowDto = {
  productId: string;
  sku: string;
  productName: string;
  warehouseId: string;
  warehouseName: string;
  quantity: number;
  availableQuantity: number;
  averageCost: number;
  stockValue: number;
  status: StockReportStatus;
};

const BASE = "/api/v1/inventory/stock";

export const stockService = {
  getStock: (itemId?: string, warehouseId?: string) => {
    const params = new URLSearchParams();
    if (itemId) params.set("itemId", itemId);
    if (warehouseId) params.set("warehouseId", warehouseId);
    return apiGet<CurrentStockDto[]>(`${BASE}?${params}`);
  },

  getMovements: (
    itemId: string,
    warehouseId: string,
    from?: string,
    to?: string,
  ) => {
    const params = new URLSearchParams({ itemId, warehouseId });
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    return apiGet<StockMovementDto[]>(`${BASE}/movements?${params}`);
  },

  getAggregated: (itemId: string) =>
    apiGet<AggregatedStockDto>(`${BASE}/aggregated/${itemId}`),

  getWarehouseAvailability: (itemId: string) =>
    apiGet<ItemWarehouseAvailabilityDto[]>(
      `${BASE}/items/${itemId}/warehouse-availability`,
    ),

  /** Reporte básico de stock actual por bodega. */
  getReport: (warehouseId?: string, search?: string) => {
    const params = new URLSearchParams();
    if (warehouseId) params.set("warehouseId", warehouseId);
    if (search?.trim()) params.set("search", search.trim());
    const qs = params.toString();
    const url = qs ? `${BASE}/report?${qs}` : `${BASE}/report`;
    return apiGet<StockReportRowDto[]>(url);
  },
};
