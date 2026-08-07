

export type ZHIconButtonVariant =
  | "primary"
  | "success"
  | "danger"
  /** Acciones informativas/secundarias en filas de tabla (ver, editar, duplicar):
   * neutro por defecto, resalta a primary solo en hover. */
  | "ghost";

export type ZHIconButtonShape = "default" | "circle";

interface ZHIconButtonProps {
  icon: string;
  title: string;
  /** Nombre accesible explícito, para cuando `title` es genérico (p.ej. "Desactivar")
   * y el contexto real vive en los datos de la fila (p.ej. "Desactivar Bodega Central").
   * Si se omite, el nombre accesible cae a `title` — comportamiento actual sin cambios. */
  ariaLabel?: string;
  variant?: ZHIconButtonVariant;
  /** `circle` fija un diámetro de 32px con `border-radius: 50%` (p.ej. un botón
   * "más acciones" tipo kebab). `default` mantiene el tamaño ajustado al ícono
   * (comportamiento actual, sin cambios). */
  shape?: ZHIconButtonShape;
  onClick?: React.MouseEventHandler<HTMLButtonElement>;
  disabled?: boolean;
  className?: string;
}

const variantClass: Record<ZHIconButtonVariant, string> = {
  primary: "prd-icon-btn--primary",
  success: "prd-icon-btn--success",
  danger: "prd-icon-btn--danger",
  ghost: "prd-icon-btn--ghost",
};

export function ZHIconButton({
  icon,
  title,
  ariaLabel,
  variant = "primary",
  shape = "default",
  onClick,
  disabled,
  className = "",
}: ZHIconButtonProps) {
  const shapeClass = shape === "circle" ? "prd-icon-btn--circle" : "";
  return (
    <button
      type="button"
      className={`prd-icon-btn ${variantClass[variant]} ${shapeClass} ${className}`}
      onClick={onClick}
      title={title}
      aria-label={ariaLabel ?? title}
      disabled={disabled}
    >
      <span className="material-symbols-outlined">
        {icon}
      </span>
    </button>
  );
}
