import { useMemo, type ReactNode } from "react";
import { useZHLocale } from "./ZHLocaleProvider";
import { formatDecimalDisplay } from "../../lib/sanitizers";
import { usePrecisionDecimals } from "../../hooks/usePrecisionPolicy";
import type { PrecisionKind } from "../../lib/config/precisionPolicy.config";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-02A — piezas INTERNAS compartidas por `ZHMoneyValue` y
 * `ZHNumberValue` (no usar directamente desde módulos): un solo renderer de markup/estado vacío y
 * un solo camino semántico → decimales. El formato es siempre `formatDecimalDisplay`.
 */

export type ZHNumericEmphasis = "default" | "muted" | "strong" | "total" | "grand";
export type ZHNumericAlign = "start" | "end";

type NumericDisplayProps = {
  /** Bloque BEM del componente público (`zh-money-value` / `zh-number-value`). */
  block: string;
  value: number | null | undefined;
  decimals: number;
  locale?: string;
  emphasis: ZHNumericEmphasis;
  align: ZHNumericAlign;
  className?: string;
  /** Nodos antes/después del importe (símbolo, prefijo, sufijo), ya con su clase BEM. */
  before?: ReactNode;
  after?: ReactNode;
};

/** Renderer único: clases, "—" para null/undefined (cero NO es vacío) y el importe formateado. */
export function NumericDisplay({
  block,
  value,
  decimals,
  locale,
  emphasis,
  align,
  className,
  before,
  after,
}: NumericDisplayProps) {
  const contextLocale = useZHLocale();
  const effectiveLocale = locale ?? contextLocale;
  const isEmpty = value === null || value === undefined;

  const formatted = useMemo(
    () => (isEmpty ? null : formatDecimalDisplay(value, decimals, effectiveLocale)),
    [isEmpty, value, decimals, effectiveLocale],
  );

  const cls = [
    block,
    `${block}--${emphasis}`,
    `${block}--${align}`,
    isEmpty ? `${block}--empty` : "",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  if (isEmpty) return <span className={cls}>—</span>;

  return (
    <span className={cls}>
      {before}
      <span className={`${block}__amount`}>{formatted}</span>
      {after}
    </span>
  );
}

/**
 * Resuelve los decimales de una semántica y delega el render. Existe como componente propio para
 * respetar las Rules of Hooks: el hook solo se ejecuta cuando el consumidor pidió `precision` sin
 * `decimals`, así los consumidores legacy no dependen de la PrecisionPolicy.
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
