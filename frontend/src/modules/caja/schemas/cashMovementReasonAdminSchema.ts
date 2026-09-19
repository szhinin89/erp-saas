import { z } from "zod";

/**
 * TREASURY-CASH-MOVEMENT-REASONS-ADMIN-03 — validación del formulario de administración de
 * CashMovementReason (nivel 1: Zod + RHF). `movementType` no se restringe aquí a un enum
 * cerrado: el <select> del formulario ya solo ofrece las opciones de
 * `MANUAL_CASH_MOVEMENT_TYPES` (única fuente SSOT, ver constants/cashMovementTypes.ts) — el
 * backend (FluentValidation) vuelve a validar todo, incluida la unicidad del `code` por
 * Tenant+Company, que un schema de cliente no puede conocer (llega por `applyServerErrors`).
 */
export const cashMovementReasonAdminSchema = z.object({
  code: z
    .string()
    .trim()
    .min(1, "El código es obligatorio.")
    .max(30, "El código no puede superar 30 caracteres."),
  name: z
    .string()
    .trim()
    .min(1, "El nombre es obligatorio.")
    .max(100, "El nombre no puede superar 100 caracteres."),
  movementType: z.string().min(1, "Seleccione el tipo de movimiento."),
  sortOrder: z.coerce.number().int().min(0).max(9999),
});

export type CashMovementReasonAdminFormValues = z.infer<
  typeof cashMovementReasonAdminSchema
>;

export const defaultCashMovementReasonAdminValues: CashMovementReasonAdminFormValues = {
  code: "",
  name: "",
  movementType: "",
  sortOrder: 0,
};
