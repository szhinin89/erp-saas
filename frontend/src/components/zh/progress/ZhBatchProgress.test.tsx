// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ZhBatchProgress, type ZhBatchProgressProps } from "./ZhBatchProgress";

afterEach(() => {
  cleanup();
});

function show(overrides: Partial<ZhBatchProgressProps> = {}) {
  render(
    <I18nProvider>
      <ZhBatchProgress
        status="running"
        total={10}
        processed={4}
        succeeded={3}
        skipped={1}
        failed={0}
        {...overrides}
      />
    </I18nProvider>,
  );
}

describe("ZhBatchProgress", () => {
  it("renders the percent based on processed/total", () => {
    show({ total: 10, processed: 4 });
    expect(screen.getByText("40%")).toBeTruthy();
  });

  it("shows processed/total", () => {
    show({ total: 10, processed: 4 });
    expect(screen.getByText("4 / 10")).toBeTruthy();
  });

  it("shows succeeded/skipped/failed tallies", () => {
    show({ succeeded: 3, skipped: 1, failed: 2 });
    expect(screen.getByText(/Exitosos: 3/)).toBeTruthy();
    expect(screen.getByText(/Omitidos: 1/)).toBeTruthy();
    expect(screen.getByText(/Fallidos: 2/)).toBeTruthy();
  });

  it.each(["running", "success", "warning", "error"] as const)(
    "supports the %s status",
    (status) => {
      show({ status });
      expect(document.querySelector(`.zh-batch-progress--${status}`)).toBeTruthy();
    },
  );

  it("supports compact mode without the tallies breakdown", () => {
    show({ compact: true });
    expect(document.querySelector(".zh-batch-progress--compact")).toBeTruthy();
    expect(screen.queryByText(/Exitosos/)).toBeNull();
  });

  it("shows the current label only while running", () => {
    show({ status: "running", currentLabel: "Procesando doc 3" });
    expect(screen.getByText("Procesando doc 3")).toBeTruthy();
  });

  it("hides the current label once finished", () => {
    show({ status: "success", currentLabel: "Procesando doc 3" });
    expect(screen.queryByText("Procesando doc 3")).toBeNull();
  });

  it("renders the message", () => {
    show({ status: "success", message: "Listo" });
    expect(screen.getByText("Listo")).toBeTruthy();
  });

  it("caps the percent at 100 when processed exceeds total", () => {
    show({ total: 5, processed: 8 });
    expect(screen.getByText("100%")).toBeTruthy();
  });
});
