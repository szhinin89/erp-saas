import { apiGet, apiPost, apiPut } from "../../../lib/apiEnvelope";
import type {
  CreateInventoryAdjustmentReasonRequest,
  InventoryAdjustmentReasonDto,
  UpdateInventoryAdjustmentReasonRequest,
} from "../types";

const BASE = "/api/v1/inventory/adjustment-reasons";

/**
 * INVENTORY-ADJUSTMENTS-03 — catálogo administrable de motivos de ajuste (SSOT dinámico).
 * No existe borrado físico: `toggle` activa/desactiva (MasterEntity.Enable/Disable), igual que
 * `warehouseService.disable/enable`. Mismo wrapper de envelope que el resto del módulo.
 */
export const inventoryAdjustmentReasonsService = {
  list: (includeInactive = false) =>
    apiGet<InventoryAdjustmentReasonDto[]>(
      `${BASE}?includeInactive=${includeInactive ? "true" : "false"}`,
    ),

  create: (payload: CreateInventoryAdjustmentReasonRequest) =>
    apiPost<InventoryAdjustmentReasonDto>(BASE, payload),

  update: (id: string, payload: UpdateInventoryAdjustmentReasonRequest) =>
    apiPut<InventoryAdjustmentReasonDto>(`${BASE}/${id}`, payload),

  toggle: (id: string, activate: boolean) =>
    apiPost<InventoryAdjustmentReasonDto>(`${BASE}/${id}/toggle`, { activate }),
};
