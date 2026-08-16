import type { ReactNode } from "react";

interface ZHLineActionRailProps {
  /** Número de orden mostrado arriba de las acciones (se formatea a 2 dígitos). */
  index?: number | string;
  children: ReactNode;
  className?: string;
}

/** Columna lateral de acciones para filas de ítems — tokens `zh-ui.css` — `.zh-line-action-rail`. */
export function ZHLineActionRail({ index, children, className = "" }: ZHLineActionRailProps) {
  const indexLabel =
    typeof index === "number" ? String(index).padStart(2, "0") : index;

  return (
    <div className={`zh-line-action-rail ${className}`.trim()}>
      {indexLabel !== undefined && (
        <span className="zh-line-action-rail__index">{indexLabel}</span>
      )}
      {children}
    </div>
  );
}
