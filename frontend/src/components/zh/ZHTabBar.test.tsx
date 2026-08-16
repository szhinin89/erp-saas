// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { ZHTabBar, type ZHTab } from "./ZHTabBar";

afterEach(() => {
  cleanup();
});

const baseTabs: ZHTab<"a" | "b">[] = [
  { id: "a", label: "Tab A" },
  { id: "b", label: "Tab B" },
];

describe("ZHTabBar", () => {
  it("renderiza tabs normales", () => {
    render(<ZHTabBar tabs={baseTabs} activeTab="a" onChange={() => {}} />);
    expect(screen.getByText("Tab A")).toBeTruthy();
    expect(screen.getByText("Tab B")).toBeTruthy();
  });

  it("click en tab normal dispara onChange con id", () => {
    const onChange = vi.fn();
    render(<ZHTabBar tabs={baseTabs} activeTab="a" onChange={onChange} />);
    fireEvent.click(screen.getByText("Tab B"));
    expect(onChange).toHaveBeenCalledWith("b");
  });

  it("tab active tiene aria-selected=true", () => {
    render(<ZHTabBar tabs={baseTabs} activeTab="a" onChange={() => {}} />);
    expect(screen.getByText("Tab A").getAttribute("aria-selected")).toBe(
      "true",
    );
    expect(screen.getByText("Tab B").getAttribute("aria-selected")).toBe(
      "false",
    );
  });

  it("tab disabled no dispara onChange y tiene aria-disabled=true", () => {
    const onChange = vi.fn();
    const tabs: ZHTab<"a" | "b">[] = [
      { id: "a", label: "Tab A" },
      { id: "b", label: "Tab B", disabled: true },
    ];
    render(<ZHTabBar tabs={tabs} activeTab="a" onChange={onChange} />);
    const tabB = screen.getByText("Tab B");
    expect(tabB.getAttribute("aria-disabled")).toBe("true");
    fireEvent.click(tabB);
    expect(onChange).not.toHaveBeenCalled();
  });

  it("tab inert no dispara onChange", () => {
    const onChange = vi.fn();
    const tabs: ZHTab<"a" | "b">[] = [
      { id: "a", label: "Tab A", inert: true },
      { id: "b", label: "Tab B" },
    ];
    render(<ZHTabBar tabs={tabs} activeTab="a" onChange={onChange} />);
    fireEvent.click(screen.getByText("Tab A"));
    expect(onChange).not.toHaveBeenCalled();
  });

  it("tab inert puede estar active sin verse disabled", () => {
    const tabs: ZHTab<"a" | "b">[] = [
      { id: "a", label: "Tab A", inert: true },
      { id: "b", label: "Tab B" },
    ];
    render(<ZHTabBar tabs={tabs} activeTab="a" onChange={() => {}} />);
    const tabA = screen.getByText("Tab A");
    expect(tabA.className.includes("prd-tab-btn--active")).toBe(true);
    expect(tabA.getAttribute("aria-disabled")).toBeNull();
    expect((tabA as HTMLButtonElement).disabled).toBe(false);
  });

  it("compatibilidad: sin inert/disabled el comportamiento anterior sigue igual", () => {
    const onChange = vi.fn();
    render(<ZHTabBar tabs={baseTabs} activeTab="a" onChange={onChange} />);
    fireEvent.click(screen.getByText("Tab B"));
    expect(onChange).toHaveBeenCalledWith("b");
    expect(onChange).toHaveBeenCalledTimes(1);
  });

  it("no hay style inline", () => {
    const tabs: ZHTab<"a" | "b">[] = [
      { id: "a", label: "Tab A", inert: true },
      { id: "b", label: "Tab B", disabled: true },
    ];
    const { container } = render(
      <ZHTabBar tabs={tabs} activeTab="a" onChange={() => {}} />,
    );
    container.querySelectorAll("*").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});
