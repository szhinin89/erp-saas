import { getDecimalConfig } from "../../../lib/config/decimal.config";
import type { SalesPageContext } from "../hooks/useSalesPage";

/** Saldo pendiente de cobro excluyendo los pagos ya asignados a una forma de pago específica —
 * único punto de este cálculo (redondeo a la precisión configurada), usado tanto para el
 * disponible mostrado en PaymentDetailModal como para precargar el monto de un nuevo pago en
 * la grilla de formas de cobro. Puede devolver negativo (ya se cobró de más con otras formas);
 * cada llamador decide si clamplear a 0 según su propio uso. */
export function remainingToCollect(
  ctx: SalesPageContext,
  excludePaymentMethodId: string,
): number {
  const factor = 10 ** getDecimalConfig().totalAmount;
  const othersTotal = ctx.payments
    .filter((p) => p.paymentMethodId !== excludePaymentMethodId)
    .reduce((s, p) => s + (p.amount || 0), 0);
  return Math.round((ctx.summary.total - othersTotal) * factor) / factor;
}
