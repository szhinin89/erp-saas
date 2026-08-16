// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { ZHFieldLabel } from "./ZHFieldLabel";

afterEach(() => {
  cleanup();
});

describe("ZHFieldLabel", () => {
  it("renderiza children", () => {
    render(<ZHFieldLabel>Nombre</ZHFieldLabel>);
    expect(screen.getByText("Nombre")).toBeTruthy();
  });

  it("aplica size md por defecto", () => {
    render(<ZHFieldLabel>Nombre</ZHFieldLabel>);
    expect(screen.getByText("Nombre").className.includes("zh-field-label--md")).toBe(
      true,
    );
  });

  it("size=sm aplica .zh-field-label--sm", () => {
    render(<ZHFieldLabel size="sm">CANTIDAD</ZHFieldLabel>);
    expect(
      screen.getByText("CANTIDAD").className.includes("zh-field-label--sm"),
    ).toBe(true);
  });

  it("size=md aplica .zh-field-label--md", () => {
    render(<ZHFieldLabel size="md">Nombre</ZHFieldLabel>);
    expect(
      screen.getByText("Nombre").className.includes("zh-field-label--md"),
    ).toBe(true);
  });

  it("required renderiza asterisco con .zh-field-label__required", () => {
    render(<ZHFieldLabel required>Nombre</ZHFieldLabel>);
    const required = document.querySelector(".zh-field-label__required");
    expect(required).toBeTruthy();
    expect(required?.textContent).toBe("*");
  });

  it('optional renderiza "(opcional)" con .zh-field-label__optional', () => {
    render(<ZHFieldLabel optional>Nombre</ZHFieldLabel>);
    const optional = document.querySelector(".zh-field-label__optional");
    expect(optional).toBeTruthy();
    expect(optional?.textContent).toBe("(opcional)");
  });

  it("required tiene prioridad sobre optional si ambos están true", () => {
    render(
      <ZHFieldLabel required optional>
        Nombre
      </ZHFieldLabel>,
    );
    expect(document.querySelector(".zh-field-label__required")).toBeTruthy();
    expect(document.querySelector(".zh-field-label__optional")).toBeFalsy();
  });

  it('optional=true con optionalLabel="(optional)" muestra "(optional)"', () => {
    render(
      <ZHFieldLabel optional optionalLabel="(optional)">
        Name
      </ZHFieldLabel>,
    );
    const optional = document.querySelector(".zh-field-label__optional");
    expect(optional).toBeTruthy();
    expect(optional?.textContent).toBe("(optional)");
  });

  it("optionalLabel acepta ReactNode", () => {
    render(
      <ZHFieldLabel optional optionalLabel={<em data-testid="optional-node">opt</em>}>
        Nombre
      </ZHFieldLabel>,
    );
    const node = screen.getByTestId("optional-node");
    expect(node).toBeTruthy();
    expect(
      document.querySelector(".zh-field-label__optional")?.contains(node),
    ).toBe(true);
  });

  it("required=true + optional=true no muestra optionalLabel", () => {
    render(
      <ZHFieldLabel required optional optionalLabel="(optional)">
        Nombre
      </ZHFieldLabel>,
    );
    expect(document.querySelector(".zh-field-label__required")).toBeTruthy();
    expect(document.querySelector(".zh-field-label__optional")).toBeFalsy();
    expect(screen.queryByText("(optional)")).toBeNull();
  });

  it("className adicional se combina correctamente", () => {
    render(<ZHFieldLabel className="custom-class">Nombre</ZHFieldLabel>);
    const label = screen.getByText("Nombre");
    expect(label.className.includes("zh-field-label")).toBe(true);
    expect(label.className.includes("custom-class")).toBe(true);
  });

  it("htmlFor se pasa al <label>", () => {
    render(<ZHFieldLabel htmlFor="name-input">Nombre</ZHFieldLabel>);
    expect(screen.getByText("Nombre").getAttribute("for")).toBe("name-input");
  });
});
