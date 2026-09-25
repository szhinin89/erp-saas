// @vitest-environment jsdom
import type { ComponentProps } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { SupplierPaymentReverseModal } from "./SupplierPaymentReverseModal";
import type { SupplierPaymentDto } from "../api/supplierPaymentService";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

afterEach(() => {
  cleanup();
});

function renderModal(props: ComponentProps<typeof SupplierPaymentReverseModal>) {
  return render(
    <I18nProvider>
      <SupplierPaymentReverseModal {...props} />
    </I18nProvider>,
  );
}

function samplePayment(): SupplierPaymentDto {
  return {
    id: "sp-1",
    supplierId: "sup-1",
    branchId: "br-1",
    paymentDate: "2026-08-28",
    totalAmount: 300,
    systemNumber: "00000001",
    receiptNumber: null,
    displayNumber: "00000001",
    status: "Confirmed",
    methodLines: [
      {
        id: "ml-1",
        paymentMethodId: "pm-1",
        companyBankAccountId: "fd-1",
        cashRegisterId: null,
        amount: 300,
        referenceNumber: null,
        checkNumber: null,
        checkDate: null,
        notes: null,
      },
    ],
    applicationLines: [
      {
        id: "al-1",
        accountsPayableInstallmentId: "inst-1",
        amountApplied: 300,
        documentNumber: "001-001-000031760",
        installmentNumber: 1,
        dueDate: "2026-09-03",
        issueDate: "2026-08-01",
        originType: "PurchaseInvoice",
      },
    ],
    allocations: [],
    createdAt: "2026-08-28T10:00:00Z",
  };
}

function baseProps(
  over: Partial<ComponentProps<typeof SupplierPaymentReverseModal>> = {},
): ComponentProps<typeof SupplierPaymentReverseModal> {
  return {
    open: true,
    payment: samplePayment(),
    supplierName: "Proveedor Test",
    methods: [],
    saving: false,
    submitError: null,
    onCancel: vi.fn(),
    onConfirm: vi.fn(),
    ...over,
  };
}

describe("SupplierPaymentReverseModal", () => {
  it("no renderiza nada si open es false", () => {
    renderModal(baseProps({ open: false }));

    expect(screen.queryByText("Reversar pago")).toBeNull();
  });

  it("muestra el resumen del pago (número, proveedor, total)", () => {
    renderModal(baseProps());

    expect(screen.getByText("00000001")).toBeTruthy();
    expect(screen.getByText("Proveedor Test")).toBeTruthy();
  });

  it("bloquea el submit si el motivo está vacío o solo espacios", () => {
    const onConfirm = vi.fn();
    renderModal(baseProps({ onConfirm }));

    fireEvent.click(screen.getByText("Confirmar reversa"));

    expect(onConfirm).not.toHaveBeenCalled();
    expect(screen.getByText("El motivo es obligatorio.")).toBeTruthy();
  });

  it("hace trim del motivo antes de confirmar", () => {
    const onConfirm = vi.fn();
    renderModal(baseProps({ onConfirm }));

    fireEvent.change(screen.getByLabelText("Motivo de la reversa"), {
      target: { value: "   Error de digitación   " },
    });
    fireEvent.click(screen.getByText("Confirmar reversa"));

    expect(onConfirm).toHaveBeenCalledWith("Error de digitación");
  });

  it("deshabilita los botones mientras saving es true", () => {
    renderModal(baseProps({ saving: true }));

    expect((screen.getByText("Cancelar") as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByText("Reversando...") as HTMLButtonElement).disabled).toBe(true);
  });

  it("muestra el error de la API sin cerrar el modal", () => {
    renderModal(baseProps({ submitError: "El pago ya fue reversado." }));

    expect(screen.getByText("El pago ya fue reversado.")).toBeTruthy();
  });
});

// ZH-DESIGN-SYSTEM-PRECISION-04F — total, medios de pago y cuotas aplicadas (textos compuestos)
// usan usePrecisionDecimals("money") en vez del default legacy 2; sin "$" como antes.
describe("SupplierPaymentReverseModal — montos con semántica money (04F)", () => {
  it("usa moneyDecimals y reacciona A → B sin remount", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    renderModal(baseProps());
    const text = () => document.body.textContent ?? "";
    expect(text()).toContain("300.00");
    expect(text()).not.toContain("$300");

    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 4 }));
    expect(text()).toContain("300.0000");
  });
});
