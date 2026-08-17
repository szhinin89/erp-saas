// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { ZHToggleTile } from "./ZHToggleTile";

afterEach(() => {
  cleanup();
});

describe("ZHToggleTile", () => {
  it("renderiza title", () => {
    render(<ZHToggleTile title="Efectivo" />);
    expect(screen.getByText("Efectivo")).toBeTruthy();
  });

  it("renderiza subtitle", () => {
    render(<ZHToggleTile title="Efectivo" subtitle="$10.00" />);
    expect(screen.getByText("$10.00")).toBeTruthy();
  });

  it("renderiza icon si se pasa", () => {
    const { container } = render(
      <ZHToggleTile title="Efectivo" icon="payments" />,
    );
    const icon = container.querySelector(".zh-toggle-tile__icon");
    expect(icon).toBeTruthy();
    expect(icon?.textContent).toBe("payments");
  });

  it("no renderiza ícono cuando no se pasa", () => {
    const { container } = render(<ZHToggleTile title="Efectivo" />);
    expect(container.querySelector(".zh-toggle-tile__icon")).toBeNull();
  });

  it("active=true aplica clase --active y aria-pressed=true", () => {
    render(<ZHToggleTile title="Efectivo" active />);
    const btn = screen.getByText("Efectivo").closest("button")!;
    expect(btn.className.includes("zh-toggle-tile--active")).toBe(true);
    expect(btn.getAttribute("aria-pressed")).toBe("true");
  });

  it("active=false (default) no aplica la clase y aria-pressed=false", () => {
    render(<ZHToggleTile title="Efectivo" />);
    const btn = screen.getByText("Efectivo").closest("button")!;
    expect(btn.className.includes("zh-toggle-tile--active")).toBe(false);
    expect(btn.getAttribute("aria-pressed")).toBe("false");
  });

  it("click llama a onClick", () => {
    const onClick = vi.fn();
    render(<ZHToggleTile title="Efectivo" onClick={onClick} />);
    fireEvent.click(screen.getByText("Efectivo"));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("disabled bloquea el click", () => {
    const onClick = vi.fn();
    render(<ZHToggleTile title="Efectivo" onClick={onClick} disabled />);
    const btn = screen.getByText("Efectivo").closest("button")!;
    expect(btn.disabled).toBe(true);
    fireEvent.click(btn);
    expect(onClick).not.toHaveBeenCalled();
  });

  it("className se combina con la clase base", () => {
    render(<ZHToggleTile title="Efectivo" className="custom-class" />);
    const btn = screen.getByText("Efectivo").closest("button")!;
    expect(btn.className.includes("zh-toggle-tile")).toBe(true);
    expect(btn.className.includes("custom-class")).toBe(true);
  });

  it('type default es "button"', () => {
    render(<ZHToggleTile title="Efectivo" />);
    const btn = screen.getByText("Efectivo").closest("button")!;
    expect(btn.getAttribute("type")).toBe("button");
  });

  it("no hay estilos inline", () => {
    render(
      <ZHToggleTile
        title="Efectivo"
        subtitle="$10.00"
        icon="payments"
        active
      />,
    );
    const btn = screen.getByText("Efectivo").closest("button")!;
    btn.querySelectorAll("*").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
    expect(btn.getAttribute("style")).toBeNull();
  });
});
