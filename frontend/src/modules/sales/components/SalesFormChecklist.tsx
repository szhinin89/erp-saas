import type { SalesPageContext } from "../hooks/useSalesPage";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";

export interface SalesFormChecklistProps {
  ctx: SalesPageContext;
  /** POS-OPERATIONAL-HEADER-01: una sola línea para la barra inferior del POS, junto a Emitir. */
  compact?: boolean;
}

// ── Form Readiness Checklist ─────────────────────────────────────────────
// POS-CANEMIT-SSOT-01: ya no calcula su propio `canEmit` — muestra "Listo para emitir" EXACTAMENTE
// cuando ctx.canEmit es true, y si no, el primer bloqueante de ctx.emitBlockers (misma lista,
// mismo orden que gobierna el botón Emitir y F8). Antes tenía su propia versión de las reglas
// (sin Consumidor Final ni configuración) y podía decir "Listo" con Emitir deshabilitado.
export function SalesFormChecklist({ ctx, compact = false }: SalesFormChecklistProps) {
  if (compact) {
    const message = ctx.canEmit
      ? "Listo para emitir"
      : (ctx.emitBlockers[0]?.message ?? "Complete los datos requeridos para emitir.");
    return (
      <span
        className={`sf-next-step-inline${ctx.canEmit ? " sf-next-step-inline--ready" : ""}`}
        role="status"
      >
        <span className="material-symbols-outlined sf-next-step-inline__icon">
          {ctx.canEmit ? "check_circle" : "arrow_forward"}
        </span>
        <span className="sf-next-step-inline__text">{message}</span>
      </span>
    );
  }
  if (ctx.canEmit) {
    return (
      <div className="sf-next-step sf-next-step--ready">
        <span className="material-symbols-outlined sf-next-step__icon">
          check_circle
        </span>
        <div>
          <div className="sf-next-step__title">
            Listo para emitir
            <ZHFieldHelp helpKey={HELP_KEYS.SALES_CHECKLIST} />
          </div>
          <div>Factura lista para emitir.</div>
        </div>
      </div>
    );
  }

  const nextStep =
    ctx.emitBlockers[0]?.message ?? "Complete los datos requeridos para emitir.";

  return (
    <div className="sf-next-step">
      <span className="material-symbols-outlined sf-next-step__icon">
        arrow_forward
      </span>
      <div>
        <div className="sf-next-step__title">
          Siguiente paso
          <ZHFieldHelp helpKey={HELP_KEYS.SALES_CHECKLIST} />
        </div>
        <div>{nextStep}</div>
      </div>
    </div>
  );
}
