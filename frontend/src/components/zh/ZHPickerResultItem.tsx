import type { ButtonHTMLAttributes, ReactNode } from "react";

export type ZHPickerResultItemProps = Omit<
  ButtonHTMLAttributes<HTMLButtonElement>,
  "title"
> & {
  title: ReactNode;
  subtitle?: ReactNode;
  meta?: ReactNode;
  icon?: string;
  selected?: boolean;
};

/** Fila seleccionable genérica para dropdowns/autocompletes de picker (cliente,
 * proveedor, factura, producto, etc.) — un único `<button>`, sin estado interno ni
 * conocimiento del dominio que la usa. `selected` marca la opción resaltada por
 * teclado/mouse dentro del listado (semántica `aria-selected` de listbox), no
 * necesariamente "el valor ya elegido" del picker — eso lo resuelve el consumidor
 * (ver `zh-picker__selected` para el estado "ya hay un valor elegido"). */
export function ZHPickerResultItem({
  title,
  subtitle,
  meta,
  icon,
  selected = false,
  className,
  type = "button",
  ...rest
}: ZHPickerResultItemProps) {
  const cls = [
    "zh-picker-result-item",
    selected ? "zh-picker-result-item--selected" : "",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <button
      type={type}
      className={cls}
      aria-selected={selected}
      {...rest}
    >
      {icon && (
        <span className="material-symbols-outlined zh-picker-result-item__icon">
          {icon}
        </span>
      )}
      <span className="zh-picker-result-item__content">
        <span className="zh-picker-result-item__title">{title}</span>
        {subtitle && (
          <span className="zh-picker-result-item__subtitle">{subtitle}</span>
        )}
        {meta && <span className="zh-picker-result-item__meta">{meta}</span>}
      </span>
    </button>
  );
}
