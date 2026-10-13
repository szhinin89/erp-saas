// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createRef, useRef } from "react";
import { ZHHelpPopover } from "./ZHHelpPopover";
import { ZHSectionHelp } from "./ZHSectionHelp";
import { ZHFieldHelp } from "./ZHFieldHelp";
import { HELP_KEYS } from "../../../help";

let width = 800, height = 600, left = 400, top = 200, contentHeight = 180;
const rect = (x: number, y: number, w: number, h: number) => ({ x, y, left: x, top: y, right: x + w, bottom: y + h, width: w, height: h, toJSON: () => ({}) });
let resize: ResizeObserverCallback;
let observer: { observe: ReturnType<typeof vi.fn>; disconnect: ReturnType<typeof vi.fn> };
function Harness({ close = vi.fn(), children = "Help" }: { close?: () => void; children?: string }) {
  const ref = useRef<HTMLButtonElement>(null);
  return <><button ref={ref}>Anchor</button><ZHHelpPopover open onClose={close} anchorRef={ref} titleId="title" title="Title" closeOnMouseLeave>{children}</ZHHelpPopover></>;
}
const panel = () => screen.getByRole("dialog");
const inside = () => {
  const r = panel().getBoundingClientRect();
  expect(r.left).toBeGreaterThanOrEqual(12);
  expect(r.top).toBeGreaterThanOrEqual(12);
  expect(r.right).toBeLessThanOrEqual(width - 12);
  expect(r.bottom).toBeLessThanOrEqual(height - 12);
};
beforeEach(() => {
  width = 800; height = 600; left = 400; top = 200; contentHeight = 180;
  vi.spyOn(document.documentElement, "clientWidth", "get").mockImplementation(() => width);
  vi.spyOn(document.documentElement, "clientHeight", "get").mockImplementation(() => height);
  vi.spyOn(HTMLElement.prototype, "scrollHeight", "get").mockImplementation(() => contentHeight);
  vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(function(this: HTMLElement) {
    if (this.classList.contains("zh-help-popover")) return rect(parseFloat(this.style.left) || 0, parseFloat(this.style.top) || 0, Math.min(320, parseFloat(this.style.maxWidth) || 320), Math.min(contentHeight, parseFloat(this.style.maxHeight) || 600));
    return rect(left, top, 20, 20);
  });
  observer = { observe: vi.fn(), disconnect: vi.fn() };
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => { callback(0); return 1; });
  vi.stubGlobal("cancelAnimationFrame", vi.fn());
  vi.stubGlobal("ResizeObserver", class { constructor(callback: ResizeObserverCallback) { resize = callback; } observe = observer.observe; disconnect = observer.disconnect; });
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
describe("help viewport positioning", () => {
  it.each([[400, 200], [780, 200], [0, 200], [400, 570]])("contains panel for anchor %i,%i", (x, y) => {
    left = x; top = y;
    const { container } = render(<Harness />);
    inside();
    expect(panel().parentElement).toBe(document.body);
    expect(container.contains(panel())).toBe(false);
    expect(panel().style.visibility).toBe("visible");
    if (y === 570) expect(parseFloat(panel().style.top)).toBeLessThan(y);
    if (x === 780) expect(panel().style.left).toBe("468px");
  });
  it("caps width and height on a narrow viewport with tall content", () => {
    width = 240; height = 300; left = 230; contentHeight = 1500;
    render(<Harness />); inside();
    expect(panel().style.maxWidth).toBe("216px");
  });
  it("repositions for resize, captured scroll, anchor and content observations", () => {
    render(<Harness />);
    width = 500; fireEvent(window, new Event("resize")); inside();
    top = 580; fireEvent(document.body, new Event("scroll")); inside();
    left = 0; act(() => resize([], {} as ResizeObserver)); inside();
    contentHeight = 1000; act(() => resize([], {} as ResizeObserver)); inside();
    expect(observer.observe).toHaveBeenCalledTimes(3);
    cleanup(); expect(observer.disconnect).toHaveBeenCalledOnce();
  });
  it("remeasures changed children", () => {
    const view = render(<Harness />);
    contentHeight = 500;
    view.rerender(<Harness>Changed content</Harness>); inside();
  });
  it("keeps inside clicks, Escape focus, outside touch and mouseleave", () => {
    const close = vi.fn(); render(<Harness close={close} />);
    fireEvent.pointerDown(panel()); expect(close).not.toHaveBeenCalled();
    fireEvent.keyDown(window, { key: "Escape" });
    expect(document.activeElement).toBe(screen.getByRole("button"));
    expect(close).toHaveBeenCalledTimes(1);
    fireEvent.pointerDown(document.body, { pointerType: "touch" });
    fireEvent.mouseLeave(panel()); fireEvent.mouseLeave(screen.getByRole("button"));
    expect(close).toHaveBeenCalledTimes(4);
  });
  it("keeps the unmeasured panel hidden and attaches mouseleave before an anchor is available", () => {
    const close = vi.fn();
    render(<ZHHelpPopover open anchorRef={createRef()} onClose={close} titleId="pending" title="Pending" closeOnMouseLeave>Help</ZHHelpPopover>);
    const pending = document.querySelector<HTMLElement>(".zh-help-popover")!;
    expect(pending.style.visibility).toBe("hidden");
    fireEvent.mouseLeave(pending); expect(close).toHaveBeenCalledOnce();
  });
  it.each([HELP_KEYS.PURCHASES_RECEPTION_OVERVIEW, HELP_KEYS.SALES_CUSTOMER_CONSUMER_FINAL])("preserves section hover, pin and keyboard for %s", (helpKey) => {
    render(<ZHSectionHelp helpKey={helpKey} />);
    const trigger = screen.getByRole("button");
    fireEvent.mouseEnter(trigger); inside();
    fireEvent.mouseLeave(panel()); expect(screen.queryByRole("dialog")).toBeNull();
    fireEvent.click(trigger); fireEvent.mouseLeave(panel()); inside();
    fireEvent.keyDown(window, { key: "Escape" });
    expect(screen.queryByRole("dialog")).toBeNull(); expect(document.activeElement).toBe(trigger);
    fireEvent.blur(trigger); fireEvent.focus(trigger); inside();
  });
  it.each([HELP_KEYS.PURCHASES_SUPPLIER, HELP_KEYS.SALES_PAYMENTS_SECTION])("keeps field consumer %s", (helpKey) => {
    render(<ZHFieldHelp helpKey={helpKey} />);
    fireEvent.click(screen.getByRole("button")); inside();
    fireEvent.pointerDown(document.body); expect(screen.queryByRole("dialog")).toBeNull();
  });
});
