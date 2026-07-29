import { useEffect, useRef, type ReactNode } from "react";

export type ZHDrawerSize = "sm" | "md" | "lg";

interface ZHDrawerProps {
  open: boolean;
  onClose: () => void;
  size?: ZHDrawerSize;
  title: string;
  subtitle?: string;
  children: ReactNode;
  footer?: ReactNode;
  closeLabel?: string;
  ariaLabel?: string;
}

/**
 * Panel lateral (no modal): permite editar mientras la tabla de fondo permanece visible e
 * interactiva. Reutiliza los mismos tokens visuales que ZHModal (header degradado, botón de
 * cierre, tipografía) sin tocar ZHModal (INMUTABLE — AI-RULES/MODAL-STANDARD.md). Usar cuando
 * el usuario necesita editar varios registros consecutivos sin perder el contexto de la lista
 * (p. ej. excepciones de precio por producto dentro de una PriceList).
 */
export function ZHDrawer({
  open,
  onClose,
  size = "md",
  title,
  subtitle,
  children,
  footer,
  closeLabel = "Cerrar",
  ariaLabel,
}: ZHDrawerProps) {
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);

    const firstFocusable = panelRef.current?.querySelector<HTMLElement>(
      'input:not([disabled]),select:not([disabled]),textarea:not([disabled]),button:not([disabled]),[tabindex]:not([tabindex="-1"])',
    );
    firstFocusable?.focus();

    return () => window.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      className="zh-drawer-overlay"
      role="dialog"
      aria-modal="true"
      aria-label={ariaLabel ?? title}
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div ref={panelRef} className={`zh-drawer zh-drawer--${size}`}>
        <div className="zh-form-header">
          <div className="zh-form-header-text">
            <h2 className="zh-form-title">{title}</h2>
            {subtitle && <p className="zh-form-subtitle">{subtitle}</p>}
          </div>
          <div className="zh-form-header-right">
            <button
              className="zh-form-header-close"
              onClick={onClose}
              type="button"
              aria-label={closeLabel}
            >
              <span className="material-symbols-outlined">close</span>
            </button>
          </div>
        </div>

        <div className="zh-drawer-body">{children}</div>

        {footer && <div className="zh-drawer-footer">{footer}</div>}
      </div>
    </div>
  );
}
