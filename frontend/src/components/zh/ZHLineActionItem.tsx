import { createContext, useContext, type ReactNode } from "react";

export type ZHLineActionItemTone = "default" | "primary" | "warning" | "danger";

interface ZHLineActionMenuContextValue {
  closeOnSelect: boolean;
  onOpenChange?: (open: boolean) => void;
}

/** Contexto interno que conecta `ZHLineActionItem` con el panel de `ZHLineAction kind="menu"`
 * que lo contiene — permite el auto-cierre opcional (`closeOnSelect`) sin acoplar el ítem
 * a la lógica de dominio del consumidor. */
export const ZHLineActionMenuContext = createContext<ZHLineActionMenuContextValue>({
  closeOnSelect: false,
});

export interface ZHLineActionItemProps {
  icon?: string;
  label: string;
  onClick?: () => void;
  disabled?: boolean;
  loading?: boolean;
  tone?: ZHLineActionItemTone;
  /** Contenido supplementario no interactivo (ej. un badge). Contenido interactivo complejo
   * (ej. un picker) debe ir como hermano de este ítem dentro del panel del menú, no como hijo. */
  children?: ReactNode;
}

/** Ítem de un menú `ZHLineAction kind="menu"` — tokens `zh-ui.css` — `.zh-line-action-item`. */
export function ZHLineActionItem({
  icon,
  label,
  onClick,
  disabled,
  loading,
  tone = "default",
  children,
}: ZHLineActionItemProps) {
  const menu = useContext(ZHLineActionMenuContext);

  const handleClick = () => {
    onClick?.();
    if (menu.closeOnSelect) menu.onOpenChange?.(false);
  };

  return (
    <button
      type="button"
      className={`zh-line-action-item ${
        tone !== "default" ? `zh-line-action-item--${tone}` : ""
      }`.trim()}
      role="menuitem"
      disabled={disabled || loading}
      onClick={handleClick}
    >
      {icon && (
        <span className="material-symbols-outlined zh-line-action-item__icon">
          {icon}
        </span>
      )}
      <span className="zh-line-action-item__label">{label}</span>
      {children}
    </button>
  );
}
