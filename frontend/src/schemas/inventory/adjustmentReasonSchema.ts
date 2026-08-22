import { z } from "zod";

/**
 * INVENTORY-ADJUSTMENTS-03 — validación del motivo de ajuste (nivel 1: Zod + RHF).
 * El backend (FluentValidation) vuelve a validar todo, incluida la unicidad del `code`, que un
 * schema de cliente no puede conocer — ese error llega por `applyServerErrors`.
 */
export const adjustmentReasonSchema = z.object({
  code: z
    .string()
    .trim()
    .min(1, "El código es obligatorio.")
    .max(20, "El código no puede superar 20 caracteres."),
  name: z
    .string()
    .trim()
    .min(1, "El nombre es obligatorio.")
    .max(120, "El nombre no puede superar 120 caracteres."),
  allowedMovementType: z.enum(["Ingreso", "Egreso", "Ambos"]),
  requiresNotes: z.boolean(),
  sortOrder: z.coerce.number().int().min(0).max(9999),
});

export type AdjustmentReasonFormValues = z.infer<typeof adjustmentReasonSchema>;

export const defaultAdjustmentReasonValues: AdjustmentReasonFormValues = {
  code: "",
  name: "",
  allowedMovementType: "Ambos",
  requiresNotes: false,
  sortOrder: 0,
};
