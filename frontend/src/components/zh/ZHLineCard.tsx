import type { ReactNode } from "react";

interface ZHLineCardProps {
  children: ReactNode;
  className?: string;
}

/** Tarjeta contenedora de una fila de ítem (rail de acciones + contenido) — tokens `zh-ui.css` — `.zh-line-card`. */
export function ZHLineCard({ children, className = "" }: ZHLineCardProps) {
  return <div className={`zh-line-card ${className}`.trim()}>{children}</div>;
}
