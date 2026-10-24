import { useEffect, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import type { NoticeSeverity } from "../../../notices";
import { useAnchoredOverlayPosition } from "../useAnchoredOverlayPosition";

interface ZHNoticePopoverProps {
  open: boolean;
  onClose: () => void;
  anchorRef: React.RefObject<HTMLElement | null>;
  titleId: string;
  title: string;
  severity: NoticeSeverity;
  children: ReactNode;
  /** Cierra también al quitar el mouse del trigger/popover (usado cuando se abrió por hover). */
  closeOnMouseLeave?: boolean;
  /** Opt-in: posicionamiento anclado de useAnchoredOverlayPosition (arriba/abajo según espacio,
   * clamp al viewport, sigue al trigger en scroll/resize). Sin la prop se conserva el cálculo
   * único bajo el trigger — los consumidores existentes no cambian. */
  anchored?: boolean;
}

/** Panel de aviso anclado a un trigger. Mismo recipe que ZHHelpPopover: rect manual + createPortal
 * + pointerdown en captura + Escape, sin librería de positioning. */
export function ZHNoticePopover({
  open,
  onClose,
  anchorRef,
  titleId,
  title,
  severity,
  children,
  closeOnMouseLeave,
  anchored = false,
}: ZHNoticePopoverProps) {
  const popoverRef = useRef<HTMLDivElement | null>(null);
  const [pos, setPos] = useState<{ top: number; left: number } | null>(null);
  useAnchoredOverlayPosition({
    open: open && anchored,
    anchorRef,
    panelRef: popoverRef,
    bodySelector: ".zh-notice-popover__body",
    deps: [children, title],
  });

  useEffect(() => {
    if (!open) {
      setPos(null);
      return;
    }
    const anchorEl = anchorRef.current;
    if (anchorEl && !anchored) {
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
  }, [open, onClose, anchorRef, closeOnMouseLeave, anchored]);

  if (!open || (!anchored && !pos)) return null;

  return createPortal(
    <div
      ref={popoverRef}
      className={`zh-notice-popover zh-notice-popover--${severity}${anchored ? " zh-notice-popover--anchored" : ""}`}
      role="dialog"
      aria-labelledby={titleId}
      style={anchored ? { top: 0, left: 0, visibility: "hidden" } : { top: pos?.top, left: pos?.left }}
    >
      <div className="zh-notice-popover__title" id={titleId}>
        {title}
      </div>
      <div className="zh-notice-popover__body">{children}</div>
    </div>,
    document.body,
  );
}
