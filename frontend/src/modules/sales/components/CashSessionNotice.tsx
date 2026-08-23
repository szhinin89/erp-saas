import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import type {
  SalesPageContext,
  CashSessionCheckErrorReason,
} from "../hooks/useSalesPage";

// Mensaje por motivo real de falla al consultar GET /cash-sessions/my — nunca el mismo texto que
// "no hay caja abierta" (esa es la única respuesta 200 OK con `null`, ver useSalesPage.ts).
const CASH_SESSION_ERROR_MESSAGE: Record<CashSessionCheckErrorReason, string> =
  {
    permission:
      "No se pudo verificar la caja abierta por falta de permiso para consultar caja.",
    context:
      "No se pudo verificar la caja abierta por contexto incompleto de empresa/sucursal.",
    server:
      "No se pudo verificar la caja abierta. Reintente o revise conexión/servidor.",
  };

export interface CashSessionNoticeProps {
  ctx: SalesPageContext;
}

/** Aviso de estado de caja — distingue "no hay caja" (confirmado) de "no se pudo verificar"
 * (permiso/contexto/servidor), con acción de reintento para el segundo caso. */
export function CashSessionNotice({ ctx }: CashSessionNoticeProps) {
  if (ctx.cashSessionCheckError) {
    return (
      <div className="sf-cash-session-notice">
        <ZHPageNotice
          variant="error"
          message={CASH_SESSION_ERROR_MESSAGE[ctx.cashSessionCheckError]}
        />
        <ZHBtn
          variant="ghost"
          size="xs"
          type="button"
          onClick={ctx.refreshCashSession}
        >
          Reintentar
        </ZHBtn>
      </div>
    );
  }
  if (ctx.hasCashSession === false) {
    return (
      <ZHPageNotice
        variant="warning"
        message="No tiene una caja abierta. Debe abrir una caja antes de autorizar facturas."
      />
    );
  }
  return null;
}
