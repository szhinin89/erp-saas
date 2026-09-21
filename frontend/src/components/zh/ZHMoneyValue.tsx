import { useMemo } from "react";
import { useZHLocale } from "./ZHLocaleProvider";
import { formatMoney } from "../../lib/sanitizers";

export type ZHMoneyValueEmphasis = "default" | "muted" | "strong" | "total" | "grand";
export type ZHMoneyValueAlign = "start" | "end";

export type ZHMoneyValueProps = {
  value: number | null | undefined;
  currencySymbol?: string;
  emphasis?: ZHMoneyValueEmphasis;
  align?: ZHMoneyValueAlign;
  /** Locale regional para el formato numérico (p. ej. `"en-US"`, `"es-EC"`). Prioridad:
   * prop explícita → `ZHLocaleProvider` (`useZHLocale`) → sin locale explícito, usa
   * `formatMoney` (punto decimal fijo, `lib/sanitizers.ts`). */
  locale?: string;
  /** Cantidad de decimales a mostrar — el módulo consumidor decide cuál config de decimales
   * aplica según el tipo de valor (`totalAmount`, `taxAmount`, `salesUnitPrice`,
   * `purchaseUnitCost`, etc.); `ZHMoneyValue` no conoce ni resuelve esa config por sí mismo.
   * Default `2`. La policy backend valida la escala; el componente la respeta sin recortarla. */
  decimals?: number;
  className?: string;
};

const DEFAULT_DECIMALS = 2;
function resolveDecimals(decimals: number | null | undefined): number {
  return decimals ?? DEFAULT_DECIMALS;
}

/** Sin `locale` prop ni `ZHLocaleProvider` en el árbol, se usa `formatMoney` (punto decimal
 * fijo, `InvariantCulture`) en vez de `Intl.NumberFormat` — bug detectado en
 * DS-LINE-ATOM-MIGRATION-01: `Intl.NumberFormat("es-EC", …)` formatea con coma decimal
 * (`"59,80"`), lo que viola el Estándar de Decimales del proyecto (punto decimal obligatorio,
 * `Intl.NumberFormat`/`toLocaleString` prohibidos para montos salvo locale explícito) y rompía
 * el look aprobado de Ventas al adoptar el átomo. `Intl.NumberFormat` solo se usa cuando un
 * locale fue pedido explícitamente (prop o provider) — nunca por default. */
export function ZHMoneyValue({
  value,
  currencySymbol = "$",
  emphasis = "default",
  align = "end",
  locale,
  decimals,
  className,
}: ZHMoneyValueProps) {
  const contextLocale = useZHLocale();
  const effectiveLocale = locale ?? contextLocale;
  const effectiveDecimals = resolveDecimals(decimals);
  const isEmpty = value === null || value === undefined;

  const formatted = useMemo(() => {
    if (isEmpty) return null;
    if (!effectiveLocale) return formatMoney(value, effectiveDecimals);
    return new Intl.NumberFormat(effectiveLocale, {
      minimumFractionDigits: effectiveDecimals,
      maximumFractionDigits: effectiveDecimals,
    }).format(value);
  }, [isEmpty, value, effectiveLocale, effectiveDecimals]);

  const cls = [
    "zh-money-value",
    `zh-money-value--${emphasis}`,
    `zh-money-value--${align}`,
    isEmpty ? "zh-money-value--empty" : "",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  if (isEmpty) {
    return <span className={cls}>—</span>;
  }

  return (
    <span className={cls}>
      <span className="zh-money-value__symbol">{currencySymbol}</span>
      <span className="zh-money-value__amount">{formatted}</span>
    </span>
  );
}
