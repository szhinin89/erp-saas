import type { HTMLAttributes, ReactNode } from "react";

export type ZHDataValueVariant = "default" | "muted" | "numeric" | "strong" | "code";
export type ZHDataValueTone = "default" | "success" | "danger";

export type ZHDataValueProps = HTMLAttributes<HTMLSpanElement> & {
  children: ReactNode;
  /** `default` = dato normal (14px/600). `muted` = texto secundario (12px/400).
   * `numeric` = dato numérico no monetario (14px/700, tabular-nums, sin monoespaciado).
   * `strong` = dato destacado (16px/700). `code` = código técnico (SKU, secuencial):
   * único variant con font-family monoespaciada — excepción documentada, nunca por defecto. */
  variant?: ZHDataValueVariant;
  /** Color semántico opcional (p.ej. margen positivo/negativo, stock crítico) —
   * mantiene el color fuera de CSS local de cada módulo. */
  tone?: ZHDataValueTone;
  /** Acento itálico opcional (p.ej. notas/hints informativos) — evita que cada
   * módulo defina `font-style: italic` en CSS local. */
  italic?: boolean;
};

/** Dato de solo lectura del Design System ZH — reemplaza estilos locales tipo
 * `.pdl-cost-val`/`.pdl-stock-val`/`.pdl-line__product-meta-value` repetidos por módulo.
 * No es para montos (usar `ZHMoneyValue`) ni para texto editable (usar los `Zh*Input`). */
export function ZHDataValue({
  children,
  variant = "default",
  tone = "default",
  italic = false,
  className,
  ...rest
}: ZHDataValueProps) {
  const cls = [
    "zh-data-value",
    `zh-data-value--${variant}`,
    tone !== "default" ? `zh-data-value--tone-${tone}` : "",
    italic ? "zh-data-value--italic" : "",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <span {...rest} className={cls}>
      {children}
    </span>
  );
}
