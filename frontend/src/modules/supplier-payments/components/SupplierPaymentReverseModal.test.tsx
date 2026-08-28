// @vitest-environment jsdom
import type { ComponentProps } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { SupplierPaymentReverseModal } from "./SupplierPaymentReverseModal";
import type { SupplierPaymentDto } from "../api/supplierPaymentService";

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
        financialDestinationId: "fd-1",
        amount: 300,
        referenceNumber: null,
        checkNumber: null,
        checkDate: null,
        notes: null,
      },
    ],
    applicationLines: [{ id: "al-1", accountsPayableInstallmentId: "inst-1", amountApplied: 300 }],
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
