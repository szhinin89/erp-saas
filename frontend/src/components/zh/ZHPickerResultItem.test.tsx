// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { ZHPickerResultItem } from "./ZHPickerResultItem";

afterEach(() => {
  cleanup();
});

describe("ZHPickerResultItem", () => {
  it("renderiza title", () => {
    render(<ZHPickerResultItem title="Juan Pérez" />);
    expect(screen.getByText("Juan Pérez")).toBeTruthy();
  });

  it("renderiza subtitle", () => {
    render(<ZHPickerResultItem title="Juan Pérez" subtitle="Cliente frecuente" />);
    expect(screen.getByText("Cliente frecuente")).toBeTruthy();
  });

  it("renderiza meta", () => {
    render(<ZHPickerResultItem title="Juan Pérez" meta="1710034065" />);
    expect(screen.getByText("1710034065")).toBeTruthy();
  });

  it("renderiza icon si se pasa", () => {
    const { container } = render(
      <ZHPickerResultItem title="Juan Pérez" icon="person" />,
    );
    const icon = container.querySelector(".zh-picker-result-item__icon");
    expect(icon).toBeTruthy();
    expect(icon?.textContent).toBe("person");
  });

  it("no renderiza ícono cuando no se pasa", () => {
    const { container } = render(<ZHPickerResultItem title="Juan Pérez" />);
    expect(container.querySelector(".zh-picker-result-item__icon")).toBeNull();
  });

  it("click llama a onClick", () => {
    const onClick = vi.fn();
    render(<ZHPickerResultItem title="Juan Pérez" onClick={onClick} />);
    fireEvent.click(screen.getByText("Juan Pérez"));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("disabled bloquea el click", () => {
    const onClick = vi.fn();
    render(<ZHPickerResultItem title="Juan Pérez" onClick={onClick} disabled />);
    const btn = screen.getByText("Juan Pérez").closest("button")!;
    expect(btn.disabled).toBe(true);
    fireEvent.click(btn);
    expect(onClick).not.toHaveBeenCalled();
  });

  it("selected=true aplica clase --selected y aria-selected=true", () => {
    render(<ZHPickerResultItem title="Juan Pérez" selected />);
    const btn = screen.getByText("Juan Pérez").closest("button")!;
    expect(btn.className.includes("zh-picker-result-item--selected")).toBe(true);
    expect(btn.getAttribute("aria-selected")).toBe("true");
  });

  it("selected=false (default) no aplica la clase y aria-selected=false", () => {
    render(<ZHPickerResultItem title="Juan Pérez" />);
    const btn = screen.getByText("Juan Pérez").closest("button")!;
    expect(btn.className.includes("zh-picker-result-item--selected")).toBe(false);
    expect(btn.getAttribute("aria-selected")).toBe("false");
  });

  it("className se combina con la clase base", () => {
    render(<ZHPickerResultItem title="Juan Pérez" className="custom-class" />);
    const btn = screen.getByText("Juan Pérez").closest("button")!;
    expect(btn.className.includes("zh-picker-result-item")).toBe(true);
    expect(btn.className.includes("custom-class")).toBe(true);
  });

  it('type default es "button"', () => {
    render(<ZHPickerResultItem title="Juan Pérez" />);
    const btn = screen.getByText("Juan Pérez").closest("button")!;
    expect(btn.getAttribute("type")).toBe("button");
  });

  it("no hay estilos inline", () => {
    render(
      <ZHPickerResultItem
        title="Juan Pérez"
        subtitle="Cliente frecuente"
        meta="1710034065"
        icon="person"
        selected
      />,
    );
    const btn = screen.getByText("Juan Pérez").closest("button")!;
    btn.querySelectorAll("*").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
    expect(btn.getAttribute("style")).toBeNull();
  });
});
