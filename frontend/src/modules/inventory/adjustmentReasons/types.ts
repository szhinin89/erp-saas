/**
 * INVENTORY-ADJUSTMENTS-03 — espejo 1:1 de `InventoryAdjustmentReasonDto` y sus comandos
 * (backend: ERP.Application/Modules/Inventory/Stock/DTOs/StockOperationDtos.cs +
 * InventoryAdjustmentReasonsController). Casing camelCase: el envelope de la API serializa
 * en camelCase — verificado contra `warehouseService.ts` / `stockService.ts`, que consumen
 * los mismos controladores con la misma configuración de JSON.
 *
 * SSOT dinámico (regla global): los motivos de ajuste NUNCA se declaran como enum/array
 * estático en el frontend — solo `allowedMovementType` (valor cerrado del dominio, no
 * administrable) se modela como unión de literales.
 */

/** Valores del dominio: `InventoryAdjustmentReason.AllowedMovementType`. */
export type ReasonAllowedMovementType = "Ingreso" | "Egreso" | "Ambos";

export interface InventoryAdjustmentReasonDto {
  id: string;
  code: string;
  name: string;
  allowedMovementType: ReasonAllowedMovementType;
  requiresNotes: boolean;
  isActive: boolean;
  sortOrder: number;
  /**
   * ACCOUNTING-12A — cuenta contable del motivo para la empresa ACTIVA (configuración por empresa,
   * el motivo no guarda cuenta); null = sin configurar (la contabilización queda fallida).
   */
  accountingAccountId: string | null;
}

/**
 * El motivo pertenece a la empresa activa: el backend la resuelve del contexto autenticado
 * (regla global: nunca se envía tenant/company/branch como autoridad desde el body).
 */
export interface CreateInventoryAdjustmentReasonRequest {
  code: string;
  name: string;
  allowedMovementType: ReasonAllowedMovementType;
  requiresNotes: boolean;
  sortOrder: number;
}

/** `Code` es inmutable tras la creación — el comando de update no lo incluye. */
export interface UpdateInventoryAdjustmentReasonRequest {
  id: string;
  name: string;
  allowedMovementType: ReasonAllowedMovementType;
  requiresNotes: boolean;
  sortOrder: number;
}

/** ACCOUNTING-12A — PUT {id}/account: la empresa sale del contexto autenticado, nunca del body. */
export interface SetInventoryAdjustmentReasonAccountRequest {
  accountingAccountId: string;
}
