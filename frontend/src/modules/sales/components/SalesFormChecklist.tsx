import type { SalesPageContext } from "../hooks/useSalesPage";
import { PAYMENT_EXCEEDS_TOLERANCE } from "../constants/tolerances";

export interface SalesFormChecklistProps {
  ctx: SalesPageContext;
}

// ── Form Readiness Checklist ─────────────────────────────────────────────
export function SalesFormChecklist({ ctx }: SalesFormChecklistProps) {
  const hasCustomer = !!ctx.formWatch.customerId.trim();
  const hasLines = ctx.lines.length > 0;
  const hasEmissionPoint = ctx.hasCashSession === true;
  const paid = ctx.paidTotal;
  const total = ctx.summary.total;
  const paymentOk = ctx.paymentOk;
  const paymentExceeds = paid > total + PAYMENT_EXCEEDS_TOLERANCE;

  const canSaveDraft = hasCustomer && hasLines;
  const canEmit =
    canSaveDraft &&
    hasEmissionPoint &&
    paymentOk &&
    !ctx.cashInsufficient &&
    !ctx.hasInsufficientStock;

  const nextStep = !hasCustomer
    ? "Seleccione un cliente para comenzar."
    : !hasLines
      ? "Agregue productos a la factura."
      : ctx.hasInsufficientStock
        ? "Hay líneas con cantidad mayor al stock disponible — ajústelas antes de emitir."
        : !hasEmissionPoint
          ? ctx.cashSessionCheckError
            ? "No se pudo verificar la caja — reintente arriba antes de emitir."
            : "Debe abrir una caja antes de emitir."
          : paymentExceeds
            ? "El cobro excede el total — ajuste las formas de pago."
            : total > 0 && !paymentOk
              ? "Configure las formas de cobro para poder emitir."
              : ctx.cashInsufficient
                ? "El monto recibido en efectivo es menor al total a cobrar."
                : canSaveDraft && !ctx.editing
                  ? "Guarde el borrador primero. Luego podrá emitir la factura."
                  : canEmit && ctx.editing
                    ? `Listo para emitir ${ctx.isElectronic ? "(electrónica)" : "(física)"}.`
                    : null;

  // SALES-POS-CHECKLIST-COMPACT-01: se retiró la lista completa de requisitos
  // (Cliente seleccionado / Productos agregados / Caja abierta / Formas de cobro) — el botón
  // Emitir (EmitButton.tsx) ya explica el motivo puntual por tooltip cuando está deshabilitado,
  // así que repetir cada requisito acá era redundante y ocupaba altura del sidebar sin aportar
  // información nueva. Queda solo un bloque de una línea: "Siguiente paso" (mismo mensaje
  // jerárquico `nextStep` de siempre, sin cambios) mientras falte algo, o "Listo para emitir"
  // una vez que `canEmit` es true — el cálculo de `canEmit`/`nextStep` no cambió.
  return canEmit ? (
    <div className="sf-next-step sf-next-step--ready">
      <span className="material-symbols-outlined sf-next-step__icon">
        check_circle
      </span>
      <div>
        <div className="sf-next-step__title">Listo para emitir</div>
        <div>La factura cumple los requisitos para emitir.</div>
      </div>
    </div>
  ) : (
    <div className="sf-next-step">
      <span className="material-symbols-outlined sf-next-step__icon">
        arrow_forward
      </span>
      <div>
        <div className="sf-next-step__title">Siguiente paso</div>
        {/* Fallback defensivo: en la cadena de mensajes actual (sin cambios) hay un caso límite
            teórico — total 0 con todo lo demás en regla — donde `nextStep` puede resolver null.
            Antes ese caso ocultaba todo el bloque en silencio; ahora, al mostrarse siempre el
            bloque "Siguiente paso" cuando no está listo, se cubre con un mensaje genérico en vez
            de dejarlo vacío. */}
        <div>{nextStep ?? "Complete los datos requeridos para emitir."}</div>
      </div>
    </div>
  );
}
