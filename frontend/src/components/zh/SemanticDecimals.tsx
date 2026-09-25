import type { ReactNode } from "react";
import { usePrecisionDecimals } from "../../hooks/usePrecisionPolicy";
import type { PrecisionKind } from "../../lib/config/precisionPolicy.config";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-02A/03B — ÚNICO puente semántica → decimales del Design System,
 * compartido por displays (`ZHMoneyValue`, `ZHNumberValue`) e inputs (`ZhDecimalInput`,
 * `ZhCurrencyInput`). INTERNO: no usar desde módulos.
 *
 * ZH-DESIGN-SYSTEM-PRECISION-06: `precision` es obligatorio en toda la API pública, así que este
 * puente resuelve SIEMPRE los decimales (no existe rama `decimals`/legacy).
 */
export function SemanticDecimals({
  kind,
  children,
}: {
  kind: PrecisionKind;
  children: (decimals: number) => ReactNode;
}) {
  return <>{children(usePrecisionDecimals(kind))}</>;
}
