import { apiPost } from "../../../lib/apiEnvelope";

// ── DTOs (espejo exacto de StockTransferDto / StockTransferLineDto — backend,
// ver StockOperationDtos.cs) — el backend no resuelve nombres de bodega/producto aquí (a
// diferencia de StockAdjustmentDto), mismo patrón: el frontend ya los conoce por los
// selectores/picker usados para armar la línea, no hace falta que el DTO los repita. ──

export interface StockTransferLineDto {
  id: string;
  productId: string;
  quantity: number;
  description: string;
}

export interface StockTransferDto {
  id: string;
  transferNumber: string;
  sourceWarehouseId: string;
  targetWarehouseId: string;
  transferDate: string;
  status: string;
  reason: string | null;
  notes: string | null;
  confirmedAt: string | null;
  lines: StockTransferLineDto[];
}

export interface CreateStockTransferLineInput {
  productId: string;
  quantity: number;
  description: string;
}

export interface CreateStockTransferPayload {
  sourceWarehouseId: string;
  targetWarehouseId: string;
  reason?: string | null;
  notes?: string | null;
  lines: CreateStockTransferLineInput[];
}

const BASE = "/api/v1/inventory/stock/transfers";

/**
 * P1-INVENTORY-WAREHOUSE-TRANSFER-UI-01 — el backend solo expone crear (Draft) y confirmar;
 * no hay endpoint de listado/detalle/cancelación (ver StockController). La UI opera en esas
 * dos operaciones únicamente — no se inventa un estado ni una acción que el backend no soporta.
 */
export const stockTransferService = {
  create: (payload: CreateStockTransferPayload) =>
    apiPost<StockTransferDto>(BASE, payload),

  confirm: (id: string) => apiPost<StockTransferDto>(`${BASE}/${id}/confirm`, {}),
};
