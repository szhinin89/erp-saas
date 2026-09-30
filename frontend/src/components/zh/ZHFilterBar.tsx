import type { ReactNode } from "react";
import { ZHBtn } from "./ZHForm";

interface ZHFilterBarProps {
  children: ReactNode;
  /** Chips de filtros activos (p.ej. tarjetas de KPI resaltadas) — se renderizan en su propia fila. */
  chips?: ReactNode;
  onClear?: () => void;
  clearLabel?: string;
  disabled?: boolean;
  /** Sin chrome (padding/fondo/borde): filtros que ya viven dentro de una card o listado. */
  plain?: boolean;
}

/**
 * Contenedor de layout para una barra de filtros: wrap horizontal de campos + botón "Limpiar" a
 * la derecha. Los campos individuales siguen siendo inputs estándar del proyecto (no es un motor
 * de filtros por schema) — este componente solo da la estructura/chrome consistente del DS.
 */
export function ZHFilterBar({
  children,
  chips,
  onClear,
  clearLabel = "Limpiar filtros",
  disabled,
  plain = false,
}: ZHFilterBarProps) {
  return (
    <div className={plain ? "zh-filterbar zh-filterbar--plain" : "zh-filterbar"}>
      {chips && <div className="zh-filterbar__chips">{chips}</div>}
      {children}
      {onClear && (
        <ZHBtn
          variant="ghost"
          size="sm"
          type="button"
          className="zh-filterbar__clear"
          onClick={onClear}
          disabled={disabled}
        >
          <span className="material-symbols-outlined zh-icon-sm">
            filter_alt_off
          </span>
          {clearLabel}
        </ZHBtn>
      )}
    </div>
  );
}
