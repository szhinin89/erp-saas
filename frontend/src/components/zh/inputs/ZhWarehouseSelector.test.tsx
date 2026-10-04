// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { ZhWarehouseSelector } from "./ZhWarehouseSelector";
import type { WarehouseDto } from "../../../modules/inventory/warehouses/api/warehouseService";

const warehouses: WarehouseDto[] = [{
  id: "main", name: "Principal", branchId: "branch", code: null,
  storageType: null, address: null, phone: null, email: null, manager: null,
  latitude: null, longitude: null, capacity: null, dailyDispatchGoal: null, isActive: true,
}];

describe("ZhWarehouseSelector viewport overlay", () => {
  beforeEach(() => {
    vi.spyOn(document.documentElement, "clientWidth", "get").mockReturnValue(1280);
    vi.spyOn(document.documentElement, "clientHeight", "get").mockReturnValue(720);
    vi.spyOn(HTMLElement.prototype, "offsetHeight", "get").mockReturnValue(36);
    vi.spyOn(HTMLElement.prototype, "scrollHeight", "get").mockReturnValue(280);
    vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockReturnValue({
      left: 1120, right: 1260, top: 660, bottom: 690, width: 140, height: 30,
      x: 1120, y: 660, toJSON: () => ({}),
    });
  });
  afterEach(() => { cleanup(); vi.restoreAllMocks(); });

  it("portals outside the scroll container, shifts left and opens above near viewport edges", () => {
    const { container } = render(<ZhWarehouseSelector value="main" onChange={vi.fn()} fallbackWarehouses={warehouses} />);
    fireEvent.click(screen.getByRole("button"));
    const panel = document.querySelector<HTMLElement>(".zh-wh-selector__panel")!;
    expect(panel.parentElement).toBe(document.body);
    expect(container.contains(panel)).toBe(false);
    expect(panel.style.width).toBe("260px");
    expect(panel.style.left).toBe("1012px");
    expect(panel.style.top).toBe("338px");
    expect(panel.style.maxHeight).toBe("318px");
  });

  it("focuses without scrolling, accepts keyboard selection, and does not treat portal clicks as outside", () => {
    const focus = vi.spyOn(HTMLElement.prototype, "focus");
    const onChange = vi.fn();
    render(<ZhWarehouseSelector value={null} onChange={onChange} fallbackWarehouses={warehouses} />);
    fireEvent.click(screen.getByRole("button"));
    const search = screen.getByPlaceholderText("Buscar bodega...");
    expect(document.activeElement).toBe(search);
    expect(focus).toHaveBeenCalledWith({ preventScroll: true });
    fireEvent.mouseDown(search);
    expect(document.querySelector(".zh-wh-selector__panel")).not.toBeNull();
    fireEvent.keyDown(search, { key: "ArrowDown" });
    fireEvent.keyDown(search, { key: "Enter" });
    expect(onChange).toHaveBeenCalledWith("main", undefined);
    expect(document.querySelector(".zh-wh-selector__panel")).toBeNull();
  });

  it("closes empty results with Escape and returns focus without scrolling", () => {
    render(<ZhWarehouseSelector value={null} onChange={vi.fn()} fallbackWarehouses={[]} />);
    const trigger = screen.getByRole("button");
    fireEvent.click(trigger);
    fireEvent.keyDown(screen.getByPlaceholderText("Buscar bodega..."), { key: "Escape" });
    expect(document.querySelector(".zh-wh-selector__panel")).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it("caps width on narrow viewports and closes on outside click", () => {
    vi.spyOn(document.documentElement, "clientWidth", "get").mockReturnValue(240);
    render(<ZhWarehouseSelector value={null} onChange={vi.fn()} fallbackWarehouses={warehouses} />);
    fireEvent.click(screen.getByRole("button"));
    const panel = document.querySelector<HTMLElement>(".zh-wh-selector__panel")!;
    expect(panel.style.width).toBe("224px");
    expect(panel.style.left).toBe("8px");
    fireEvent.mouseDown(document.body);
    expect(document.querySelector(".zh-wh-selector__panel")).toBeNull();
  });
});
