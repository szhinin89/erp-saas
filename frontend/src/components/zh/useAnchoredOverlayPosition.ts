import { useLayoutEffect, type DependencyList, type RefObject } from "react";

/**
 * Posicionamiento de un panel `position: fixed` (portal) anclado a un trigger: medición previa,
 * apertura arriba/abajo según el espacio disponible, clamp horizontal/vertical al viewport
 * (incluido `visualViewport` en móvil) y reposicionamiento en scroll del documento o de
 * contenedores internos (listener en captura), resize y cambios de tamaño (ResizeObserver).
 *
 * Extraído sin cambios de ZHHelpPopover (patrón de ZhWarehouseSelector) para reutilizarlo en
 * ZHNoticePopover (`anchored`). El panel debe montarse con `visibility: hidden` y `top/left: 0`;
 * el hook lo hace visible tras la primera medición.
 */
export function useAnchoredOverlayPosition({
  open,
  anchorRef,
  panelRef,
  bodySelector,
  deps = [],
}: {
  open: boolean;
  anchorRef: RefObject<HTMLElement | null>;
  panelRef: RefObject<HTMLElement | null>;
  /** Cuerpo interno del panel cuyo cambio de tamaño también reposiciona. */
  bodySelector?: string;
  /** Dependencias extra que obligan a remedir (p. ej. contenido/título). */
  deps?: DependencyList;
}) {
  useLayoutEffect(() => {
    if (!open) return;
    const panel = panelRef.current;
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
      panel.dataset.placement = openAbove ? "top" : "bottom";
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
    const body = bodySelector ? panel.querySelector(bodySelector) : null;
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
    // eslint-disable-next-line react-hooks/exhaustive-deps -- deps extra explícitas del consumidor
  }, [open, anchorRef, panelRef, bodySelector, ...deps]);
}
