// Tolerancias monetarias del módulo de ventas — parámetros globales del ERP.
// Un cambio aquí aplica a todos los puntos de validación del módulo.

/** Tolerancia para detectar si un pago individual excede el saldo disponible. */
export const PAYMENT_DETAIL_TOLERANCE = 0.01;

/** Tolerancia de redondeo al distribuir cuotas de crédito (suma de cuotas vs monto). */
export const INSTALLMENT_ROUNDING_TOLERANCE = 0.01;

/** Tolerancia acumulada para validar que los cobros suman el total de la factura.
 *  Ligeramente mayor que PAYMENT_DETAIL_TOLERANCE para absorber errores de redondeo
 *  cuando hay múltiples formas de pago con decimales. */
export const INVOICE_PAYMENT_TOLERANCE = 0.02;

/** Tolerancia para determinar si el cobro excede el total de la factura. */
export const PAYMENT_EXCEEDS_TOLERANCE = 0.01;
