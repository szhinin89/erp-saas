import { ZHLineAction } from "./ZHLineAction";

interface ZHRowDeleteActionProps {
  title: string;
  ariaLabel?: string;
  label?: string;
  onClick?: () => void;
  disabled?: boolean;
  className?: string;
  showText?: boolean;
  compact?: boolean;
}

/** Acción de eliminar preconfigurada para `ZHLineActionRail` (ícono `delete`, variante `danger`). */
export function ZHRowDeleteAction({
  title,
  ariaLabel,
  label,
  onClick,
  disabled,
  className,
  showText,
  compact,
}: ZHRowDeleteActionProps) {
  return (
    <ZHLineAction
      icon="delete"
      variant="danger"
      size="lg"
      title={title}
      ariaLabel={ariaLabel}
      label={label ?? title}
      onClick={onClick}
      disabled={disabled}
      className={className}
      showText={showText}
      compact={compact}
    />
  );
}
