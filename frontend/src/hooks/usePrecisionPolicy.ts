import { useSyncExternalStore } from "react";
import {
  PrecisionPolicyNotLoadedError,
  getPrecisionPolicySnapshot,
  resolvePrecisionDecimals,
  subscribePrecisionPolicy,
  type PrecisionKind,
  type PrecisionPolicy,
} from "../lib/config/precisionPolicy.config";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-01D: adaptador React ÚNICO de la PrecisionPolicy. Observa el snapshot
 * de `precisionPolicy.config.ts` vía `useSyncExternalStore` — React no guarda ninguna copia: la
 * policy devuelta es exactamente la referencia vigente, la misma que `getPrecisionPolicy()`.
 * Re-renderiza en cada cambio aceptado (load/save/clear, cambio de empresa).
 *
 * Fail-closed igual que `getPrecisionPolicy()`: sin política cargada lanza
 * `PrecisionPolicyNotLoadedError` (nunca un default). SPA sin SSR: no hay `getServerSnapshot`.
 */
export function usePrecisionPolicy(): PrecisionPolicy {
  const snapshot = useSyncExternalStore(subscribePrecisionPolicy, getPrecisionPolicySnapshot);
  if (!snapshot.loaded) throw new PrecisionPolicyNotLoadedError();
  return snapshot.policy;
}

/** Decimales de la semántica `kind` según la policy vigente — mapeo solo vía `resolvePrecisionDecimals`. */
export function usePrecisionDecimals(kind: PrecisionKind): number {
  return resolvePrecisionDecimals(usePrecisionPolicy(), kind);
}
