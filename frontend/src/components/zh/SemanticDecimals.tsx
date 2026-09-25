import type { ReactNode } from "react";
import { usePrecisionDecimals } from "../../hooks/usePrecisionPolicy";
import type { PrecisionKind } from "../../lib/config/precisionPolicy.config";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-02A/03B — ÚNICO puente semántica → decimales del Design System,
 * compartido por displays (`ZHMoneyValue`, `ZHNumberValue`) e inputs (`ZhDecimalInput`,
 * `ZhCurrencyInput`). INTERNO: no usar desde módulos.
 *
 * Existe como componente para respetar las Rules of Hooks: el hook solo se ejecuta cuando el
 * consumidor pidió `precision` sin `decimals`, así los consumidores legacy no dependen de la
 * PrecisionPolicy.
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
