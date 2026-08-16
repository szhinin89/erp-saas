import type { ReactNode } from "react";
import { ZHIconButton } from "./ZHIconButton";

export type ZHPickerSelectedValueProps = {
  title: ReactNode;
  subtitle?: ReactNode;
  meta?: ReactNode;
  /** Título/aria-label del botón "quitar selección". Default genérico "Cambiar selección". */
  clearLabel?: string;
  /** Título/aria-label del botón "editar". Default genérico "Editar". */
  editLabel?: string;
  /** Quita/cambia la selección actual — nunca elimina el registro. Si se omite, no se
   * renderiza la acción de cerrar. */
  onClear?: () => void;
  /** Edita los datos del registro seleccionado. Si se omite, no se renderiza la acción. */
  onEdit?: () => void;
  className?: string;
};

/** Tarjeta compacta de "valor seleccionado" para pickers/autocompletes (cliente,
 * proveedor, factura, producto, etc.) — sin estado interno ni conocimiento del dominio
 * que la usa. Acciones apiladas verticalmente a la derecha: `close` (quitar/cambiar
 * selección, arriba) y `edit` (editar el registro seleccionado, abajo), ambas
 * opcionales e independientes entre sí. */
export function ZHPickerSelectedValue({
  title,
  subtitle,
  meta,
  clearLabel = "Cambiar selección",
  editLabel = "Editar",
  onClear,
  onEdit,
  className,
}: ZHPickerSelectedValueProps) {
  const cls = ["zh-picker-selected-value", className].filter(Boolean).join(" ");

  return (
    <div className={cls}>
      <div className="zh-picker-selected-value__content">
        <div className="zh-picker-selected-value__title">{title}</div>
        {subtitle && (
          <div className="zh-picker-selected-value__subtitle">{subtitle}</div>
        )}
        {meta && <div className="zh-picker-selected-value__meta">{meta}</div>}
      </div>
      {(onClear || onEdit) && (
        <div className="zh-picker-selected-value__actions">
          {onClear && (
            <ZHIconButton
              icon="close"
              title={clearLabel}
              variant="ghost"
              onClick={onClear}
            />
          )}
          {onEdit && (
            <ZHIconButton
              icon="edit"
              title={editLabel}
              variant="ghost"
              onClick={onEdit}
            />
          )}
        </div>
      )}
    </div>
  );
}
