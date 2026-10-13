import { useEffect, useLayoutEffect, useRef, type ReactNode } from "react";
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

/** Portal fijo con medición previa y colisiones de viewport, siguiendo el patrón de
 * ZhWarehouseSelector. Conserva pointerdown en captura y Escape. */
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
  useLayoutEffect(() => {
    if (!open) return;
    const panel = popoverRef.current;
    const anchor = anchorRef.current;
    if (!panel || !anchor) return;
    const position = () => {
      const viewport = window.visualViewport;
      const width = viewport?.width ?? document.documentElement.clientWidth;
      const height = viewport?.height ?? document.documentElement.clientHeight;
      const x = viewport?.offsetLeft ?? 0;
      const y = viewport?.offsetTop ?? 0;
      const styles = getComputedStyle(panel);
      const margin = Math.min(parseFloat(styles.getPropertyValue("--space-3")) || 12, width / 2, height / 2);
      const gap = parseFloat(styles.getPropertyValue("--space-2")) || 8;
      const bounds = anchor.getBoundingClientRect();
      const availableWidth = Math.max(0, width - margin * 2);
      panel.style.maxWidth = `${Math.min(320, availableWidth)}px`;
      panel.style.maxHeight = `${Math.max(0, height - margin * 2)}px`;
      const panelWidth = panel.getBoundingClientRect().width;
      const desiredHeight = panel.scrollHeight + (parseFloat(styles.borderTopWidth) || 0) + (parseFloat(styles.borderBottomWidth) || 0);
      const below = Math.max(0, y + height - margin - bounds.bottom - gap);
      const above = Math.max(0, bounds.top - gap - y - margin);
      const openAbove = desiredHeight > below && above > below;
      panel.style.maxHeight = `${Math.min(height - margin * 2, openAbove ? above : below)}px`;
      const panelHeight = panel.getBoundingClientRect().height;
      const preferredLeft = bounds.left + panelWidth <= x + width - margin
        ? bounds.left : bounds.right - panelWidth;
      panel.style.left = `${Math.max(x + margin, Math.min(preferredLeft, x + width - margin - panelWidth))}px`;
      const preferredTop = openAbove ? bounds.top - gap - panelHeight : bounds.bottom + gap;
      panel.style.top = `${Math.max(y + margin, Math.min(preferredTop, y + height - margin - panelHeight))}px`;
      panel.style.visibility = "visible";
    };
    position();
    // No cambiar dimensiones dentro de la entrega de ResizeObserver: evita ciclos
    // cuando el clamp cambia el tamaño del propio panel observado.
    let frame: number | null = null;
    const schedulePosition = () => {
      if (frame !== null) cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        frame = null;
        position();
      });
    };
    const observer = typeof ResizeObserver !== "undefined" ? new ResizeObserver(schedulePosition) : null;
    observer?.observe(anchor);
    observer?.observe(panel);
    const body = panel.querySelector(".zh-help-popover__body");
    if (body) observer?.observe(body);
    window.addEventListener("resize", position);
    window.addEventListener("scroll", position, true);
    window.visualViewport?.addEventListener("resize", position);
    window.visualViewport?.addEventListener("scroll", position);
    return () => {
      if (frame !== null) cancelAnimationFrame(frame);
      observer?.disconnect();
      window.removeEventListener("resize", position);
      window.removeEventListener("scroll", position, true);
      window.visualViewport?.removeEventListener("resize", position);
      window.visualViewport?.removeEventListener("scroll", position);
    };
  }, [open, anchorRef, children, title]);

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
