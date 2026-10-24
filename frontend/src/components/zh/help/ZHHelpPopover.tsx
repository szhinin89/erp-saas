import { useEffect, useRef, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { useAnchoredOverlayPosition } from "../useAnchoredOverlayPosition";

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

/** Portal fijo con medición previa y colisiones de viewport (useAnchoredOverlayPosition, patrón de
 * ZhWarehouseSelector). Conserva pointerdown en captura y Escape. */
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
  useAnchoredOverlayPosition({
    open,
    anchorRef,
    panelRef: popoverRef,
    bodySelector: ".zh-help-popover__body",
    deps: [children, title],
  });

  useEffect(() => {
    if (!open) return;

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
        anchorRef.current?.focus({ preventScroll: true });
        onClose();
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

  if (!open) return null;

  return createPortal(
    <div
      ref={popoverRef}
      className="zh-help-popover"
      role="dialog"
      aria-labelledby={titleId}
      style={{ top: 0, left: 0, visibility: "hidden" }}
    >
      <div className="zh-help-popover__title" id={titleId}>
        {title}
      </div>
      <div className="zh-help-popover__body">{children}</div>
    </div>,
    document.body,
  );
}
