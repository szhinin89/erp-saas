import type { SalesPageContext } from "../hooks/useSalesPage";

export type SalesConfigStatusLevel = "ready" | "review" | "incomplete";

export interface SalesConfigStatus {
  level: SalesConfigStatusLevel;
  /** Bloquea emitir — se resuelve solo en el modal. */
  missing: string[];
  /** No bloquea capturar productos, pero conviene revisar (fallback en uso). */
  warnings: string[];
}

/**
 * SALES-POS-EMISSION-PANEL-SIMPLIFICATION-01: única fuente de verdad del estado visual de
 * "Configuración de venta" — no crea ni duplica ningún dato del form, solo interpreta los mismos
 * campos que useSalesPage ya expone (ctx.hasCashSession/formWatch/myCashSession/lines/payments).
 * "incomplete" = bloquea emitir (mismos requisitos que ctx.canEmit exige para la config, sin
 * repetir cliente/stock/cobro — esos ya tienen su propio aviso en SalesFormChecklist); "review" =
 * algo está usando un fallback (no bloquea capturar productos); "ready" = todo resuelto.
 * Extraído a un archivo aparte (sin JSX) para que SalesEmissionConfigSection.tsx solo exporte
 * componentes React (react-refresh/only-export-components).
 */
export function computeSalesConfigStatus(
  ctx: SalesPageContext,
): SalesConfigStatus {
  const missing: string[] = [];
  const warnings: string[] = [];

  if (!ctx.formWatch.customerId?.trim()) missing.push("Cliente");
  if (ctx.hasCashSession !== true) {
    missing.push("Caja abierta (Sucursal / Caja / Punto de emisión)");
  }

  const docTypeCode = ctx.readOnly
    ? ctx.editing?.docTypeCode
    : ctx.formWatch.docTypeCode;
  if (!docTypeCode) missing.push("Tipo de documento");

  const emissionType = ctx.myCashSession?.emissionType ?? ctx.editing?.emissionType;
  if (!emissionType) missing.push("Tipo de emisión");

  const hasStockLines = ctx.lines.some((l) => l._tracksStock);
  if (hasStockLines && !ctx.selectedWarehouseId) missing.push("Bodega");

  const defaultSriPaymentCode = ctx.readOnly
    ? ctx.editing?.sriPaymentMethodCode
    : ctx.formWatch.sriPaymentMethodCode;
  const paymentsInUse = ctx.payments.filter((p) => p.amount > 0);
  const anyUnmappedPayment = paymentsInUse.some((p) => {
    const pm = ctx.paymentMethods.find((m) => m.id === p.paymentMethodId);
    return !pm?.sriPaymentMethodCode;
  });

  if (!defaultSriPaymentCode) {
    // Sin default de empresa: solo bloquea si además hay un cobro en uso sin mapeo propio —
    // si todas las formas de cobro usadas ya tienen su propio código SRI, el default nunca
    // llega a necesitarse.
    if (anyUnmappedPayment) missing.push("Forma de pago SRI por defecto");
    else warnings.push("No hay Forma Pago SRI por defecto configurada.");
  } else if (anyUnmappedPayment) {
    warnings.push(
      "Alguna forma de cobro no tiene mapeo SRI propio y usa la Forma Pago SRI por defecto.",
    );
  }

  const level: SalesConfigStatusLevel =
    missing.length > 0 ? "incomplete" : warnings.length > 0 ? "review" : "ready";

  return { level, missing, warnings };
}
