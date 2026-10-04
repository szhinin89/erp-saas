import { ZHBtn } from "../../../components/zh/ZHForm";
import type { SalesPageContext } from "../hooks/useSalesPage";

export interface EmitButtonProps {
  ctx: SalesPageContext;
}

// ── Emit Button ─────────────────────────────────────────────────────────
// Único botón de acción del formulario de venta: "Nueva Venta → Emitir
// Factura → Modal de confirmación → Emisión → Pantalla de éxito" es el
// flujo completo visible al usuario. Este botón solo abre el modal
// (ctx.openIssueFlow) — toda la lógica de negocio vive en el hook.
// El atajo de teclado F8 dispara la misma acción (ver useSalesPage.ts).
// POS-CANEMIT-SSOT-01: habilitación y motivo salen de ctx.canEmit / ctx.emitBlockers — el botón
// no recalcula reglas ni repite el motivo como texto visible (ya lo muestra "Siguiente paso" y,
// para el cobro, el estado del resumen); el motivo principal queda solo como `title` accesible.
export function EmitButton({ ctx }: EmitButtonProps) {
  const primaryBlocker = ctx.emitBlockers[0];

  return (
    <div className="sales-emit-wrap">
      <ZHBtn
        variant="cta"
        onClick={ctx.openIssueFlow}
        disabled={!ctx.canEmit && !ctx.lineIssues?.length}
        aria-disabled={!ctx.canEmit}
        title={
          !ctx.canEmit && primaryBlocker
            ? `No se puede emitir: ${primaryBlocker.message}`
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
    </div>
  );
}
