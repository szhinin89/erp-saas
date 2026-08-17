// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { ZHDataValue } from "./ZHDataValue";

afterEach(() => {
  cleanup();
});

describe("ZHDataValue", () => {
  it("renderiza children", () => {
    render(<ZHDataValue>1.0000</ZHDataValue>);
    expect(screen.getByText("1.0000")).toBeTruthy();
  });

  it("aplica variant default por defecto", () => {
    render(<ZHDataValue>dato</ZHDataValue>);
    expect(screen.getByText("dato").className.includes("zh-data-value--default")).toBe(
      true,
    );
  });

  it("variant=muted aplica .zh-data-value--muted", () => {
    render(<ZHDataValue variant="muted">dato</ZHDataValue>);
    expect(screen.getByText("dato").className.includes("zh-data-value--muted")).toBe(
      true,
    );
  });

  it("variant=numeric aplica .zh-data-value--numeric", () => {
    render(<ZHDataValue variant="numeric">3.0000</ZHDataValue>);
    expect(
      screen.getByText("3.0000").className.includes("zh-data-value--numeric"),
    ).toBe(true);
  });

  it("variant=strong aplica .zh-data-value--strong", () => {
    render(<ZHDataValue variant="strong">100%</ZHDataValue>);
    expect(screen.getByText("100%").className.includes("zh-data-value--strong")).toBe(
      true,
    );
  });

  it("variant=code aplica .zh-data-value--code (única excepción monoespaciada)", () => {
    render(<ZHDataValue variant="code">CUBHUEVO</ZHDataValue>);
    expect(
      screen.getByText("CUBHUEVO").className.includes("zh-data-value--code"),
    ).toBe(true);
  });

  it("tone=default no agrega clase de tono", () => {
    render(<ZHDataValue>dato</ZHDataValue>);
    expect(screen.getByText("dato").className.includes("zh-data-value--tone-")).toBe(
      false,
    );
  });

  it("tone=success aplica .zh-data-value--tone-success", () => {
    render(<ZHDataValue tone="success">12%</ZHDataValue>);
    expect(
      screen.getByText("12%").className.includes("zh-data-value--tone-success"),
    ).toBe(true);
  });

  it("tone=danger aplica .zh-data-value--tone-danger", () => {
    render(<ZHDataValue tone="danger">-5%</ZHDataValue>);
    expect(
      screen.getByText("-5%").className.includes("zh-data-value--tone-danger"),
    ).toBe(true);
  });

  it("italic no agrega clase por defecto", () => {
    render(<ZHDataValue>dato</ZHDataValue>);
    expect(screen.getByText("dato").className.includes("zh-data-value--italic")).toBe(
      false,
    );
  });

  it("italic=true aplica .zh-data-value--italic", () => {
    render(<ZHDataValue italic>nota</ZHDataValue>);
    expect(screen.getByText("nota").className.includes("zh-data-value--italic")).toBe(
      true,
    );
  });

  it("className adicional se combina correctamente", () => {
    render(<ZHDataValue className="custom-class">dato</ZHDataValue>);
    const el = screen.getByText("dato");
    expect(el.className.includes("zh-data-value")).toBe(true);
    expect(el.className.includes("custom-class")).toBe(true);
  });

  it("no usa style inline", () => {
    render(<ZHDataValue>dato</ZHDataValue>);
    expect(screen.getByText("dato").getAttribute("style")).toBeNull();
  });
});
