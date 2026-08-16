import { useEffect, useRef, type ReactNode } from "react";
import { ZHIconButton, type ZHIconButtonVariant } from "./ZHIconButton";
import { ZHLineActionMenuContext } from "./ZHLineActionItem";

export type ZHLineActionKind = "button" | "menu";
export type ZHLineActionTone = "default" | "primary" | "warning" | "danger";
export type ZHLineActionMenuAlign = "start" | "end";

/** @deprecated usar `tone` — se mantiene por compatibilidad con el rail de acciones de Compras. */
export type ZHLineActionVariant = "default" | "danger";
/** @deprecated usar `compact` — se mantiene por compatibilidad con el rail de acciones de Compras. */
export type ZHLineActionSize = "sm" | "md" | "lg";

export interface ZHLineActionProps {
  /** `"button"` (default) = acción simple con onClick. `"menu"` = trigger que abre un panel. */
  kind?: ZHLineActionKind;
  /** Nombre del ícono Material Symbols. */
  icon: string;
  label: string;
  /** Tooltip / `title` del botón. Si se omite, cae a `label`. */
  title?: string;
  ariaLabel?: string;
  onClick?: () => void;
  disabled?: boolean;
  loading?: boolean;
  tone?: ZHLineActionTone;
  /** @deprecated usar `tone` */
  variant?: ZHLineActionVariant;
  showText?: boolean;
  /** @deprecated usar `compact` */
  size?: ZHLineActionSize;
  compact?: boolean;
  className?: string;

  // --- kind="menu" ---
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  children?: ReactNode;
  /** Si es `true`, seleccionar un `ZHLineActionItem` hijo cierra el menú automáticamente.
   * Por defecto `false`: el consumidor controla el cierre (necesario cuando una acción del
   * menú debe mantenerlo abierto, ej. un selector inline). */
  closeOnSelect?: boolean;
  menuAlign?: ZHLineActionMenuAlign;
}

const iconButtonVariant: Record<ZHLineActionTone, ZHIconButtonVariant> = {
  default: "ghost",
  primary: "primary",
  warning: "ghost",
  danger: "danger",
};

/** Acción para filas de ítems (ver `ZHLineActionRail`) — modo `button` (simple) o `menu`
 * (trigger + panel) — tokens `zh-ui.css` — `.zh-line-action` / `.zh-line-action-menu`. */
export function ZHLineAction(props: ZHLineActionProps) {
  const {
    kind = "button",
    icon,
    label,
    title,
    ariaLabel,
    onClick,
    disabled,
    loading,
    tone,
    variant,
    showText,
    size = "md",
    compact,
    className = "",
    open = false,
    onOpenChange,
    children,
    closeOnSelect = false,
    menuAlign = "start",
  } = props;

  const resolvedTone: ZHLineActionTone = tone ?? (variant === "danger" ? "danger" : "default");
  const resolvedTitle = title ?? label;
  const resolvedShowText = showText ?? kind === "button";

  if (kind === "menu") {
    return (
      <ZHLineActionMenuTrigger
        icon={icon}
        label={label}
        title={resolvedTitle}
        ariaLabel={ariaLabel}
        disabled={disabled}
        loading={loading}
        tone={resolvedTone}
        showText={resolvedShowText}
        className={className}
        open={open}
        onOpenChange={onOpenChange}
        closeOnSelect={closeOnSelect}
        menuAlign={menuAlign}
      >
        {children}
      </ZHLineActionMenuTrigger>
    );
  }

  return (
    <div
      className={`zh-line-action zh-line-action--${size} ${compact ? "zh-line-action--compact" : ""} ${className}`.trim()}
    >
      <ZHIconButton
        icon={icon}
        title={resolvedTitle}
        ariaLabel={ariaLabel}
        variant={iconButtonVariant[resolvedTone]}
        onClick={onClick}
        disabled={disabled || loading}
        className={`zh-line-action__btn zh-line-action__btn--${size} ${
          resolvedTone === "warning" ? "zh-line-action__btn--tone-warning" : ""
        }`.trim()}
      />
      {resolvedShowText && label && (
        <span
          className={`zh-line-action__label ${
            resolvedTone === "danger" ? "zh-line-action__label--danger" : ""
          } ${resolvedTone === "warning" ? "zh-line-action__label--warning" : ""}`.trim()}
        >
          {label}
        </span>
      )}
    </div>
  );
}

function ZHLineActionMenuTrigger({
  icon,
  label,
  title,
  ariaLabel,
  disabled,
  loading,
  tone,
  showText,
  className,
  open,
  onOpenChange,
  closeOnSelect,
  menuAlign,
  children,
}: {
  icon: string;
  label: string;
  title: string;
  ariaLabel?: string;
  disabled?: boolean;
  loading?: boolean;
  tone: ZHLineActionTone;
  showText: boolean;
  className: string;
  open: boolean;
  onOpenChange?: (open: boolean) => void;
  closeOnSelect: boolean;
  menuAlign: ZHLineActionMenuAlign;
  children?: ReactNode;
}) {
  const rootRef = useRef<HTMLDivElement>(null);

  // Cierre por click-outside / Escape — mismo patrón usado en ZHHeaderUserMenu/ZHModal/etc.
  // Delegado por completo a `onOpenChange`: el consumidor decide si el cierre procede
  // (ver ProductLineActionMenu, que lo ignora mientras hay un modal interno abierto).
  useEffect(() => {
    if (!open) return;
    const onDown = (event: MouseEvent | TouchEvent) => {
      const target = event.target;
      if (!(target instanceof Node)) return;
      if (rootRef.current?.contains(target)) return;
      onOpenChange?.(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onOpenChange?.(false);
    };
    window.addEventListener("pointerdown", onDown as unknown as EventListener, true);
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("pointerdown", onDown as unknown as EventListener, true);
      window.removeEventListener("keydown", onKey);
    };
  }, [open, onOpenChange]);

  const triggerDisabled = disabled || loading;

  return (
    <div className={`zh-line-action-menu ${className}`.trim()} ref={rootRef}>
      <button
        type="button"
        className={`zh-line-action-menu__trigger ${
          tone === "primary" ? "zh-line-action-menu__trigger--primary" : ""
        }`.trim()}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={ariaLabel ?? title}
        title={title}
        disabled={triggerDisabled}
        onClick={() => onOpenChange?.(!open)}
      >
        <span className="material-symbols-outlined zh-line-action-menu__trigger-icon">
          {icon}
        </span>
        <span
          className={`material-symbols-outlined zh-line-action-menu__trigger-chevron${
            open ? " zh-line-action-menu__trigger-chevron--open" : ""
          }`}
        >
          expand_more
        </span>
      </button>
      {showText && <span className="zh-line-action__label">{label}</span>}
      {open && (
        <div
          className={`zh-line-action-menu__panel ${
            menuAlign === "end" ? "zh-line-action-menu__panel--end" : ""
          }`.trim()}
          role="menu"
        >
          <ZHLineActionMenuContext.Provider value={{ closeOnSelect, onOpenChange }}>
            {children}
          </ZHLineActionMenuContext.Provider>
        </div>
      )}
    </div>
  );
}
