// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PurchaseConfirmChecklist } from "./PurchaseConfirmChecklist";

vi.mock("../../../i18n/i18n", () => ({ useI18n: () => ({ t: (key: string, fallback?: unknown) => typeof fallback === "string" ? fallback
  : fallback && typeof fallback === "object" && "count" in fallback ? `Advertencias (${String((fallback as { count: number }).count)})` : key }) }));
afterEach(cleanup);

const items = () => screen.getAllByRole("listitem").filter(li => li.classList.contains("pf-confirm-checklist__item")).map(li => li.className.replace("pf-confirm-checklist__item pf-confirm-checklist__item--", ""));

describe("PurchaseConfirmChecklist (P2)", () => {
  it("zero issues: one passed line and no blocking hint", () => {
    render(<PurchaseConfirmChecklist summary={{ hasXmlPurchase: true, blockerCount: 0, issues: [], passed: ["Ítems (2)", "Impuestos (2)"] }} />);
    expect(items()).toEqual(["success"]);
    expect(screen.getByRole("listitem").textContent).toBe("check_circleCorrectoÍtems (2) · Impuestos (2)");
    expect(screen.queryByText(/bloquearán la confirmación/)).toBeNull();
    expect(screen.getByText(/La compra quedará de solo lectura/)).toBeTruthy();
  });

  it("issues come first, the blocking hint appears once and icons are hidden from screen readers", () => {
    render(<PurchaseConfirmChecklist summary={{ hasXmlPurchase: true, blockerCount: 1, passed: ["Impuestos (2)"], issues: [
      { key: "presentations", tone: "danger", label: "Presentaciones", detail: "1 sin presentación" },
      { key: "total-difference", tone: "warning", label: "Diferencia total", detail: "$0.50" },
    ] }} />);
    expect(items()).toEqual(["danger", "warning", "success"]);
    expect(screen.getAllByText("Hay controles pendientes que bloquearán la confirmación.")).toHaveLength(1);
    document.querySelectorAll(".material-symbols-outlined").forEach(icon => expect(icon.getAttribute("aria-hidden")).toBe("true"));
  });

  it("manual purchases keep their neutral note", () => {
    render(<PurchaseConfirmChecklist summary={{ hasXmlPurchase: false, blockerCount: 0, issues: [], passed: [] }} />);
    expect(items()).toEqual(["neutral"]);
  });

  // PURCHASE-CONFIRM-LINE-WARNINGS-01 — caso DINADEC 034-005-002241241: la línea 01 con cantidad/costo
  // distintos al XML ya no queda oculta detrás de "Correcto".
  const originWarning = { key: "1-0", line: "Línea 1 · Leche", reason: "Cantidad o costo distintos al documento del proveedor" };

  it("no line warnings: no warnings section is rendered", () => {
    render(<PurchaseConfirmChecklist summary={{ hasXmlPurchase: true, blockerCount: 0, issues: [], passed: ["Ítems (1)"] }} lineWarnings={[]} />);
    expect(items()).toEqual(["success"]);
    expect(screen.queryByText(/Advertencias/)).toBeNull();
  });

  it("one warning: grouped section with the line and reason, kept next to Correcto, without blocking hint", () => {
    render(<PurchaseConfirmChecklist summary={{ hasXmlPurchase: true, blockerCount: 0, issues: [], passed: ["Ítems (1)"] }} lineWarnings={[originWarning]} />);
    expect(items()).toEqual(["warning", "success"]);
    expect(screen.getByText("Advertencias (1)")).toBeTruthy();
    expect(screen.getByText("Línea 1 · Leche").closest("li")?.textContent).toBe("Línea 1 · Leche — Cantidad o costo distintos al documento del proveedor");
    expect(screen.getByText("Correcto")).toBeTruthy();
    expect(screen.queryByText(/bloquearán la confirmación/)).toBeNull();
  });

  it("several warnings: one compact row per warning under a single heading", () => {
    render(<PurchaseConfirmChecklist summary={{ hasXmlPurchase: true, blockerCount: 0, issues: [], passed: ["Ítems (2)"] }} lineWarnings={[
      originWarning,
      { key: "1-1", line: "Línea 1 · Leche", reason: "Sin precio de venta" },
      { key: "2-0", line: "Línea 2 · Queso", reason: "Costo fuera de rango" },
    ]} />);
    expect(screen.getAllByText(/^Advertencias/)).toHaveLength(1);
    expect(screen.getByText("Advertencias (3)")).toBeTruthy();
    expect(document.querySelectorAll(".pf-confirm-checklist__sublist > li")).toHaveLength(3);
  });

  it("real blocker plus warnings: blocker first, then warnings, then Correcto; blocking hint only from blockers", () => {
    render(<PurchaseConfirmChecklist summary={{ hasXmlPurchase: true, blockerCount: 1, passed: ["Impuestos (2)"], issues: [
      { key: "linked-items", tone: "danger", label: "Ítems vinculados", detail: "1 línea(s) XML sin ítem." },
    ] }} lineWarnings={[originWarning]} />);
    expect(items()).toEqual(["danger", "warning", "success"]);
    expect(screen.getAllByText("Hay controles pendientes que bloquearán la confirmación.")).toHaveLength(1);
    document.querySelectorAll(".material-symbols-outlined").forEach(icon => expect(icon.getAttribute("aria-hidden")).toBe("true"));
  });

  it("commercial risk: one compact warning row without per-product rows and without blocking hint", () => {
    render(<PurchaseConfirmChecklist summary={{ hasXmlPurchase: true, blockerCount: 0, issues: [], passed: ["Ítems (40)"] }}
      commercialRisk={{ lineCount: 30, detail: "20 con margen negativo · 10 sin precio de venta. El detalle está en cada línea; no impide confirmar." }} />);
    expect(items()).toEqual(["warning", "success"]);
    expect(screen.getByText(/20 con margen negativo · 10 sin precio de venta/)).toBeTruthy();
    expect(document.querySelectorAll(".pf-confirm-checklist__sublist > li")).toHaveLength(0);
    expect(screen.queryByText(/bloquearán la confirmación/)).toBeNull();
  });
});
