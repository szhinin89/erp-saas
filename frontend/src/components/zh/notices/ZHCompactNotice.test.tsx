// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { ZHCompactNotice } from "./ZHCompactNotice";
import type { NoticeVM } from "../../../notices";

/**
 * EXPENSE-UI-02 — compatibilidad de `ZHCompactNotice` tras añadir `anchored` (opt-in): sin la prop
 * el comportamiento es el original (popover bajo el trigger, sin posicionamiento anclado); con la
 * prop se usa el posicionamiento anclado. Consumidor existente: Compras (`PurchaseReceptionProcessCell`).
 */
afterEach(cleanup);

const NOTICE: NoticeVM = {
  severity: "warning",
  intent: "status",
  source: "domain-status",
  label: "Aviso XML",
  detail: "Detalle completo del aviso.",
};

describe("ZHCompactNotice", () => {
  it("sin `anchored` conserva el comportamiento original (popover bajo el trigger)", () => {
    render(<ZHCompactNotice notice={NOTICE} />);
    const trigger = screen.getByRole("button", { name: /Aviso XML/ });
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(trigger);
    const dialog = screen.getByRole("dialog");
    expect(trigger.getAttribute("aria-expanded")).toBe("true");
    expect(dialog.classList.contains("zh-notice-popover--anchored")).toBe(false);
    expect(dialog.style.visibility).not.toBe("hidden"); // posición calculada bajo el trigger, no oculto a la espera de medir
    expect(dialog.textContent).toContain("Detalle completo del aviso.");
    fireEvent.keyDown(window, { key: "Escape" });
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("con `anchored` usa el posicionamiento anclado (opt-in)", () => {
    render(<ZHCompactNotice notice={NOTICE} anchored />);
    fireEvent.click(screen.getByRole("button", { name: /Aviso XML/ }));
    expect(screen.getByRole("dialog").classList.contains("zh-notice-popover--anchored")).toBe(true);
  });

  it("sin detalle muestra solo el título", () => {
    render(<ZHCompactNotice notice={{ ...NOTICE, detail: undefined }} />);
    fireEvent.click(screen.getByRole("button", { name: /Aviso XML/ }));
    expect(screen.getByRole("dialog").querySelector(".zh-notice-popover__detail")).toBeNull();
  });
});
