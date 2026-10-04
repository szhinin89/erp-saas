// Tolerancias monetarias del módulo de ventas — parámetros globales del ERP.
// Un cambio aquí aplica a todos los puntos de validación del módulo.

/** Tolerancia para detectar si un pago individual excede el saldo disponible. */
export const PAYMENT_DETAIL_TOLERANCE = 0.01;

/** Tolerancia de redondeo al distribuir cuotas de crédito (suma de cuotas vs monto). */
export const INSTALLMENT_ROUNDING_TOLERANCE = 0.01;

// POS-COLLECTION-SSOT-01: la tolerancia de cuadre cobros-vs-total de la factura YA NO vive aquí
// (antes INVOICE_PAYMENT_TOLERANCE=0.02 / PAYMENT_EXCEEDS_TOLERANCE=0.01, distintas entre sí y de
// la del backend). Es la tolerancia de settlement de la empresa —
// CompanyPrecisionPolicy.SettlementToleranceAmount (getPrecisionPolicy()), la misma que usa
// AuthorizeSalesInvoiceHandler — consumida por utils/salesCollectionStatus.ts.
