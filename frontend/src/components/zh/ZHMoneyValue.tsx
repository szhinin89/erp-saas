import type { JSX } from "react";
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
  /** Override CONTRACTUAL explícito (constante `*_DECIMALS` documentada): gana sobre `precision`.
   * Sin `decimals` ni `precision` = default legacy 2, DEPRECATED (ver sobrecargas). */
  decimals?: number;
  className?: string;
};

/** Default legacy para consumidores que aún no declaran `precision` ni `decimals`. */
const LEGACY_DEFAULT_DECIMALS = 2;

type ZHMoneyValueBaseProps = Omit<ZHMoneyValueProps, "precision" | "decimals">;

/**
 * @deprecated ZH-DESIGN-SYSTEM-PRECISION-05B — LEGACY: sin `precision` ni `decimals` usa el default
 * fijo 2. Solo compatibilidad de consumidores legacy baselined (F-PREC-implicit-value). Código
 * nuevo: `precision="<PrecisionKind>"`.
 */
export function ZHMoneyValue(
  props: ZHMoneyValueBaseProps & { precision?: undefined; decimals?: undefined },
): JSX.Element;
/** Override contractual: `decimals={X_DECIMALS}` (constante nombrada y documentada). */
export function ZHMoneyValue(
  props: ZHMoneyValueBaseProps & { precision?: PrecisionKind; decimals: number },
): JSX.Element;
/**
 * Valor monetario de solo lectura con precisión semántica (patrón estándar). Formato único
 * `formatDecimalDisplay` (Decimal.js ROUND_HALF_UP, punto decimal); `Intl` solo aplica
 * representación cuando se pide un locale. Negativos: se conserva el contrato actual `$-5.00`.
 */
export function ZHMoneyValue(
  props: ZHMoneyValueBaseProps & { precision: PrecisionKind; decimals?: number },
): JSX.Element;
export function ZHMoneyValue({
  value,
  currencySymbol = "$",
  emphasis = "default",
  align = "end",
  locale,
  precision,
  decimals,
  className,
}: ZHMoneyValueProps): JSX.Element {
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
