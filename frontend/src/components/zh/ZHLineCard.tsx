import type { ReactNode } from "react";

interface ZHLineCardProps {
  /** Contenido de la columna lateral izquierda (índice, acciones) — opcional; sin `rail`, la
   * tarjeta se comporta igual que antes de agregar esta prop. */
  rail?: ReactNode;
  children: ReactNode;
  className?: string;
}

/** Tarjeta contenedora de una fila de ítem (rail de acciones + contenido) — tokens `zh-ui.css` —
 * `.zh-line-card` / `.zh-line-card__rail` / `.zh-line-card__body`. El componente solo define la
 * estructura (flex rail + body); cada módulo consumidor estiliza el contenido de ambas zonas con
 * su propio CSS (ver `sf-product-card` / `sf-product__rail-*` en Ventas). */
export function ZHLineCard({ rail, children, className = "" }: ZHLineCardProps) {
  return (
    <div className={`zh-line-card ${className}`.trim()}>
      {rail && <div className="zh-line-card__rail">{rail}</div>}
      <div className="zh-line-card__body">{children}</div>
    </div>
  );
}
