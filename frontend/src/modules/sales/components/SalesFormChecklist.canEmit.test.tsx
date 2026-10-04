// @vitest-environment jsdom
import { describe, it, expect, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import type { SalesPageContext, EmitBlocker } from "../hooks/useSalesPage";
import { SalesFormChecklist } from "./SalesFormChecklist";
import { EmitButton } from "./EmitButton";

// POS-CANEMIT-SSOT-01 — checklist y botón Emitir leen la MISMA fuente (ctx.canEmit /
// ctx.emitBlockers). Ninguno recalcula reglas: "Listo para emitir" ⇔ Emitir habilitado.

afterEach(() => cleanup());

function ctx(canEmit: boolean, emitBlockers: EmitBlocker[] = []): SalesPageContext {
  return {
    canEmit,
    emitBlockers,
    isElectronic: false,
    fieldDisabled: false,
    openIssueFlow: () => {},
  } as unknown as SalesPageContext;
}

const CONSUMER_FINAL: EmitBlocker = {
  source: "consumerFinal",
  message:
    "El total supera el monto permitido para Consumidor Final — seleccione un cliente identificado.",
};
const PAYMENT: EmitBlocker = { source: "payment", message: "Complete el cobro en Formas de Cobro." };

describe("SalesFormChecklist = canEmit", () => {
  it("canEmit=true → 'Listo para emitir' y el botón está habilitado", () => {
    render(
      <>
        <SalesFormChecklist ctx={ctx(true)} />
        <EmitButton ctx={ctx(true)} />
      </>,
    );
    expect(screen.getByText("Listo para emitir")).toBeTruthy();
    expect((screen.getByRole("button", { name: /Emitir Factura/ }) as HTMLButtonElement).disabled).toBe(false);
  });

  it("Consumidor Final excedido: NUNCA 'Listo para emitir' con Emitir deshabilitado", () => {
    render(
      <>
        <SalesFormChecklist ctx={ctx(false, [CONSUMER_FINAL])} />
        <EmitButton ctx={ctx(false, [CONSUMER_FINAL])} />
      </>,
    );
    expect(screen.queryByText("Listo para emitir")).toBeNull();
    expect(screen.getByText(CONSUMER_FINAL.message)).toBeTruthy();
    expect((screen.getByRole("button", { name: /Emitir Factura/ }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("muestra SOLO el primer bloqueante (orden del hook), no la lista completa", () => {
    render(<SalesFormChecklist ctx={ctx(false, [CONSUMER_FINAL, PAYMENT])} />);
    expect(screen.getByText(CONSUMER_FINAL.message)).toBeTruthy();
    expect(screen.queryByText(PAYMENT.message)).toBeNull();
  });
});

describe("EmitButton — sin texto de motivo duplicado", () => {
  it("deshabilitado: el motivo queda solo como title accesible, no como texto visible extra", () => {
    const { container } = render(<EmitButton ctx={ctx(false, [PAYMENT])} />);
    const button = screen.getByRole("button", { name: /Emitir Factura/ });
    expect(button.getAttribute("title")).toBe(`No se puede emitir: ${PAYMENT.message}`);
    expect(container.querySelector(".sf-save-tooltip")).toBeNull();
    expect(screen.queryByText(PAYMENT.message)).toBeNull();
  });
});
