// TREASURY-CASH-MANUAL-MOVEMENTS-01 — única fuente de labels para CashMovementType (backend:
// ERP.Domain.Modules.Caja.Enums.CashMovementType). Antes existían dos mapas hardcodeados
// independientes (uno para el <select> de "Registrar movimiento", otro para la columna Tipo del
// historial de Movimientos) que además omitía "SaleRefund" — un reembolso de devolución se veía
// como el string crudo "SaleRefund" en pantalla. Nunca dupliques este mapeo en otro archivo.

export interface CashMovementTypeInfo {
  value: string;
  label: string;
  /**
   * true si el cajero puede elegirlo en "Registrar movimiento manual de efectivo" — false para
   * los tipos de sistema (Opening/SaleIncome/SaleRefund), que RecordCashMovementHandler rechaza
   * explícitamente si se intenta crearlos por esa vía (solo los crean CashSession.Open/
   * SalesInvoiceAuthorizedHandler/SalesReturnRefundHandler a partir de una operación real).
   */
  manualEntry: boolean;
}

export const CASH_MOVEMENT_TYPES: readonly CashMovementTypeInfo[] = [
  { value: "Opening", label: "Apertura", manualEntry: false },
  { value: "SaleIncome", label: "Venta", manualEntry: false },
  { value: "ManualIncome", label: "Ingreso manual", manualEntry: true },
  { value: "ManualExpense", label: "Egreso manual", manualEntry: true },
  { value: "Withdrawal", label: "Retiro", manualEntry: true },
  { value: "SaleRefund", label: "Reembolso de devolución", manualEntry: false },
];

const LABEL_BY_VALUE: ReadonlyMap<string, string> = new Map(
  CASH_MOVEMENT_TYPES.map((t) => [t.value, t.label]),
);

/** Etiqueta en español para cualquier valor de CashMovementType — usado tanto por el historial de
 * Movimientos como por cualquier otro lugar que necesite mostrar el tipo. Nunca reimplementar
 * este mapeo ad hoc en un componente. */
export function cashMovementTypeLabel(value: string): string {
  return LABEL_BY_VALUE.get(value) ?? value;
}

/** Únicos tipos que el cajero puede elegir en "Registrar movimiento manual de efectivo". */
export const MANUAL_CASH_MOVEMENT_TYPES: readonly CashMovementTypeInfo[] =
  CASH_MOVEMENT_TYPES.filter((t) => t.manualEntry);
