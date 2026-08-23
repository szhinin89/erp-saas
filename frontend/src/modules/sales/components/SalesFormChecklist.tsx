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

  type ItemStatus = "ok" | "missing" | "error";
  const item = (label: string, status: ItemStatus) => (
    <div className="sf-checklist__item">
      <span
        className={`material-symbols-outlined sf-checklist__icon sf-checklist__icon--${status}`}
      >
        {status === "ok"
          ? "check_circle"
          : status === "error"
            ? "error"
            : "radio_button_unchecked"}
      </span>
      <span className={`sf-checklist__label--${status}`}>{label}</span>
    </div>
  );

  return (
    <>
      <div className="sf-checklist">
        <div className="sf-checklist__title zh-section-title">
          Estado del formulario
        </div>
        {item("Cliente seleccionado", hasCustomer ? "ok" : "missing")}
        {item("Productos agregados", hasLines ? "ok" : "missing")}
        {ctx.hasInsufficientStock &&
          item("Cantidad supera el stock disponible en una línea", "error")}
        {item(
          ctx.cashSessionCheckError ? "Caja abierta (sin verificar)" : "Caja abierta",
          ctx.hasCashSession === true
            ? "ok"
            : ctx.hasCashSession === false || ctx.cashSessionCheckError
              ? "error"
              : "missing",
        )}
        {item(
          paymentExceeds ? "Cobro excede el total" : "Formas de cobro",
          paymentOk
            ? "ok"
            : paymentExceeds
              ? "error"
              : paid > 0
                ? "missing"
                : "missing",
        )}
        {ctx.cashDue > 0 &&
          item(
            "Monto recibido en efectivo",
            ctx.cashInsufficient ? "error" : "ok",
          )}
      </div>
      {nextStep && (
        <div className="sf-next-step">
          <span className="material-symbols-outlined sf-next-step__icon">
            arrow_forward
          </span>
          {nextStep}
        </div>
      )}
    </>
  );
}
