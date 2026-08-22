/**
 * INVENTORY-ADJUSTMENTS-03 — espejo 1:1 de `StockAdjustmentDto` / `StockAdjustmentLineDto` y
 * de los comandos Create/Update (backend: StockOperationDtos.cs + StockController). Casing
 * camelCase, igual que el resto de servicios de inventario (`stockService.ts`).
 *
 * `movementType` y `status` son valores cerrados del dominio (StockAdjustment.MovementTypeIngreso
 * / MovementTypeEgreso; Draft/Executed/Cancelled) — no son catálogo administrable, por eso se
 * modelan como uniones de literales y no como SSOT dinámico.
 */

export type AdjustmentMovementType = "Ingreso" | "Egreso";

export type AdjustmentStatus = "Draft" | "Executed" | "Cancelled";

export interface StockAdjustmentLineDto {
  id: string;
  itemId: string;
  itemName: string;
  packagingLevelId: string | null;
  uomCode: string;
  baseUomCode: string;
  conversionFactor: number;
  quantity: number;
  quantityInBaseUom: number;
  /** Solo resuelto por el backend al Ejecutar (Egreso: desde RunningAverageCost). */
  unitCostBase: number | null;
  totalCost: number | null;
  currentStockBefore: number | null;
  currentStockAfter: number | null;
  lineNotes: string | null;
}

export interface StockAdjustmentDto {
  id: string;
  adjustmentNumber: string;
  warehouseId: string;
  warehouseName: string;
  movementType: AdjustmentMovementType;
  reasonId: string;
  reasonName: string | null;
  notes: string | null;
  adjustmentDate: string;
  status: AdjustmentStatus;
  executedAt: string | null;
  executedBy: string | null;
  cancelledAt: string | null;
  cancelledBy: string | null;
  cancelledReason: string | null;
  lines: StockAdjustmentLineDto[];
}

export interface CreateStockAdjustmentLineInput {
  itemId: string;
  itemName: string;
  packagingLevelId: string | null;
  quantity: number;
  /** Solo se envía en Ingreso — en Egreso el backend deriva el costo del promedio móvil. */
  unitCostBase: number | null;
  lineNotes: string | null;
}

/**
 * Nunca incluye companyId/tenantId/branchId: el backend los resuelve del contexto autenticado.
 * `warehouseId` es el único campo de ubicación del contrato (`CreateStockAdjustmentCommand`).
 */
export interface CreateStockAdjustmentRequest {
  warehouseId: string;
  warehouseName: string;
  movementType: AdjustmentMovementType;
  reasonId: string;
  notes: string | null;
  lines: CreateStockAdjustmentLineInput[];
}

export type UpdateStockAdjustmentRequest = CreateStockAdjustmentRequest & {
  id: string;
};

export interface StockAdjustmentListFilters {
  warehouseId?: string;
  status?: string;
  reasonId?: string;
  movementType?: string;
  startDate?: string;
  endDate?: string;
  pageNumber?: number;
  pageSize?: number;
}

/** Espejo de `PagedResult<T>` (ERP.Application/Modules/Common/PagedResult.cs). */
export interface StockAdjustmentPagedResult {
  items: StockAdjustmentDto[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}
