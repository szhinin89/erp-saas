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

describe("ZHTabBar — distribución responsive automática (UIUX-GLOBAL-TABS-DEFAULT-01)", () => {
  type Id = "a" | "b" | "c";
  const tabs: ZHTab<Id>[] = [
    { id: "a", label: "Resumen", icon: "bar_chart_4_bars" },
    { id: "b", label: "Listado", icon: "view_list" },
    { id: "c", label: "Nuevo proveedor", mobileLabel: "Nuevo", icon: "add_box" },
  ];
  const make = (count: number) =>
    Array.from({ length: count }, (_, i) => ({ id: String(i), label: `Destino ${i}`, mobileLabel: `D${i}`, icon: "add" }));
  const R3 = "prd-tabs--responsive-three";

  it("con 3 pestañas aplica el patrón sin prop, con nombre accesible completo e icono decorativo", () => {
    const onChange = vi.fn();
    render(<ZHTabBar tabs={tabs} activeTab="a" onChange={onChange} ariaLabel="Secciones" />);
    const tablist = screen.getByRole("tablist", { name: "Secciones" });
    expect(tablist.classList.contains(R3)).toBe(true);
    const tab = screen.getByRole("tab", { name: "Nuevo proveedor" });
    expect(tab.querySelector(".prd-tab-label--full")?.textContent).toBe("Nuevo proveedor");
    expect(tab.querySelector(".prd-tab-label--mobile")?.textContent).toBe("Nuevo");
    expect(tab.querySelector(".prd-tab-label--mobile")?.getAttribute("aria-hidden")).toBe("true");
    expect(tab.querySelector(".prd-tab-icon")?.getAttribute("aria-hidden")).toBe("true");
    fireEvent.click(tab);
    expect(onChange).toHaveBeenCalledExactlyOnceWith("c");
  });

  it("sin mobileLabel conserva la etiqueta completa como texto visible y nombre", () => {
    render(<ZHTabBar tabs={tabs} activeTab="a" onChange={() => {}} />);
    const tab = screen.getByRole("tab", { name: "Listado" });
    expect(tab.querySelector(".prd-tab-label--mobile")).toBeNull();
    expect(tab.textContent).toContain("Listado");
  });

  it.each([2, 4, 6])("con %i pestañas conserva el render estándar (sin etiquetas móviles)", (count) => {
    const { container } = render(<ZHTabBar tabs={make(count)} activeTab="0" onChange={() => {}} />);
    const tablist = screen.getByRole("tablist");
    expect(tablist.className).toBe("prd-tabs");
    expect(container.querySelector(".prd-tab-label--mobile, .prd-tab-label--full")).toBeNull();
    expect(screen.getAllByRole("tab")).toHaveLength(count);
    screen.getAllByRole("tab").forEach((t, i) => {
      expect(t.getAttribute("aria-label")).toBeNull();
      expect(t.textContent).toContain(`Destino ${i}`);
    });
  });

  it("variant=\"standard\" excluye explícitamente el patrón con 3 pestañas", () => {
    const { container, rerender } = render(<ZHTabBar tabs={make(3)} activeTab="0" onChange={() => {}} variant="standard" />);
    expect(screen.getByRole("tablist").className).toBe("prd-tabs");
    expect(container.querySelector(".prd-tab-label--mobile")).toBeNull();
    const markup = container.innerHTML;
    rerender(<ZHTabBar tabs={make(4)} activeTab="0" onChange={() => {}} />);
    rerender(<ZHTabBar tabs={make(3)} activeTab="0" onChange={() => {}} variant="standard" />);
    expect(container.innerHTML).toBe(markup);
  });

  it("fill se combina con el patrón sin perder su modificador", () => {
    render(<ZHTabBar tabs={tabs} activeTab="a" onChange={() => {}} fill />);
    expect(screen.getByRole("tablist").className).toBe(`prd-tabs prd-tabs--fill ${R3}`);
  });

  it("cambio dinámico 3 → 6 → 3 (p. ej. por permisos o edición) alterna el patrón y conserva foco y selección", () => {
    const six = [...tabs, { id: "d" as Id, label: "Productos" }, { id: "e" as Id, label: "Excepciones" }, { id: "f" as Id, label: "Clientes" }] as ZHTab<string>[];
    const { rerender } = render(<ZHTabBar<string> tabs={tabs} activeTab="c" onChange={() => {}} />);
    const nuevo = screen.getByRole("tab", { name: "Nuevo proveedor" });
    nuevo.focus();
    rerender(<ZHTabBar<string> tabs={six} activeTab="c" onChange={() => {}} />);
    expect(screen.getByRole("tablist").classList.contains(R3)).toBe(false);
    expect(screen.getAllByRole("tab")).toHaveLength(6);
    // Render estándar: sin aria-label propio; el mismo nodo conserva foco y selección.
    const nuevoSix = screen.getAllByRole("tab")[2];
    expect(nuevoSix).toBe(nuevo);
    expect(document.activeElement).toBe(nuevo);
    expect(nuevoSix.getAttribute("aria-selected")).toBe("true");
    expect(nuevoSix.querySelector(".prd-tab-label--mobile")).toBeNull();
    rerender(<ZHTabBar<string> tabs={tabs} activeTab="c" onChange={() => {}} />);
    expect(screen.getByRole("tablist").classList.contains(R3)).toBe(true);
    expect(document.activeElement).toBe(nuevo);
  });

  it("cambio dinámico 2 → 3 (permisos) activa el patrón sin alterar el nombre de las existentes", () => {
    const two = tabs.slice(0, 2);
    const { rerender } = render(<ZHTabBar tabs={two} activeTab="a" onChange={() => {}} />);
    expect(screen.getByRole("tablist").classList.contains(R3)).toBe(false);
    rerender(<ZHTabBar tabs={tabs} activeTab="a" onChange={() => {}} />);
    expect(screen.getByRole("tablist").classList.contains(R3)).toBe(true);
    expect(screen.getAllByRole("tab").map((t) => t.getAttribute("aria-label") ?? t.textContent)).toEqual(["Resumen", "Listado", "Nuevo proveedor"]);
  });

  it("conserva selección, disabled, inert y botones nativos enfocables", () => {
    const onChange = vi.fn();
    render(<ZHTabBar tabs={tabs.map(t => ({ ...t, disabled: t.id === "b", inert: t.id === "a" }))} activeTab="a" onChange={onChange} />);
    const active = screen.getByRole("tab", { name: "Resumen" });
    const disabled = screen.getByRole("tab", { name: "Listado" });
    expect(active.tagName).toBe("BUTTON");
    expect(active.getAttribute("type")).toBe("button");
    expect(active.getAttribute("aria-selected")).toBe("true");
    expect(disabled.getAttribute("aria-disabled")).toBe("true");
    expect((disabled as HTMLButtonElement).disabled).toBe(true);
    fireEvent.click(active);
    fireEvent.click(disabled);
    expect(onChange).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("tab", { name: "Nuevo proveedor" }));
    expect(onChange).toHaveBeenCalledExactlyOnceWith("c");
  });
  describe("barra desplazable (ZH-DS-TABBAR-RESPONSIVE-01)", () => {
    const five: ZHTab<string>[] = ["Uno", "Dos", "Tres", "Cuatro", "Cinco"].map((label) => ({ id: label, label }));
    // jsdom no calcula layout: se simula una barra de 200 px con pestañas de 100 px.
    function layout(list: HTMLElement, scrollWidth: number) {
      Object.defineProperty(list, "clientWidth", { configurable: true, value: 200 });
      Object.defineProperty(list, "scrollWidth", { configurable: true, value: scrollWidth });
      Array.from(list.querySelectorAll<HTMLElement>("[role='tab']")).forEach((tab, i) => {
        Object.defineProperty(tab, "offsetLeft", { configurable: true, value: i * 100 });
        Object.defineProperty(tab, "offsetWidth", { configurable: true, value: 100 });
      });
    }

    it("desplaza solo la barra para mostrar la pestaña activa y la enfocada por teclado", () => {
      const { rerender } = render(<ZHTabBar tabs={five} activeTab="Uno" onChange={() => {}} />);
      const list = screen.getByRole("tablist");
      layout(list, 500);
      rerender(<ZHTabBar tabs={five} activeTab="Cinco" onChange={() => {}} />);
      expect(list.scrollLeft).toBe(300); // Cinco (400–500) queda completa en 300–500
      fireEvent.focus(screen.getByRole("tab", { name: "Dos" }));
      expect(list.scrollLeft).toBe(100); // Dos (100–200) vuelve a la vista
    });

    it("sin desbordamiento (escritorio) no toca el scroll", () => {
      const { rerender } = render(<ZHTabBar tabs={five} activeTab="Uno" onChange={() => {}} />);
      const list = screen.getByRole("tablist");
      layout(list, 200);
      rerender(<ZHTabBar tabs={five} activeTab="Cinco" onChange={() => {}} />);
      fireEvent.focus(screen.getByRole("tab", { name: "Cuatro" }));
      expect(list.scrollLeft).toBe(0);
    });
  });
});
