import { getPrecisionPolicy } from "../../../lib/config/precisionPolicy.config";
import type { SalesPageContext } from "../hooks/useSalesPage";

/** Saldo pendiente de cobro excluyendo los pagos ya asignados a una forma de pago específica —
 * único punto de este cálculo (redondeo a la precisión configurada), usado tanto para el
 * disponible mostrado en PaymentDetailModal como para precargar el monto de un nuevo pago en
 * la grilla de formas de cobro. Parte de `ctx.paymentsForAdditionalMethod` (POS-CASH-ONLY-
 * FOLLOWS-TOTAL-01): si hoy el único cobro es Efectivo, lo aplicado en Efectivo ya se considera
 * fijado a lo recibido, así que la nueva forma de cobro cubre solo el resto. Puede devolver
 * negativo (ya se cobró de más con otras formas); cada llamador decide si clamplear a 0. */
export function remainingToCollect(
  ctx: SalesPageContext,
  excludePaymentMethodId: string,
): number {
  const factor = 10 ** getPrecisionPolicy().moneyDecimals;
  const othersTotal = ctx
    .paymentsForAdditionalMethod(excludePaymentMethodId)
    .filter((p) => p.paymentMethodId !== excludePaymentMethodId)
    .reduce((s, p) => s + (p.amount || 0), 0);
  return Math.round((ctx.summary.total - othersTotal) * factor) / factor;
}
