import type { HTMLAttributes, ReactNode } from "react";

export type ZHInfoRowProps = HTMLAttributes<HTMLDivElement> & {
  label: ReactNode;
  value: ReactNode;
  /** Fila ancha: label arriba, value alineado a la derecha pero con salto de línea
   * permitido — para valores largos (p.ej. "Sin presentación vinculada", equivalencias). */
  wide?: boolean;
};

/** Par label/valor compacto del Design System ZH — reemplaza los pares locales
 * `.pdl-cost-label`/`.pdl-cost-val` (y equivalentes) repetidos en paneles de detalle
 * de línea/card por módulo. `label` no aplica ZHFieldLabel automáticamente: el
 * consumidor decide (normalmente `<ZHFieldLabel size="sm">`), esta fila solo da el
 * layout label/value. `value` no formatea nada: pasar `ZHDataValue`/`ZHMoneyValue`. */
export function ZHInfoRow({ label, value, wide, className, ...rest }: ZHInfoRowProps) {
  const cls = ["zh-info-row", wide ? "zh-info-row--wide" : "", className]
    .filter(Boolean)
    .join(" ");

  return (
    <div {...rest} className={cls}>
      <span className="zh-info-row__label">{label}</span>
      <span className="zh-info-row__value">{value}</span>
    </div>
  );
}
