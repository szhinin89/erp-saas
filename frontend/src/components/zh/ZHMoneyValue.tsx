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
  /** Semántica del valor (estándar): los decimales salen de la PrecisionPolicy de la empresa vía
   * `usePrecisionDecimals`, y el componente re-renderiza si la policy cambia. */
  precision?: PrecisionKind;
  /** Override explícito / compatibilidad legacy: gana sobre `precision`. Sin `decimals` ni
   * `precision` se mantiene el default legacy de 2. */
  decimals?: number;
  className?: string;
};

/** Default legacy para consumidores que aún no declaran `precision` ni `decimals`. */
const LEGACY_DEFAULT_DECIMALS = 2;

/**
 * Valor monetario de solo lectura. Formato único `formatDecimalDisplay` (Decimal.js
 * ROUND_HALF_UP, punto decimal); `Intl` solo aplica representación cuando se pide un locale.
 * Negativos: se conserva el contrato actual `$-5.00`.
 */
export function ZHMoneyValue({
  value,
  currencySymbol = "$",
  emphasis = "default",
  align = "end",
  locale,
  precision,
  decimals,
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

  if (decimals == null && precision !== undefined) {
    return <SemanticDecimals kind={precision}>{render}</SemanticDecimals>;
  }
  return render(decimals ?? LEGACY_DEFAULT_DECIMALS);
}
