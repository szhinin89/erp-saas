import { useEffect, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";

interface ZHHelpPopoverProps {
  open: boolean;
  onClose: () => void;
  anchorRef: React.RefObject<HTMLElement | null>;
  titleId: string;
  title: string;
  children: ReactNode;
  /** Cierra también al quitar el mouse del trigger/popover (usado cuando se abrió por hover, no por click). */
  closeOnMouseLeave?: boolean;
}

/** Panel de ayuda anclado a un trigger. Mismo recipe que ZHHeaderUserMenu: rect manual +
 * createPortal + pointerdown en captura + Escape, sin librería de positioning (no hay ninguna
 * instalada en el proyecto). */
export function ZHHelpPopover({
  open,
  onClose,
  anchorRef,
  titleId,
  title,
  children,
  closeOnMouseLeave,
}: ZHHelpPopoverProps) {
  const popoverRef = useRef<HTMLDivElement | null>(null);
  const [pos, setPos] = useState<{ top: number; left: number } | null>(null);

  useEffect(() => {
    if (!open) {
      setPos(null);
      return;
    }
    const anchorEl = anchorRef.current;
    if (anchorEl) {
      const r = anchorEl.getBoundingClientRect();
      setPos({
        top: Math.round(r.bottom + 8),
        left: Math.max(12, Math.round(r.left)),
      });
    }

    const onDown = (e: MouseEvent | TouchEvent) => {
      const target = e.target;
      if (!(target instanceof Node)) return;
      const anchor = anchorRef.current;
      const pop = popoverRef.current;
      if (anchor?.contains(target)) return;
      if (pop?.contains(target)) return;
      onClose();
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
        anchorRef.current?.focus();
      }
    };
    const onLeave = () => {
      if (closeOnMouseLeave) onClose();
    };

    window.addEventListener("pointerdown", onDown as unknown as EventListener, true);
    window.addEventListener("keydown", onKey);
    const anchor = anchorRef.current;
    const pop = popoverRef.current;
    if (closeOnMouseLeave) {
      anchor?.addEventListener("mouseleave", onLeave);
      pop?.addEventListener("mouseleave", onLeave);
    }
    return () => {
      window.removeEventListener("pointerdown", onDown as unknown as EventListener, true);
      window.removeEventListener("keydown", onKey);
      if (closeOnMouseLeave) {
        anchor?.removeEventListener("mouseleave", onLeave);
        pop?.removeEventListener("mouseleave", onLeave);
      }
    };
  }, [open, onClose, anchorRef, closeOnMouseLeave]);

  if (!open || !pos) return null;

  return createPortal(
    <div
      ref={popoverRef}
      className="zh-help-popover"
      role="dialog"
      aria-labelledby={titleId}
      style={{ top: pos.top, left: pos.left }}
    >
      <div className="zh-help-popover__title" id={titleId}>
        {title}
      </div>
      <div className="zh-help-popover__body">{children}</div>
    </div>,
    document.body,
  );
}
