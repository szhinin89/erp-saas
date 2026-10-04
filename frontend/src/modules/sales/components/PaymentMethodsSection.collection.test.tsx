// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import type { SalesPageContext } from "../hooks/useSalesPage";
import type { PaymentMethodDto } from "../api/paymentMethodService";
import { withPosDerivedCtx } from "../test/salesPageCtxTestUtils";
import { PaymentMethodsSection } from "./PaymentMethodsSection";

// POS-COLLECTION-INLINE-B-01 — cobro inline: Forma de cobro → Efectivo recibido → Resumen → un
// único estado. El estado/mensaje sale de ctx.collection (SSOT), nunca se recalcula en la vista.

afterEach(() => cleanup());

function pm(overrides: Partial<PaymentMethodDto>): PaymentMethodDto {
  return {
    id: "pm-x",
    code: "X",
    name: "X",
    isActive: true,
    requiresReference: false,
    isCreditAllowed: false,
    sortOrder: 1,
    detailType: "None",
    affectsPhysicalCash: false,
    sriPaymentMethodCode: null,
    accountSource: "PaymentMethodAccount",
    accountingAccountId: null,
    ...overrides,
  };
}
const CASH = pm({
  id: "pm-cash",
  code: "EFECTIVO",
  name: "Efectivo",
  sriPaymentMethodCode: "01",
  affectsPhysicalCash: true,
});
const CARD = pm({ id: "pm-card", code: "TARJETA", name: "Tarjeta", sortOrder: 2, sriPaymentMethodCode: "19" });

function buildCtx(overrides: Record<string, unknown> = {}): SalesPageContext {
  return withPosDerivedCtx({
    readOnly: false,
    fieldDisabled: false,
    isCreditTerm: false,
    isElectronic: false,
    canEmit: false,
    editing: null,
    grandTotal: 14.66,
    summary: { total: 14.66 },
    formWatch: { sriPaymentMethodCode: "01", docTypeCode: "01", customerId: "c-1" },
    paymentMethods: [CASH, CARD],
    sriPaymentMethods: [{ code: "01", name: "Sin utilización del sistema financiero" }],
    payments: [{ _key: 1, paymentMethodId: CASH.id, amount: 14.66, reference: null }],
    payKey: 2,
    setPayKey: vi.fn(),
    setInvoicePayments: vi.fn(),
    setCashReceivedInput: vi.fn(),
    openIssueFlow: vi.fn(),
    setCreditAmount: vi.fn(),
    setCreditRows: vi.fn(),
    setModalCredit: vi.fn(),
    simulateCreditInstallments: vi.fn(() => []),
    ...overrides,
  });
}

/** POS-COLLECTION-INLINE-B-01: UNA tarjeta de resultado (con el tono del estado) y UN estado. */
function stateLine(container: HTMLElement) {
  return container.querySelectorAll(".sales-result");
}
function highlight(container: HTMLElement) {
  return container.querySelector(".sales-result__highlight-amount")?.textContent;
}

describe("PaymentMethodsSection — efectivo único (flujo B)", () => {
  it("estado inicial (recibido vacío): neutro — sin 'Insuficiente' ni 'Cobro completo'", () => {
    const { container } = render(<PaymentMethodsSection ctx={buildCtx()} />);
    expect(stateLine(container)).toHaveLength(1);
    expect(screen.getByText("Esperando monto")).toBeTruthy();
    expect(container.querySelectorAll(".sales-result__state")).toHaveLength(1);
    expect(container.querySelector(".sales-result__highlight")).toBeNull();
    expect(screen.queryByText(/Insuficiente/)).toBeNull();
    expect(screen.queryByText(/Cobro completo/)).toBeNull();
    expect(stateLine(container)[0].className).toContain("--neutral");
  });

  it("solo existe el campo 'Efectivo recibido' — el monto aplicado no es un segundo input idéntico", () => {
    const { container } = render(<PaymentMethodsSection ctx={buildCtx()} />);
    expect(screen.getByLabelText("Efectivo recibido", { selector: "input" })).toBeTruthy();
    expect(container.querySelector(".sales-payment-input")).toBeNull();
  });

  it("menor al debido: 'Falta por cobrar $X' (warning), único mensaje", () => {
    const { container } = render(
      <PaymentMethodsSection ctx={buildCtx({ cashReceivedInput: "10" })} />,
    );
    expect(stateLine(container)).toHaveLength(1);
    expect(screen.getByText("Falta por cobrar")).toBeTruthy();
    expect(highlight(container)).toBe("$4.66");
    expect(stateLine(container)[0].className).toContain("--warning");
  });

  it("exacto: 'Pago exacto' (success)", () => {
    const { container } = render(
      <PaymentMethodsSection ctx={buildCtx({ cashReceivedInput: "14.66" })} />,
    );
    expect(screen.getByText("Pago exacto")).toBeTruthy();
    expect(stateLine(container)[0].className).toContain("--success");
  });

  it("mayor: resumen Total / Recibido / Vuelto + estado 'Cobro completo'", () => {
    const { container } = render(
      <PaymentMethodsSection ctx={buildCtx({ cashReceivedInput: "20" })} />,
    );
    const amounts = Array.from(
      container.querySelectorAll(".sales-result__rows dd"),
    ).map((d) => d.textContent);
    expect(amounts).toEqual(["$14.66", "$20.00"]);
    expect(highlight(container)).toBe("$5.34");
    expect(screen.getByText("Cobro completo")).toBeTruthy();
    expect(stateLine(container)).toHaveLength(1);
  });

  it("escribir en 'Efectivo recibido' actualiza el estado en cada pulsación (onChange, no blur)", () => {
    const setCashReceivedInput = vi.fn();
    render(<PaymentMethodsSection ctx={buildCtx({ setCashReceivedInput })} />);
    fireEvent.change(screen.getByLabelText("Efectivo recibido", { selector: "input" }), { target: { value: "2" } });
    expect(setCashReceivedInput).toHaveBeenCalledWith("2");
  });

  it("Enter en 'Efectivo recibido' abre la emisión cuando canEmit", () => {
    const openIssueFlow = vi.fn();
    render(
      <PaymentMethodsSection
        ctx={buildCtx({ cashReceivedInput: "20", canEmit: true, openIssueFlow })}
      />,
    );
    fireEvent.keyDown(screen.getByLabelText("Efectivo recibido", { selector: "input" }), { key: "Enter" });
    expect(openIssueFlow).toHaveBeenCalledTimes(1);
  });

  it("Enter NO emite si canEmit es false (misma autoridad que el botón y F8)", () => {
    const openIssueFlow = vi.fn();
    render(<PaymentMethodsSection ctx={buildCtx({ openIssueFlow, canEmit: false })} />);
    fireEvent.keyDown(screen.getByLabelText("Efectivo recibido", { selector: "input" }), { key: "Enter" });
    expect(openIssueFlow).not.toHaveBeenCalled();
  });

  it("sin composición vieja: no existen la caja de efectivo ni la de resumen separadas", () => {
    const { container } = render(<PaymentMethodsSection ctx={buildCtx({ cashReceivedInput: "5" })} />);
    expect(container.querySelectorAll(".sales-cash-box, .sales-summary-box, .sales-collection")).toHaveLength(0);
    expect(container.querySelectorAll(".sales-result")).toHaveLength(1);
    expect(screen.queryByText(/Insuficiente/)).toBeNull();
  });

  it("montos rápidos útiles respecto al total (14.66 → $15 · $20 · $50) y Limpiar", () => {
    const setCashReceivedInput = vi.fn();
    render(<PaymentMethodsSection ctx={buildCtx({ setCashReceivedInput })} />);
    fireEvent.click(screen.getByRole("button", { name: "$20" }));
    expect(setCashReceivedInput).toHaveBeenLastCalledWith("20.00");
    expect(screen.getByRole("button", { name: "$15" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "$50" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Limpiar" }));
    expect(setCashReceivedInput).toHaveBeenLastCalledWith("");
  });

  it("efectivo único: sin controles extra bajo la forma de cobro (sin 'Quitar efectivo')", () => {
    render(<PaymentMethodsSection ctx={buildCtx()} />);
    expect(screen.queryByTitle("Quitar efectivo")).toBeNull();
  });

  it("el input declara el opt-in de F8 (data-pos-emit-shortcut)", () => {
    render(<PaymentMethodsSection ctx={buildCtx()} />);
    expect(
      screen.getByLabelText("Efectivo recibido", { selector: "input" }).getAttribute("data-pos-emit-shortcut"),
    ).toBe("true");
  });
});

describe("PaymentMethodsSection — multipago", () => {
  const multi = {
    payments: [
      { _key: 1, paymentMethodId: CASH.id, amount: 4.66, reference: null },
      { _key: 2, paymentMethodId: CARD.id, amount: 10, reference: null },
    ],
  };

  it("cada forma conserva su monto 'Aplicado' rotulado + Efectivo recibido separado", () => {
    const { container } = render(<PaymentMethodsSection ctx={buildCtx(multi)} />);
    expect(screen.getByLabelText("Monto aplicado Efectivo")).toBeTruthy();
    expect(screen.getByLabelText("Monto aplicado Tarjeta")).toBeTruthy();
    expect(screen.getByLabelText("Efectivo recibido", { selector: "input" })).toBeTruthy();
    expect(container.querySelectorAll(".sales-payment-applied-label").length).toBe(2);
  });

  it("aplicado < total: 'Falta por cobrar' con la diferencia (sin redistribuir)", () => {
    const { container } = render(
      <PaymentMethodsSection
        ctx={buildCtx({
          payments: [
            { _key: 1, paymentMethodId: CASH.id, amount: 2, reference: null },
            { _key: 2, paymentMethodId: CARD.id, amount: 10, reference: null },
          ],
          cashReceivedInput: "2",
        })}
      />,
    );
    expect(screen.getByText("Falta por cobrar")).toBeTruthy();
    expect(highlight(container)).toBe("$2.66");
  });

  it("aplicado > total: 'El cobro excede el total' (error)", () => {
    const { container } = render(
      <PaymentMethodsSection
        ctx={buildCtx({
          payments: [
            { _key: 1, paymentMethodId: CASH.id, amount: 10, reference: null },
            { _key: 2, paymentMethodId: CARD.id, amount: 10, reference: null },
          ],
        })}
      />,
    );
    expect(screen.getByText("El cobro excede el total")).toBeTruthy();
    expect(stateLine(container)[0].className).toContain("--error");
  });
});

describe("PaymentMethodsSection — visibilidad electrónica / física", () => {
  it("física: no muestra el código SRI de la forma de cobro", () => {
    render(<PaymentMethodsSection ctx={buildCtx({ isElectronic: false })} />);
    expect(screen.queryByText(/SRI 01/)).toBeNull();
    expect(screen.queryByText(/Sin forma de pago SRI/)).toBeNull();
  });

  it("electrónica: muestra el código SRI derivado de la forma de cobro", () => {
    render(<PaymentMethodsSection ctx={buildCtx({ isElectronic: true })} />);
    expect(screen.getByText(/SRI 01/)).toBeTruthy();
  });
});
