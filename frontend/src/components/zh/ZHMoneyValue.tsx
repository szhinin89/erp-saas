import type { PrecisionKind } from "../../lib/config/precisionPolicy.config";
import {
  NumericDisplay,
  type ZHNumericAlign,
  type ZHNumericEmphasis,
} from "./ZHNumericDisplay";
import { SemanticDecimals } from "./SemanticDecimals";

export type ZHMoneyValueEmphasis = ZHNumericEmphasis;
export type ZHMoneyValueAlign = ZHNumericAlign;

export type ZHMoneyValueProps = {
  value: number | null | undefined;
  currencySymbol?: string;
  emphasis?: ZHMoneyValueEmphasis;
  align?: ZHMoneyValueAlign;
  /** Locale regional para la representación (separadores/agrupación), p. ej. `"en-US"`. Prioridad:
   * prop explícita → `ZHLocaleProvider` → sin locale: punto decimal fijo sin agrupación. El
   * redondeo NUNCA depende del locale (ver `formatDecimalDisplay`). */
  locale?: string;
  /** Semántica del valor (ÚNICA forma de decidir la escala): los decimales salen de la
   * PrecisionPolicy de la empresa vía `resolvePrecisionDecimals`, y el componente re-renderiza si
   * la policy cambia. No existe `decimals` ni default. */
  precision: PrecisionKind;
  className?: string;
};

/**
 * Valor monetario de solo lectura con precisión semántica (ZH-DESIGN-SYSTEM-PRECISION-06: única
 * API). Formato único `formatDecimalDisplay` (Decimal.js ROUND_HALF_UP, punto decimal); `Intl`
 * solo aplica representación cuando se pide un locale. Negativos: contrato `$-5.00`.
 */
export function ZHMoneyValue({
  value,
  currencySymbol = "$",
  emphasis = "default",
  align = "end",
  locale,
  precision,
  className,
}: ZHMoneyValueProps) {
  const render = (resolvedDecimals: number) => (
    <NumericDisplay
      block="zh-money-value"
      value={value}
      decimals={resolvedDecimals}
      locale={locale}
      emphasis={emphasis}
      align={align}
      className={className}
      before={<span className="zh-money-value__symbol">{currencySymbol}</span>}
    />
  );

  return <SemanticDecimals kind={precision}>{render}</SemanticDecimals>;
}
