// @vitest-environment jsdom
import { describe, it, expect } from "vitest";
import { shouldTriggerF8Emit } from "./useSalesPage";

// SALES-QUICK-CUSTOMER-MODAL-FIX-07A: guard del atajo global F8 ("Emitir Factura") — nunca debe
// disparar mientras el foco está en un control editable (p. ej. el modal "Crear Cliente"
// abierto), y debe conservar exactamente el comportamiento anterior (tab/issuePhase/canEmit)
// fuera de ese caso.

const READY_CTX = { tab: "nuevo" as const, issuePhase: "idle" as const, canEmit: true };

describe("SALES-QUICK-CUSTOMER-MODAL-FIX-07A — shouldTriggerF8Emit", () => {
  it("con el foco en un input editable, F8 NO dispara la emisión aunque el resto del contexto lo permita", () => {
    const input = document.createElement("input");
    expect(shouldTriggerF8Emit({ key: "F8", target: input }, READY_CTX)).toBe(false);
  });

  it("con el foco en un textarea o select editable, F8 tampoco dispara", () => {
    const textarea = document.createElement("textarea");
    const select = document.createElement("select");
    expect(shouldTriggerF8Emit({ key: "F8", target: textarea }, READY_CTX)).toBe(false);
    expect(shouldTriggerF8Emit({ key: "F8", target: select }, READY_CTX)).toBe(false);
  });

  it("con el foco en un elemento contentEditable, F8 tampoco dispara", () => {
    const div = document.createElement("div");
    div.setAttribute("contenteditable", "true");
    expect(shouldTriggerF8Emit({ key: "F8", target: div }, READY_CTX)).toBe(false);
  });

  it("fuera de un control editable, F8 dispara igual que antes cuando tab/issuePhase/canEmit lo permiten", () => {
    const button = document.createElement("button");
    expect(shouldTriggerF8Emit({ key: "F8", target: button }, READY_CTX)).toBe(true);
    // target null (p. ej. el propio window) también debe permitir el disparo — comportamiento
    // preexistente, no debe romperse por el guard nuevo.
    expect(shouldTriggerF8Emit({ key: "F8", target: null }, READY_CTX)).toBe(true);
  });

  it("fuera de un control editable, sigue respetando tab/issuePhase/canEmit exactamente igual que antes", () => {
    const button = document.createElement("button");
    expect(
      shouldTriggerF8Emit({ key: "F8", target: button }, { ...READY_CTX, tab: "listado" }),
    ).toBe(false);
    expect(
      shouldTriggerF8Emit({ key: "F8", target: button }, { ...READY_CTX, issuePhase: "confirm" }),
    ).toBe(false);
    expect(
      shouldTriggerF8Emit({ key: "F8", target: button }, { ...READY_CTX, canEmit: false }),
    ).toBe(false);
  });

  it("cualquier tecla distinta de F8 nunca dispara, editable o no", () => {
    const input = document.createElement("input");
    const button = document.createElement("button");
    expect(shouldTriggerF8Emit({ key: "Enter", target: input }, READY_CTX)).toBe(false);
    expect(shouldTriggerF8Emit({ key: "Enter", target: button }, READY_CTX)).toBe(false);
  });
});
