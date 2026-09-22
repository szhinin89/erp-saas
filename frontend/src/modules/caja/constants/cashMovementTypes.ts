// TREASURY-CASH-MANUAL-MOVEMENTS-01 — única fuente de valores/orden para CashMovementType
// (backend: ERP.Domain.Modules.Caja.Enums.CashMovementType). Antes existían dos mapas
// hardcodeados independientes (uno para el <select> de "Registrar movimiento", otro para la
// columna Tipo del historial de Movimientos) que además omitía "SaleRefund" — un reembolso de
// devolución se veía como el string crudo "SaleRefund" en pantalla. Nunca dupliques este mapeo en
// otro archivo.
//
// TREASURY-CASH-ARCHITECTURE-I18N-AUDIT-04 — los LABELS visibles ya no viven hardcodeados aquí:
// cada tipo declara su labelKey + fallbackLabel; los diccionarios (es/en) contienen todas
// las etiquetas, y fallbackLabel es solo la red de seguridad estándar del proyecto (t(key,
// fallback)) si algún locale llegara a perder la key — nunca la fuente real de la traducción.
// La resolución siempre usa useI18n().t y los valores internos (`value`) permanecen estables.

export interface CashMovementTypeInfo {
  value: string;
  labelKey: string;
  fallbackLabel: string;
  /**
   * true si el cajero puede elegirlo en "Registrar movimiento manual de efectivo" — false para
   * los tipos de sistema (Opening/SaleIncome/SaleRefund), que RecordCashMovementHandler rechaza
   * explícitamente si se intenta crearlos por esa vía (solo los crean CashSession.Open/
   * SalesInvoiceAuthorizedHandler/SalesReturnRefundHandler a partir de una operación real).
   */
  manualEntry: boolean;
}

export const CASH_MOVEMENT_TYPES: readonly CashMovementTypeInfo[] = [
  { value: "Opening", labelKey: "caja.movementType.opening", fallbackLabel: "Apertura", manualEntry: false },
  { value: "SaleIncome", labelKey: "caja.movementType.saleIncome", fallbackLabel: "Venta", manualEntry: false },
  { value: "ManualIncome", labelKey: "caja.movementType.manualIncome", fallbackLabel: "Ingreso manual", manualEntry: true },
  { value: "ManualExpense", labelKey: "caja.movementType.manualExpense", fallbackLabel: "Egreso manual", manualEntry: true },
  { value: "Withdrawal", labelKey: "caja.movementType.withdrawal", fallbackLabel: "Retiro de efectivo", manualEntry: true },
  { value: "SaleRefund", labelKey: "caja.movementType.saleRefund", fallbackLabel: "Reembolso de devolución", manualEntry: false },
];

const TYPE_BY_VALUE: ReadonlyMap<string, CashMovementTypeInfo> = new Map(
  CASH_MOVEMENT_TYPES.map((t) => [t.value, t]),
);

/** Firma mínima que necesitamos de `useI18n().t` — evita acoplar este módulo al contexto de React. */
type TranslateFn = (key: string, fallback?: string) => string;

/** Etiqueta traducida para cualquier valor de CashMovementType — usado tanto por el historial de
 * Movimientos como por cualquier otro lugar que necesite mostrar el tipo. Nunca reimplementar
 * este mapeo ad hoc en un componente; siempre pasar el `t` del `useI18n()` del componente llamante. */
export function cashMovementTypeLabel(t: TranslateFn, value: string): string {
  const info = TYPE_BY_VALUE.get(value);
  if (!info) return value;
  return t(info.labelKey, info.fallbackLabel);
}

/** Únicos tipos que el cajero puede elegir en "Registrar movimiento manual de efectivo", con su
 * etiqueta ya traducida — listos para poblar un <select>. */
export function manualCashMovementTypeOptions(
  t: TranslateFn,
): { value: string; label: string }[] {
  return CASH_MOVEMENT_TYPES.filter((type) => type.manualEntry).map((type) => ({
    value: type.value,
    label: t(type.labelKey, type.fallbackLabel),
  }));
}
