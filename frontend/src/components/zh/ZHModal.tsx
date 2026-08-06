import { ZHBrandMark } from "./ZHBrandMark";
import { useEffect, useRef, type ReactNode } from "react";

export type ZHModalSize = "sm" | "md" | "lg" | "xl" | "2xl" | "full";

interface ZHModalProps {
  open: boolean;
  onClose: () => void;
  size?: ZHModalSize;
  title: string;
  subtitle?: string;
  children: ReactNode;
  footer?: ReactNode;
  closeLabel?: string;
  ariaLabel?: string;
  closeOnBackdrop?: boolean;
}

export function ZHModal({
  open,
  onClose,
  size = "md",
  title,
  subtitle,
  children,
  footer,
  closeLabel = "Cerrar",
  ariaLabel,
  closeOnBackdrop = true,
}: ZHModalProps) {
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);

    const firstFocusable = dialogRef.current?.querySelector<HTMLElement>(
      'input:not([disabled]),select:not([disabled]),textarea:not([disabled]),button:not([disabled]),[tabindex]:not([tabindex="-1"])',
    );
    firstFocusable?.focus();

    return () => {
      document.body.style.overflow = prev;
      window.removeEventListener("keydown", onKey);
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      className="zh-modal-overlay"
      role="dialog"
      aria-modal="true"
      aria-label={ariaLabel ?? title}
      onClick={
        closeOnBackdrop
          ? (e) => {
              if (e.target === e.currentTarget) onClose();
            }
          : undefined
      }
    >
      <div ref={dialogRef} className={`zh-modal zh-modal--${size}`}>
        <div className="zh-form-header">
          <div className="zh-form-header-left">
            <div className="zh-form-logo">
  <ZHBrandMark />
</div>
            <div className="zh-form-header-text">
              <h2 className="zh-form-title">{title}</h2>
              {subtitle && <p className="zh-form-subtitle">{subtitle}</p>}
            </div>
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

        <div className="zh-modal-body">{children}</div>

        {footer && <div className="zh-modal-footer">{footer}</div>}
      </div>
    </div>
  );
}
