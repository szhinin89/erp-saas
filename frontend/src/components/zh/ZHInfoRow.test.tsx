// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { ZHInfoRow } from "./ZHInfoRow";

afterEach(() => {
  cleanup();
});

describe("ZHInfoRow", () => {
  it("renderiza label y value", () => {
    render(<ZHInfoRow label="Cantidad" value="3.0000" />);
    expect(screen.getByText("Cantidad")).toBeTruthy();
    expect(screen.getByText("3.0000")).toBeTruthy();
  });

  it("label y value aceptan ReactNode", () => {
    render(
      <ZHInfoRow
        label={<span data-testid="label-node">Costo</span>}
        value={<strong data-testid="value-node">$0.0000</strong>}
      />,
    );
    expect(screen.getByTestId("label-node")).toBeTruthy();
    expect(screen.getByTestId("value-node")).toBeTruthy();
  });

  it("aplica .zh-info-row por defecto sin --wide", () => {
    const { container } = render(<ZHInfoRow label="Costo" value="$0.00" />);
    const row = container.querySelector(".zh-info-row");
    expect(row).toBeTruthy();
    expect(row?.className.includes("zh-info-row--wide")).toBe(false);
  });

  it("wide aplica .zh-info-row--wide", () => {
    const { container } = render(<ZHInfoRow label="Presentación" value="—" wide />);
    expect(container.querySelector(".zh-info-row--wide")).toBeTruthy();
  });

  it("estructura label/value usa las clases __label/__value", () => {
    render(<ZHInfoRow label="Cantidad" value="3.0000" />);
    expect(document.querySelector(".zh-info-row__label")?.textContent).toBe(
      "Cantidad",
    );
    expect(document.querySelector(".zh-info-row__value")?.textContent).toBe(
      "3.0000",
    );
  });

  it("className adicional se combina correctamente", () => {
    const { container } = render(
      <ZHInfoRow label="Costo" value="$0.00" className="custom-class" />,
    );
    const row = container.querySelector(".zh-info-row");
    expect(row?.className.includes("custom-class")).toBe(true);
  });

  it("no usa style inline", () => {
    const { container } = render(<ZHInfoRow label="Costo" value="$0.00" />);
    expect(container.querySelector(".zh-info-row")?.getAttribute("style")).toBeNull();
  });
});
