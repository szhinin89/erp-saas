// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { ZHInputGroup } from "./ZHInputGroup";

afterEach(() => {
  cleanup();
});

describe("ZHInputGroup", () => {
  it("renderiza children (input)", () => {
    render(
      <ZHInputGroup prefix="$">
        <input aria-label="costo" />
      </ZHInputGroup>,
    );
    expect(screen.getByLabelText("costo")).toBeTruthy();
  });

  it("renderiza prefix cuando se provee", () => {
    render(
      <ZHInputGroup prefix="$">
        <input aria-label="costo" />
      </ZHInputGroup>,
    );
    expect(document.querySelector(".zh-input-group__prefix")?.textContent).toBe("$");
  });

  it("no renderiza prefix si no se provee", () => {
    render(
      <ZHInputGroup>
        <input aria-label="costo" />
      </ZHInputGroup>,
    );
    expect(document.querySelector(".zh-input-group__prefix")).toBeFalsy();
  });

  it("renderiza suffix cuando se provee", () => {
    render(
      <ZHInputGroup suffix="kg">
        <input aria-label="peso" />
      </ZHInputGroup>,
    );
    expect(document.querySelector(".zh-input-group__suffix")?.textContent).toBe("kg");
  });

  it("aplica la clase .zh-input-group al wrapper", () => {
    const { container } = render(
      <ZHInputGroup prefix="$">
        <input aria-label="costo" />
      </ZHInputGroup>,
    );
    expect(container.querySelector(".zh-input-group")).toBeTruthy();
  });

  it("className adicional se combina correctamente", () => {
    const { container } = render(
      <ZHInputGroup prefix="$" className="custom-class">
        <input aria-label="costo" />
      </ZHInputGroup>,
    );
    expect(container.querySelector(".zh-input-group")?.className.includes("custom-class")).toBe(
      true,
    );
  });

  it("no usa style inline", () => {
    const { container } = render(
      <ZHInputGroup prefix="$">
        <input aria-label="costo" />
      </ZHInputGroup>,
    );
    expect(container.querySelector(".zh-input-group")?.getAttribute("style")).toBeNull();
  });
});
