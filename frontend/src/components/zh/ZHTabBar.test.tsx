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

describe("ZHTabBar responsive-three — opt-in", () => {
  const tabs: ZHTab<"a" | "b" | "c">[] = [
    { id: "a", label: "Resumen", icon: "bar_chart_4_bars" },
    { id: "b", label: "Listado", icon: "view_list" },
    { id: "c", label: "Nuevo proveedor", mobileLabel: "Nuevo", icon: "add_box" },
  ];

  it("mantiene nombre accesible completo e iconos decorativos con etiqueta breve", () => {
    const onChange = vi.fn();
    render(<ZHTabBar tabs={tabs} activeTab="a" onChange={onChange} variant="responsive-three" />);
    const tab = screen.getByRole("tab", { name: "Nuevo proveedor" });
    expect(screen.getByRole("tablist").classList.contains("prd-tabs--responsive-three")).toBe(true);
    expect(tab.querySelector(".prd-tab-label--mobile")?.textContent).toBe("Nuevo");
    expect(tab.querySelector(".prd-tab-label--mobile")?.getAttribute("aria-hidden")).toBe("true");
    expect(tab.querySelector(".prd-tab-icon")?.getAttribute("aria-hidden")).toBe("true");
    fireEvent.click(tab);
    expect(onChange).toHaveBeenCalledExactlyOnceWith("c");
  });

  it("no altera el markup default ni aplica etiquetas móviles sin la variante", () => {
    const { container, rerender } = render(<ZHTabBar tabs={tabs} activeTab="a" onChange={() => {}} fill />);
    const markup = container.innerHTML;
    expect(container.querySelector(".prd-tab-label--mobile")).toBeNull();
    expect(screen.getByRole("tablist").className).toBe("prd-tabs prd-tabs--fill");
    rerender(<ZHTabBar tabs={tabs} activeTab="a" onChange={() => {}} fill variant="default" />);
    expect(container.innerHTML).toBe(markup);
  });

  it.each([0, 1, 2, 4, 5])("ignora responsive-three con %i pestañas", (count) => {
    const otherTabs = Array.from({ length: count }, (_, i) => ({ id: String(i), label: `Destino ${i}`, mobileLabel: `D${i}` }));
    const { container, rerender } = render(<ZHTabBar tabs={otherTabs} activeTab="0" onChange={() => {}} />);
    const markup = container.innerHTML;
    rerender(<ZHTabBar tabs={otherTabs} activeTab="0" onChange={() => {}} variant="responsive-three" />);
    expect(container.innerHTML).toBe(markup);
  });

  it("conserva selección, disabled e inert en la variante", () => {
    const onChange = vi.fn();
    render(<ZHTabBar tabs={tabs.map(t => ({ ...t, disabled: t.id === "b", inert: t.id === "a" }))} activeTab="a" onChange={onChange} variant="responsive-three" />);
    const active = screen.getByRole("tab", { name: "Resumen" });
    const disabled = screen.getByRole("tab", { name: "Listado" });
    expect(active.getAttribute("aria-selected")).toBe("true");
    expect(disabled.getAttribute("aria-disabled")).toBe("true");
    expect((disabled as HTMLButtonElement).disabled).toBe(true);
    fireEvent.click(active);
    fireEvent.click(disabled);
    expect(onChange).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("tab", { name: "Nuevo proveedor" }));
    expect(onChange).toHaveBeenCalledExactlyOnceWith("c");
  });
});
