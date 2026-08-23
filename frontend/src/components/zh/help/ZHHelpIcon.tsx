interface ZHHelpIconProps {
  id?: string;
  ariaLabel: string;
  expanded: boolean;
  describedById?: string;
  onClick: () => void;
  onMouseEnter?: () => void;
  triggerRef: React.RefObject<HTMLButtonElement | null>;
}

/** Trigger de ayuda contextual: icono "?" pequeño, ghost, circular. Reutiliza la estética de
 * ZHIconButton (variant ghost) con una clase propia para el tamaño reducido del help icon. */
export function ZHHelpIcon({
  id,
  ariaLabel,
  expanded,
  describedById,
  onClick,
  onMouseEnter,
  triggerRef,
}: ZHHelpIconProps) {
  return (
    <button
      id={id}
      ref={triggerRef}
      type="button"
      className="zh-help-icon"
      aria-label={ariaLabel}
      aria-haspopup="dialog"
      aria-expanded={expanded}
      aria-describedby={expanded ? describedById : undefined}
      onClick={onClick}
      onMouseEnter={onMouseEnter}
      onFocus={onMouseEnter}
    >
      <span className="material-symbols-outlined" aria-hidden="true">
        help
      </span>
    </button>
  );
}
