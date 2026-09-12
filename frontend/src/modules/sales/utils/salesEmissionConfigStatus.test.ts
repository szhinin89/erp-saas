import { describe, it, expect, vi } from "vitest";
import { computeSalesConfigStatus } from "./salesEmissionConfigStatus";
import type { SalesPageContext } from "../hooks/useSalesPage";

// SALES-POS-EMISSION-PANEL-SIMPLIFICATION-01: computeSalesConfigStatus es la única fuente de
// verdad del estado "Lista/Revisar/Incompleta" — usada tanto por la tarjeta compacta
// (SalesEmissionConfigSection) como por el mensaje "Siguiente paso" (SalesFormChecklist).

function buildCtx(overrides: Partial<SalesPageContext> = {}): SalesPageContext {
  const base = {
    formWatch: { docTypeCode: "01", sriPaymentMethodCode: "01", customerId: "cust-1" },
    setValue: vi.fn(),
    readOnly: false,
    fieldDisabled: false,
    editing: null,
    lines: [],
    selectedWarehouseId: "wh-1",
    payments: [],
    paymentMethods: [],
    hasCashSession: true,
    myCashSession: {
      id: "cash-session-1",
      companyId: "company-1",
      branchId: "branch-1",
      userId: "user-1",
      cashRegisterId: "cr-1",
      cashRegisterCodeSnapshot: "CAJA-01",
      cashRegisterNameSnapshot: "Caja Principal",
      emissionPointId: "ep-1",
      emissionPointCodeSnapshot: "001",
      emissionType: "Physical",
      defaultWarehouseId: null,
      defaultCustomerId: null,
    },
    branchName: "Sucursal Principal",
    sriDocTypes: [{ code: "01", name: "Factura" }],
    sriPaymentMethods: [
      { code: "01", name: "Sin utilización del sistema financiero" },
      { code: "20", name: "Otros con utilización del sistema financiero" },
    ],
  };
  return { ...base, ...overrides } as unknown as SalesPageContext;
}

describe("computeSalesConfigStatus", () => {
  it("ready cuando todos los defaults están presentes y no hay fallback en uso", () => {
    expect(computeSalesConfigStatus(buildCtx()).level).toBe("ready");
  });

  it("incomplete cuando falta el cliente", () => {
    const status = computeSalesConfigStatus(
      buildCtx({
        formWatch: {
          docTypeCode: "01",
          sriPaymentMethodCode: "01",
          customerId: "",
        } as unknown as SalesPageContext["formWatch"],
      }),
    );
    expect(status.level).toBe("incomplete");
    expect(status.missing).toContain("Cliente");
  });

  it("incomplete cuando no hay caja abierta", () => {
    const status = computeSalesConfigStatus(
      buildCtx({ hasCashSession: false, myCashSession: null }),
    );
    expect(status.level).toBe("incomplete");
    expect(status.missing).toContain("Caja abierta (Sucursal / Caja / Punto de emisión)");
  });

  it("incomplete cuando hay líneas que controlan stock pero no hay bodega seleccionada", () => {
    const status = computeSalesConfigStatus(
      buildCtx({
        selectedWarehouseId: "",
        lines: [{ _tracksStock: true }] as unknown as SalesPageContext["lines"],
      }),
    );
    expect(status.level).toBe("incomplete");
    expect(status.missing).toContain("Bodega");
  });

  it("incomplete cuando no hay Forma Pago SRI por defecto y una forma de cobro en uso no tiene mapeo propio", () => {
    const status = computeSalesConfigStatus(
      buildCtx({
        formWatch: {
          docTypeCode: "01",
          sriPaymentMethodCode: "",
          customerId: "cust-1",
        } as unknown as SalesPageContext["formWatch"],
        paymentMethods: [
          {
            id: "pm-1",
            code: "OTRO",
            name: "Otro",
            isActive: true,
            requiresReference: false,
            isCreditAllowed: false,
            sortOrder: 1,
            detailType: "None",
            sriPaymentMethodCode: null,
          },
        ],
        payments: [{ _key: 1, paymentMethodId: "pm-1", amount: 10, reference: null }],
      }),
    );
    expect(status.level).toBe("incomplete");
    expect(status.missing).toContain("Forma de pago SRI por defecto");
  });

  it("review (no bloquea) cuando hay Forma Pago SRI por defecto pero una forma de cobro en uso cae al fallback", () => {
    const status = computeSalesConfigStatus(
      buildCtx({
        paymentMethods: [
          {
            id: "pm-1",
            code: "OTRO",
            name: "Otro",
            isActive: true,
            requiresReference: false,
            isCreditAllowed: false,
            sortOrder: 1,
            detailType: "None",
            sriPaymentMethodCode: null,
          },
        ],
        payments: [{ _key: 1, paymentMethodId: "pm-1", amount: 10, reference: null }],
      }),
    );
    expect(status.level).toBe("review");
    expect(status.missing).toHaveLength(0);
    expect(status.warnings.length).toBeGreaterThan(0);
  });
});
