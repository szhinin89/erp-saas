import { ZHBtn } from "../../../components/zh/ZHForm";
import type { SalesPageContext } from "../hooks/useSalesPage";

export interface EmitButtonProps {
  ctx: SalesPageContext;
}

// ── Emit Button with tooltip ────────────────────────────────────────────
// Único botón de acción del formulario de venta: "Nueva Venta → Emitir
// Factura → Modal de confirmación → Emisión → Pantalla de éxito" es el
// flujo completo visible al usuario. Este botón solo abre el modal
// (ctx.openIssueFlow) — toda la lógica de negocio vive en el hook.
// El atajo de teclado F8 dispara la misma acción (ver useSalesPage.ts).
export function EmitButton({ ctx }: EmitButtonProps) {
  const reasons: string[] = [];
  if (!ctx.formWatch.customerId.trim()) reasons.push("Seleccione un cliente");
  if (ctx.lines.length === 0) reasons.push("Agregue al menos un producto");
  if (ctx.hasCashSession === true && ctx.summary.total > 0 && !ctx.paymentOk)
    reasons.push("Registre formas de pago por el total de la factura");
  if (ctx.cashInsufficient)
    reasons.push("El monto recibido en efectivo es menor al total a cobrar");
  if (ctx.hasInsufficientStock)
    reasons.push("Hay una línea con cantidad mayor al stock disponible");

  return (
    <div className="sales-emit-wrap">
      <ZHBtn
        variant="cta"
        onClick={ctx.openIssueFlow}
        disabled={!ctx.canEmit}
        title={
          reasons.length > 0
            ? `No se puede emitir: ${reasons.join(", ")}`
            : undefined
        }
      >
        <span className="material-symbols-outlined zh-icon-lg">
          play_arrow
        </span>
        {ctx.isElectronic
          ? "Emitir Factura Electrónica (F8)"
          : "Emitir Factura (F8)"}
      </ZHBtn>
      {!ctx.canEmit && !ctx.fieldDisabled && reasons.length > 0 && (
        <div className="sf-save-tooltip">{reasons.join(" · ")}</div>
      )}
    </div>
  );
}
