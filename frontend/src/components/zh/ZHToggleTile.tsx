import type { ButtonHTMLAttributes, ReactNode } from "react";

export type ZHToggleTileProps = Omit<
  ButtonHTMLAttributes<HTMLButtonElement>,
  "title"
> & {
  active?: boolean;
  icon?: string;
  title: ReactNode;
  subtitle?: ReactNode;
};

/** Tile/tarjeta seleccionable genérica (p.ej. método de pago, opción de un grupo
 * de opciones exclusivas) — un único `<button>`, sin estado interno ni
 * conocimiento del dominio que la usa. `active` marca la opción actualmente
 * seleccionada dentro del grupo (semántica `aria-pressed` de toggle button). */
export function ZHToggleTile({
  active = false,
  icon,
  title,
  subtitle,
  className,
  type = "button",
  ...rest
}: ZHToggleTileProps) {
  const cls = [
    "zh-toggle-tile",
    active ? "zh-toggle-tile--active" : "",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <button type={type} className={cls} aria-pressed={active} {...rest}>
      {icon && (
        <span className="material-symbols-outlined zh-toggle-tile__icon">
          {icon}
        </span>
      )}
      <span className="zh-toggle-tile__content">
        <span className="zh-toggle-tile__title">{title}</span>
        {subtitle && (
          <span className="zh-toggle-tile__subtitle">{subtitle}</span>
        )}
      </span>
    </button>
  );
}
