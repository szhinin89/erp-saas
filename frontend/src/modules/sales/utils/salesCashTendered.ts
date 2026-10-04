import type { SalesInvoicePaymentDto } from "../api/salesService";

/**
 * POS-CASH-TENDERED-01 — "Recibido" / "Vuelto" de una factura YA persistida, leídos de sus pagos
 * (SalesInvoicePayment.TenderedAmount / ChangeAmount del backend). Misma regla que la tirilla
 * (SalesReceiptPrintPayloadMapper.ResolveCashTendered): suma de los pagos con efectivo entregado;
 * null si ninguno lo registró (venta sin efectivo o anterior a este dato). Nunca usa estado del POS.
 */
export function persistedCashTendered(
  payments: readonly Pick<SalesInvoicePaymentDto, "tenderedAmount" | "changeAmount">[] | undefined,
): { cashReceived: number; cashChange: number } | null {
  const tendered = (payments ?? []).filter((p) => p.tenderedAmount != null);
  if (tendered.length === 0) return null;
  return {
    cashReceived: tendered.reduce((s, p) => s + (p.tenderedAmount ?? 0), 0),
    cashChange: tendered.reduce((s, p) => s + (p.changeAmount ?? 0), 0),
  };
}

/**
 * Efectivo entregado a enviar en el pago en efectivo al persistir la venta. Solo cuando el cajero
 * lo ingresó y cubre lo aplicado (el backend rechaza un entregado menor). Dentro de la tolerancia
 * de settlement por debajo de lo aplicado (estado "Pago exacto" con 1 centavo menos) no se envía:
 * registrar un monto distinto al realmente entregado falsearía el dato.
 */
export function tenderedForPayment(
  isCashPayment: boolean,
  appliedAmount: number,
  cashReceived: number | null,
): number | null {
  if (!isCashPayment || cashReceived === null) return null;
  return cashReceived >= appliedAmount ? cashReceived : null;
}
