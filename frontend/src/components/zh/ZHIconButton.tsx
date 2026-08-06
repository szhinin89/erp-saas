

export type ZHIconButtonVariant =
  | "primary"
  | "success"
  | "danger";

interface ZHIconButtonProps {
  icon: string;
  title: string;
  variant?: ZHIconButtonVariant;
  onClick?: React.MouseEventHandler<HTMLButtonElement>;
  disabled?: boolean;
  className?: string;
}

const variantClass: Record<ZHIconButtonVariant, string> = {
  primary: "prd-icon-btn--primary",
  success: "prd-icon-btn--success",
  danger: "prd-icon-btn--danger",
};

export function ZHIconButton({
  icon,
  title,
  variant = "primary",
  onClick,
  disabled,
  className = "",
}: ZHIconButtonProps) {
  return (
    <button
      type="button"
      className={`prd-icon-btn ${variantClass[variant]} ${className}`}
      onClick={onClick}
      title={title}
      disabled={disabled}
    >
      <span className="material-symbols-outlined">
        {icon}
      </span>
    </button>
  );
}
