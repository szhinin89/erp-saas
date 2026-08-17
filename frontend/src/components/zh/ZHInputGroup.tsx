import type { ReactNode } from "react";

export type ZHInputGroupProps = {
  /** Adorno antes del input (p.ej. "$"). */
  prefix?: ReactNode;
  /** Adorno después del input (p.ej. una unidad). */
  suffix?: ReactNode;
  children: ReactNode;
  className?: string;
};

/** Agrupa un prefijo/sufijo con un input `Zh*Input` en un solo control visual
 * (borde/foco compartidos) — reemplaza wrappers locales tipo `.pdl-line__cost-group`
 * repetidos por módulo cada vez que un input necesita un prefijo de moneda/unidad.
 * El input hijo no debe traer su propio borde/foco: `.zh-input-group` se los quita
 * (border:none, box-shadow:none) y los aplica él mismo vía :focus-within. */
export function ZHInputGroup({ prefix, suffix, children, className }: ZHInputGroupProps) {
  const cls = ["zh-input-group", className].filter(Boolean).join(" ");

  return (
    <div className={cls}>
      {prefix != null && <span className="zh-input-group__prefix">{prefix}</span>}
      {children}
      {suffix != null && <span className="zh-input-group__suffix">{suffix}</span>}
    </div>
  );
}
