import type { PrecisionKind } from "../../lib/config/precisionPolicy.config";
import {
  NumericDisplay,
  type ZHNumericAlign,
  type ZHNumericEmphasis,
} from "./ZHNumericDisplay";
import { SemanticDecimals } from "./SemanticDecimals";

type ZHNumberValueBaseProps = {
  value: number | null | undefined;
  /** Texto antes del valor (p. ej. `"×"`). */
  prefix?: string;
  /** Texto después del valor (p. ej. `"%"`, una unidad). */
  suffix?: string;
  emphasis?: ZHNumericEmphasis;
  /** Default `end`: los números se alinean a la derecha desde el Design System. */
  align?: ZHNumericAlign;
  /** Locale regional solo de representación (separadores/agrupación); nunca decide el redondeo. */
  locale?: string;
  className?: string;
};

/**
 * `precision` (semántica de la PrecisionPolicy) es el estándar; `decimals` es un override
 * excepcional y gana si se pasan ambos. Uno de los dos es obligatorio: no existe default.
 */
export type ZHNumberValueProps = ZHNumberValueBaseProps &
  (
    | { precision: PrecisionKind; decimals?: number }
    | { precision?: PrecisionKind; decimals: number }
  );

/**
 * ZH-DESIGN-SYSTEM-PRECISION-02A — valor numérico NO monetario de solo lectura (cantidades,
 * porcentajes, factores, costos sin símbolo). Mismo renderer, mismo resolver semántico y mismo
 * motor de formato (`formatDecimalDisplay`) que `ZHMoneyValue`. Solo muestra: no calcula ni
 * modifica el valor.
 */
export function ZHNumberValue({
  value,
  precision,
  decimals,
  prefix,
  suffix,
  emphasis = "default",
  align = "end",
  locale,
  className,
}: ZHNumberValueProps) {
  const render = (resolvedDecimals: number) => (
    <NumericDisplay
      block="zh-number-value"
      value={value}
      decimals={resolvedDecimals}
      locale={locale}
      emphasis={emphasis}
      align={align}
      className={className}
      before={prefix != null ? <span className="zh-number-value__prefix">{prefix}</span> : undefined}
      after={suffix != null ? <span className="zh-number-value__suffix">{suffix}</span> : undefined}
    />
  );

  if (decimals == null) {
    return <SemanticDecimals kind={precision as PrecisionKind}>{render}</SemanticDecimals>;
  }
  return render(decimals);
}
