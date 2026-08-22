import { z } from "zod";

/**
 * INVENTORY-ADJUSTMENTS-03 — validación de la cabecera del ajuste (nivel 1: Zod + RHF).
 * Nivel 2 es FluentValidation en el backend, que sigue siendo la autoridad: aquí solo se evitan
 * envíos obviamente inválidos.
 *
 * `notes` NO se declara requerido aquí porque su obligatoriedad depende del motivo seleccionado
 * (`InventoryAdjustmentReason.RequiresNotes`), que es un dato dinámico del catálogo — un schema
 * estático no puede expresarlo sin duplicar el catálogo en el frontend. Esa regla se aplica en el
 * hook contra el motivo realmente elegido, con `setError("notes", ...)`.
 */
export const stockAdjustmentHeaderSchema = z.object({
  movementType: z.enum(["Ingreso", "Egreso"]),
  warehouseId: z.string().min(1, "Seleccione una bodega."),
  reasonId: z.string().min(1, "Seleccione un motivo de ajuste."),
  notes: z.string().max(1000).optional(),
});

export type StockAdjustmentHeaderValues = z.infer<
  typeof stockAdjustmentHeaderSchema
>;

export const defaultStockAdjustmentHeaderValues: StockAdjustmentHeaderValues = {
  movementType: "Ingreso",
  warehouseId: "",
  reasonId: "",
  notes: "",
};
