// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { ZHLocaleProvider, useZHLocale } from "./ZHLocaleProvider";

afterEach(() => {
  cleanup();
});

function LocaleProbe() {
  const locale = useZHLocale();
  return <span>{locale ?? "none"}</span>;
}

describe("ZHLocaleProvider / useZHLocale", () => {
  it("useZHLocale devuelve undefined sin provider", () => {
    render(<LocaleProbe />);
    expect(screen.getByText("none")).toBeTruthy();
  });

  it("useZHLocale devuelve el locale del provider", () => {
    render(
      <ZHLocaleProvider locale="en-US">
        <LocaleProbe />
      </ZHLocaleProvider>,
    );
    expect(screen.getByText("en-US")).toBeTruthy();
  });
});
