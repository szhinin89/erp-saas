/**
 * POS-COLLECTION-SSOT-01 — única fuente de verdad del estado del cobro en Ventas/POS.
 *
 * Una sola función pura decide, a partir de los cobros aplicados y del efectivo recibido
 * físicamente, el estado del cobro (`state`), el único mensaje que ve el cajero y si el cobro
 * permite emitir (`isComplete`). La consumen el resumen inline de Formas de Cobro, el bloqueo de
 * Emitir/F8 (vía `canEmit` del hook), el checklist y el modal de resultado — ninguno recalcula
 * reglas por su cuenta.
 *
 * Distingue dos montos de efectivo:
 * - **aplicado** (`cashApplied`): lo que la factura registra como cobrado en Efectivo
 *   (payments[], lo que viaja al backend);
 * - **recibido** (`cashReceived`): el dinero que el cliente entrega físicamente en caja — solo
 *   sirve para Falta / Pago exacto / Vuelto; nunca altera el total ni lo aplicado.
 *
 * La tolerancia es SIEMPRE la de la política de settlement real de la empresa
 * (`CompanyPrecisionPolicy.SettlementToleranceAmount`, la misma que usa
 * `AuthorizeSalesInvoiceHandler` → `SalesInvoice.Authorize`) — el llamador la inyecta; aquí no
 * existe ninguna tolerancia propia de UI.
 */

export type SalesCollectionState =
  /** Sin total a cobrar (sin productos). */
  | "noTotal"
  /** Hay total pero ninguna forma de cobro registrada. */
  | "empty"
  /** Los cobros aplicados no cubren el total. */
  | "pending"
  /** Los cobros aplicados superan el total. */
  | "exceeds"
  /** Cobro en efectivo aplicado, pero aún no se ingresó el efectivo recibido (estado neutro). */
  | "cashPending"
  /** Efectivo recibido menor al efectivo aplicado. */
  | "cashShort"
  /** Efectivo recibido igual al aplicado (dentro de la tolerancia). */
  | "exact"
  /** Efectivo recibido mayor al aplicado — hay vuelto. */
  | "change"
  /** Cobro completo sin efectivo (tarjeta, transferencia, crédito…). */
  | "complete";

export type SalesCollectionTone = "neutral" | "warning" | "error" | "success";

export interface SalesCollectionPaymentInput {
  paymentMethodId: string;
  amount: number;
}

export interface SalesCollectionInput {
  total: number;
  payments: readonly SalesCollectionPaymentInput[];
  /** true si la forma de cobro es Efectivo (dinero físico con vuelto). */
  isCashMethod: (paymentMethodId: string) => boolean;
  /** Efectivo recibido físicamente; null = el cajero aún no ingresó nada. */
  cashReceived: number | null;
  /** Tolerancia de settlement de la empresa (CompanyPrecisionPolicy). */
  tolerance: number;
  moneyDecimals: number;
}

export interface SalesCollectionStatus {
  state: SalesCollectionState;
  tone: SalesCollectionTone;
  /** Único texto principal del estado del cobro. */
  label: string;
  /** Monto asociado al label (Falta / Excede / Vuelto); null si no aplica. */
  amount: number | null;
  total: number;
  /** Suma de cobros aplicados a la factura. */
  appliedTotal: number;
  /** total − aplicado, redondeado a la escala de dinero (positivo = falta, negativo = excede). */
  appliedDiff: number;
  /** Los cobros aplicados cuadran con el total dentro de la tolerancia de settlement. */
  appliedOk: boolean;
  /** Efectivo aplicado a la factura (0 si no hay cobro en efectivo). */
  cashApplied: number;
  cashReceived: number | null;
  /** Vuelto a entregar (0 si no aplica). */
  cashChange: number;
  /** Efectivo que falta recibir (0 si no aplica). */
  cashShortfall: number;
  /** true si el único cobro es Efectivo — el monto aplicado sigue al total. */
  isCashOnly: boolean;
  /** El cobro permite emitir: aplicado cuadra y, si hay efectivo, el recibido lo cubre. */
  isComplete: boolean;
}

function roundTo(value: number, decimals: number): number {
  const factor = 10 ** decimals;
  return Math.round(value * factor) / factor;
}

/** Interpreta el texto del input de efectivo recibido: vacío / inválido / 0 → null (sin ingresar). */
export function parseCashReceivedInput(raw: string): number | null {
  const trimmed = raw.trim();
  if (trimmed === "") return null;
  const n = Number(trimmed);
  return Number.isFinite(n) && n > 0 ? n : null;
}

export function computeSalesCollectionStatus(
  input: SalesCollectionInput,
): SalesCollectionStatus {
  const { tolerance, moneyDecimals } = input;
  const total = roundTo(input.total, moneyDecimals);
  const inUse = input.payments.filter((p) => (Number(p.amount) || 0) > 0);
  const appliedTotal = roundTo(
    inUse.reduce((s, p) => s + (Number(p.amount) || 0), 0),
    moneyDecimals,
  );
  const appliedDiff = roundTo(total - appliedTotal, moneyDecimals);
  const appliedOk =
    total > 0 && appliedTotal > 0 && Math.abs(appliedDiff) <= tolerance;

  const cashEntries = inUse.filter((p) => input.isCashMethod(p.paymentMethodId));
  const cashApplied = roundTo(
    cashEntries.reduce((s, p) => s + (Number(p.amount) || 0), 0),
    moneyDecimals,
  );
  const isCashOnly = inUse.length === 1 && cashEntries.length === 1;
  const cashReceived =
    input.cashReceived === null ? null : roundTo(input.cashReceived, moneyDecimals);

  const base = {
    total,
    appliedTotal,
    appliedDiff,
    appliedOk,
    cashApplied,
    cashReceived,
    isCashOnly,
  };
  const result = (
    state: SalesCollectionState,
    tone: SalesCollectionTone,
    label: string,
    amount: number | null,
    extra: { cashChange?: number; cashShortfall?: number; isComplete?: boolean } = {},
  ): SalesCollectionStatus => ({
    ...base,
    state,
    tone,
    label,
    amount,
    cashChange: extra.cashChange ?? 0,
    cashShortfall: extra.cashShortfall ?? 0,
    isComplete: extra.isComplete ?? false,
  });

  if (total <= 0) return result("noTotal", "neutral", "Sin productos por cobrar", null);
  if (inUse.length === 0)
    return result("empty", "neutral", "Seleccione una forma de cobro", null);
  if (appliedDiff > tolerance)
    return result("pending", "warning", "Falta por cobrar", appliedDiff);
  if (appliedDiff < -tolerance)
    return result("exceeds", "error", "El cobro excede el total", -appliedDiff);

  if (cashApplied <= 0)
    return result("complete", "success", "Cobro completo", null, { isComplete: true });

  if (cashReceived === null)
    return result("cashPending", "neutral", "Esperando monto", null);

  const cashDiff = roundTo(cashReceived - cashApplied, moneyDecimals);
  if (cashDiff < -tolerance)
    return result("cashShort", "warning", "Falta por cobrar", -cashDiff, {
      cashShortfall: -cashDiff,
    });
  if (cashDiff <= tolerance)
    return result("exact", "success", "Pago exacto", null, { isComplete: true });
  return result("change", "success", "Vuelto", cashDiff, {
    cashChange: cashDiff,
    isComplete: true,
  });
}

type PaymentWithKey = SalesCollectionPaymentInput & { _key: number };

/**
 * POS-CASH-ONLY-FOLLOWS-TOTAL-01 — con un ÚNICO cobro y en Efectivo, lo aplicado es un derivado
 * del total: devuelve los pagos con ese monto alineado al total, o `null` si no hay nada que
 * cambiar (multipago, sin efectivo, ya alineado o total 0). Con 2+ formas de cobro NUNCA
 * redistribuye: los montos explícitos del cajero se respetan y la diferencia se muestra.
 */
export function syncCashOnlyAppliedAmount<T extends PaymentWithKey>(
  payments: readonly T[],
  isCashMethod: (paymentMethodId: string) => boolean,
  total: number,
): T[] | null {
  if (total <= 0) return null;
  const inUse = payments.filter((p) => (Number(p.amount) || 0) > 0);
  if (inUse.length !== 1 || !isCashMethod(inUse[0].paymentMethodId)) return null;
  const only = inUse[0];
  if (Math.abs((Number(only.amount) || 0) - total) < 1e-9) return null;
  return payments.map((p) => (p._key === only._key ? { ...p, amount: total } : p));
}

/**
 * POS-CASH-ONLY-FOLLOWS-TOTAL-01 — pagos base al sumar OTRA forma de cobro. Si hoy el único
 * cobro es Efectivo (aplicado = total, derivado), pasar a multipago fija lo aplicado en Efectivo
 * a lo realmente recibido (tope: el total) — o lo quita si aún no se ingresó nada — para que la
 * nueva forma de cobro cubra solo el resto. En multipago devuelve los pagos tal cual.
 */
export function basePaymentsForAdditionalMethod<T extends PaymentWithKey>(
  payments: readonly T[],
  newPaymentMethodId: string,
  isCashMethod: (paymentMethodId: string) => boolean,
  cashReceived: number | null,
  total: number,
): T[] {
  const inUse = payments.filter((p) => (Number(p.amount) || 0) > 0);
  const isCashOnly = inUse.length === 1 && isCashMethod(inUse[0].paymentMethodId);
  if (!isCashOnly || isCashMethod(newPaymentMethodId)) return [...payments];
  return payments.flatMap((p) => {
    if (!isCashMethod(p.paymentMethodId)) return [p];
    if (cashReceived === null) return [];
    return [{ ...p, amount: Math.min(cashReceived, total) }];
  });
}

const QUICK_TENDER_DENOMINATIONS = [1, 5, 10, 20, 50, 100] as const;

/**
 * POS-COLLECTION-INLINE-B-01 — montos rápidos de "Efectivo recibido": el siguiente valor redondo
 * que cubre lo aplicado en efectivo para cada denominación (1, 5, 10, 20, 50, 100), sin repetir,
 * los `max` más cercanos. Ej. 14.66 → 15 · 20 · 50. Solo sugiere: el cajero puede escribir otro.
 */
export function quickTenderAmounts(cashApplied: number, max = 3): number[] {
  if (cashApplied <= 0) return [];
  const values = new Set<number>();
  for (const d of QUICK_TENDER_DENOMINATIONS) values.add(Math.ceil(cashApplied / d - 1e-9) * d);
  return [...values].sort((a, b) => a - b).slice(0, max);
}
