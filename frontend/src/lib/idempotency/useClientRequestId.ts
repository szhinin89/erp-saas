import { useCallback, useRef } from "react";

/**
 * ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — identificador de UNA intención financiera del usuario
 * (pago a proveedor, cobro, movimiento de caja), que el backend usa como ClientRequestId.
 *
 * - `idFor(payload)` devuelve el MISMO id mientras el payload sea el mismo: doble clic, Enter
 *   repetido, reintento tras timeout o respuesta incierta, reabrir el modal y reenviar → el
 *   backend responde el documento ya creado, nunca un segundo efecto.
 * - Si el payload cambia, es otra intención: id nuevo (evita un Conflict por reutilizar el id).
 * - `complete()` se llama cuando la operación terminó bien: la próxima intención, aunque tenga
 *   datos idénticos (p. ej. dos ingresos de 25), es una operación nueva y legítima.
 *
 * El estado `saving` de la UI sigue siendo solo UX; la garantía es del servidor + PostgreSQL.
 */
export function useClientRequestId() {
  const current = useRef<{ fingerprint: string; id: string } | null>(null);

  const idFor = useCallback((payload: unknown): string => {
    const fingerprint = JSON.stringify(payload);
    if (current.current?.fingerprint !== fingerprint) {
      current.current = { fingerprint, id: crypto.randomUUID() };
    }
    return current.current.id;
  }, []);

  const complete = useCallback(() => {
    current.current = null;
  }, []);

  return { idFor, complete };
}
