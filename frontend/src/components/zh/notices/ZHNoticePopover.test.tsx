// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useRef } from "react";
import { ZHNoticePopover } from "./ZHNoticePopover";

// Posicionamiento de ZHNoticePopover: modo `anchored` (useAnchoredOverlayPosition — mismo
// mecanismo que ZHHelpPopover) y modo por defecto sin cambios para los consumidores existentes.

let width = 1200, height = 800, left = 100, top = 200, contentHeight = 240;
const rect = (x: number, y: number, w: number, h: number) =>
  ({ x, y, left: x, top: y, right: x + w, bottom: y + h, width: w, height: h, toJSON: () => ({}) });

function Harness({ anchored = true, close = vi.fn() }: { anchored?: boolean; close?: () => void }) {
  const ref = useRef<HTMLButtonElement>(null);
  return (
    <div className="scroll-container">
      <button ref={ref}>Etiqueta</button>
      <ZHNoticePopover open onClose={close} anchorRef={ref} titleId="t" title="Incidencias" severity="danger" anchored={anchored}>
        <button type="button">Recalcular desde XML</button>
      </ZHNoticePopover>
    </div>
  );
}
const panel = () => screen.getByRole("dialog");
const box = () => panel().getBoundingClientRect();
const inside = () => {
  const r = box();
  expect(r.left).toBeGreaterThanOrEqual(12);
  expect(r.top).toBeGreaterThanOrEqual(12);
  expect(r.right).toBeLessThanOrEqual(width - 12);
  expect(r.bottom).toBeLessThanOrEqual(height - 12);
};

beforeEach(() => {
  width = 1200; height = 800; left = 100; top = 200; contentHeight = 240;
  vi.spyOn(document.documentElement, "clientWidth", "get").mockImplementation(() => width);
  vi.spyOn(document.documentElement, "clientHeight", "get").mockImplementation(() => height);
  vi.spyOn(HTMLElement.prototype, "scrollHeight", "get").mockImplementation(() => contentHeight);
  vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(function (this: HTMLElement) {
    if (this.classList.contains("zh-notice-popover"))
      return rect(parseFloat(this.style.left) || 0, parseFloat(this.style.top) || 0,
        Math.min(320, parseFloat(this.style.maxWidth) || 320), Math.min(contentHeight, parseFloat(this.style.maxHeight) || 800));
    return rect(left, top, 180, 20);
  });
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => { callback(0); return 1; });
  vi.stubGlobal("cancelAnimationFrame", vi.fn());
  vi.stubGlobal("ResizeObserver", class { observe = vi.fn(); disconnect = vi.fn(); });
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

describe("ZHNoticePopover anchored", () => {
  it("abre debajo de la etiqueta cuando hay espacio (primera línea)", () => {
    render(<Harness />);
    expect(panel().dataset.placement).toBe("bottom");
    expect(box().top).toBe(top + 20 + 8);
    expect(box().left).toBe(left);
    expect(panel().style.visibility).toBe("visible");
    expect(panel().className).toContain("zh-notice-popover--anchored");
    inside();
  });

  it("abre encima de la etiqueta cuando no cabe debajo (última línea)", () => {
    top = 700;
    render(<Harness />);
    expect(panel().dataset.placement).toBe("top");
    expect(box().bottom).toBe(top - 8);
    inside();
  });

  it("sigue a la etiqueta al hacer scroll en un contenedor interno (listener en captura)", () => {
    const { container } = render(<Harness />);
    top = 120;
    fireEvent.scroll(container.querySelector(".scroll-container")!);
    expect(box().top).toBe(120 + 20 + 8);
    top = 690;
    fireEvent.scroll(window);
    expect(panel().dataset.placement).toBe("top");
    expect(box().bottom).toBe(690 - 8);
  });

  it("no desborda horizontalmente en el borde derecho ni en móvil", () => {
    left = 1150;
    render(<Harness />);
    inside();
    cleanup();
    width = 390; height = 700; left = 300; contentHeight = 900;
    render(<Harness />);
    inside();
    expect(panel().style.maxWidth).toBe("320px");
  });

  it("repositions and clamps on resize while open", () => {
    render(<Harness />);
    width = 360; height = 640; left = 310; top = 550;
    fireEvent(window, new Event("resize"));
    expect(panel().dataset.placement).toBe("top");
    inside();
  });

  it("conserva Escape (devuelve el foco) y clic exterior", () => {
    const close = vi.fn();
    render(<Harness close={close} />);
    fireEvent.pointerDown(screen.getByRole("button", { name: "Recalcular desde XML" }));
    expect(close).not.toHaveBeenCalled();
    fireEvent.keyDown(window, { key: "Escape" });
    expect(close).toHaveBeenCalledTimes(1);
    expect(document.activeElement?.textContent).toBe("Etiqueta");
    fireEvent.pointerDown(document.body);
    expect(close).toHaveBeenCalledTimes(2);
  });
});

describe("ZHNoticePopover por defecto (consumidores existentes)", () => {
  it("mantiene el cálculo único bajo el trigger, sin modo anclado", () => {
    render(<Harness anchored={false} />);
    expect(panel().className).not.toContain("zh-notice-popover--anchored");
    expect(panel().style.top).toBe(`${top + 20 + 8}px`);
    expect(panel().style.left).toBe(`${left}px`);
    expect(panel().style.visibility).toBe("");
    expect(panel().dataset.placement).toBeUndefined();
  });
});
