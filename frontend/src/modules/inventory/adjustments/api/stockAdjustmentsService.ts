import { apiGet, apiPost, apiPut } from "../../../lib/apiEnvelope";
import type {
  CreateStockAdjustmentRequest,
  StockAdjustmentDto,
  StockAdjustmentListFilters,
  StockAdjustmentPagedResult,
  UpdateStockAdjustmentRequest,
} from "../types";

const BASE = "/api/v1/inventory/stock/adjustments";

/**
 * INVENTORY-ADJUSTMENTS-03 — superficie HTTP de ajustes de inventario. Usa el mismo wrapper de
 * envelope (`apiGet`/`apiPost`/`apiPut`) que `warehouseService`/`stockService`; nunca `fetch`
 * ni axios directo. El backend es la autoridad de todas las reglas de negocio (borrador,
 * ejecución, anulación, stock suficiente, motivo activo) — este archivo solo transporta.
 */
function listQuery(filters: StockAdjustmentListFilters): string {
  const q = new URLSearchParams();
  if (filters.warehouseId) q.set("warehouseId", filters.warehouseId);
  if (filters.status) q.set("status", filters.status);
  if (filters.reasonId) q.set("reasonId", filters.reasonId);
  if (filters.movementType) q.set("movementType", filters.movementType);
  if (filters.startDate) q.set("startDate", filters.startDate);
  if (filters.endDate) q.set("endDate", filters.endDate);
  q.set("pageNumber", String(filters.pageNumber ?? 1));
  q.set("pageSize", String(filters.pageSize ?? 20));
  return `?${q.toString()}`;
}

export const stockAdjustmentsService = {
  list: (filters: StockAdjustmentListFilters = {}) =>
    apiGet<StockAdjustmentPagedResult>(`${BASE}${listQuery(filters)}`),

  getById: (id: string) => apiGet<StockAdjustmentDto>(`${BASE}/${id}`),

  create: (payload: CreateStockAdjustmentRequest) =>
    apiPost<StockAdjustmentDto>(BASE, payload),

  update: (id: string, payload: UpdateStockAdjustmentRequest) =>
    apiPut<StockAdjustmentDto>(`${BASE}/${id}`, payload),

  /** Draft → Executed. Sin body: el backend resuelve costo/kardex al ejecutar. */
  execute: (id: string) => apiPost<StockAdjustmentDto>(`${BASE}/${id}/execute`, {}),

  /** Executed → Cancelled. `reason` es obligatorio en el contrato del backend. */
  cancel: (id: string, reason: string) =>
    apiPost<StockAdjustmentDto>(`${BASE}/${id}/cancel`, { reason }),
};
