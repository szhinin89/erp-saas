// @vitest-environment jsdom
import { describe, it, expect } from "vitest";
import { isEditableTarget } from "./inputUtils";

describe("SALES-QUICK-CUSTOMER-MODAL-INPUT-FIX-07 — isEditableTarget", () => {
  it("true para input, textarea y select", () => {
    expect(isEditableTarget(document.createElement("input"))).toBe(true);
    expect(isEditableTarget(document.createElement("textarea"))).toBe(true);
    expect(isEditableTarget(document.createElement("select"))).toBe(true);
  });

  it("true para un elemento contentEditable", () => {
    const div = document.createElement("div");
    div.setAttribute("contenteditable", "true");
    expect(isEditableTarget(div)).toBe(true);
  });

  it("false para botones, divs normales y null", () => {
    expect(isEditableTarget(document.createElement("button"))).toBe(false);
    expect(isEditableTarget(document.createElement("div"))).toBe(false);
    expect(isEditableTarget(null)).toBe(false);
  });

  it("false para un target que no es un HTMLElement (p. ej. window)", () => {
    expect(isEditableTarget(window)).toBe(false);
  });
});
