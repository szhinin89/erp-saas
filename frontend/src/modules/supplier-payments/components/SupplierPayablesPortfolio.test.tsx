// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { FormProvider, useForm } from "react-hook-form";
import { SupplierPayablesPortfolio } from "./SupplierPayablesPortfolio";
import type { PendingInstallmentOption } from "../api/pendingPayablesFacade";
import type { RegisterSupplierPaymentFormValues } from "../../../schemas/supplier-payments/registerSupplierPaymentSchema";

const TODAY = new Date().toISOString().slice(0, 10);
const PAST = "2000-01-01";
const FUTURE = "2999-01-01";

const installments: PendingInstallmentOption[] = [
  {
    installmentId: "inst-1",
    payableId: "pay-1",
    documentType: "FAC",
    documentNumber: "001-001-000031760",
    installmentNumber: 1,
    issueDate: PAST,
    dueDate: PAST,
    totalAmount: 47.37,
    paidAmount: 3.84,
    outstandingAmount: 43.53,
    status: "partiallypaid",
  },
  {
    installmentId: "inst-2",
    payableId: "pay-2",
    documentType: "FAC",
    documentNumber: "001-001-000031761",
    installmentNumber: 1,
    issueDate: FUTURE,
    dueDate: FUTURE,
    totalAmount: 100,
    paidAmount: 0,
    outstandingAmount: 100,
    status: "pending",
  },
];

function Wrapper({ rows = installments, loading = false }: { rows?: PendingInstallmentOption[]; loading?: boolean }) {
  const form = useForm<RegisterSupplierPaymentFormValues>({
    defaultValues: {
      supplierId: "sup-1",
      paymentDate: TODAY,
      receiptNumber: "",
      methodLines: [],
      applicationLines: [],
    },
  });
  return (
    <FormProvider {...form}>
      <SupplierPayablesPortfolio installments={rows} loading={loading} />
      <output data-testid="applicationLines">{JSON.stringify(form.watch("applicationLines"))}</output>
    </FormProvider>
  );
}

afterEach(cleanup);

describe("SupplierPayablesPortfolio", () => {
  it("muestra el resumen de cartera (total pendiente, vencido, próximo vencimiento, cuotas)", () => {
    const { container } = render(<Wrapper />);
    const values = Array.from(container.querySelectorAll(".pg-kpi-value")).map((el) => el.textContent);
    expect(values).toEqual(["143.53", "43.53", expect.any(String), "2"]);
  });

  it("muestra la grilla con las cuotas pendientes ordenadas como llegan (vencimiento ascendente)", () => {
    render(<Wrapper />);
    const rows = screen.getAllByRole("row").slice(1); // sin header
    expect(rows[0].textContent).toContain("001-001-000031760");
    expect(rows[1].textContent).toContain("001-001-000031761");
  });

  it('"Aplicar saldo completo" llena el monto de la cuota con el saldo pendiente', () => {
    render(<Wrapper />);
    const btn = screen.getAllByRole("button", { name: "Aplicar saldo completo" })[0];
    fireEvent.click(btn);
    const applied = JSON.parse(screen.getByTestId("applicationLines").textContent ?? "[]");
    expect(applied).toEqual([{ accountsPayableInstallmentId: "inst-1", amountApplied: 43.53 }]);
  });

  it('"Pagar todo el saldo pendiente" distribuye el saldo completo en todas las cuotas', () => {
    render(<Wrapper />);
    fireEvent.click(screen.getByRole("button", { name: "Pagar todo el saldo pendiente" }));
    const applied = JSON.parse(screen.getByTestId("applicationLines").textContent ?? "[]");
    expect(applied).toEqual([
      { accountsPayableInstallmentId: "inst-1", amountApplied: 43.53 },
      { accountsPayableInstallmentId: "inst-2", amountApplied: 100 },
    ]);
  });

  it("no permite un monto mayor al saldo pendiente de la cuota", () => {
    render(<Wrapper />);
    const input = screen.getByLabelText(/Monto a aplicar: FAC 001-001-000031760/);
    fireEvent.change(input, { target: { value: "999" } });
    const applied = JSON.parse(screen.getByTestId("applicationLines").textContent ?? "[]");
    expect(applied).toEqual([{ accountsPayableInstallmentId: "inst-1", amountApplied: 43.53 }]);
  });

  it("permite escribir 20.50 tecla por tecla sin perder el punto decimal (SUPPLIER-PAYMENT-PORTFOLIO-DECIMAL-INPUT-01)", () => {
    render(<Wrapper />);
    const input = screen.getByLabelText(
      /Monto a aplicar: FAC 001-001-000031760/,
    ) as HTMLInputElement;

    // Simula la escritura real, carácter por carácter — el bug reformateaba a "20.00" apenas se
    // tecleaba el punto, impidiendo continuar escribiendo los decimales.
    for (const partial of ["2", "20", "20.", "20.5", "20.50"]) {
      fireEvent.change(input, { target: { value: partial } });
      expect(input.value).toBe(partial);
    }

    const applied = JSON.parse(screen.getByTestId("applicationLines").textContent ?? "[]");
    expect(applied).toEqual([{ accountsPayableInstallmentId: "inst-1", amountApplied: 20.5 }]);
  });

  it("permite escribir 0.25", () => {
    render(<Wrapper />);
    const input = screen.getByLabelText(
      /Monto a aplicar: FAC 001-001-000031760/,
    ) as HTMLInputElement;

    for (const partial of ["0", "0.", "0.2", "0.25"]) {
      fireEvent.change(input, { target: { value: partial } });
      expect(input.value).toBe(partial);
    }

    const applied = JSON.parse(screen.getByTestId("applicationLines").textContent ?? "[]");
    expect(applied).toEqual([{ accountsPayableInstallmentId: "inst-1", amountApplied: 0.25 }]);
  });

  it("permite escribir el separador decimal , (coma) además del punto", () => {
    render(<Wrapper />);
    const input = screen.getByLabelText(
      /Monto a aplicar: FAC 001-001-000031760/,
    ) as HTMLInputElement;

    fireEvent.change(input, { target: { value: "20,50" } });
    expect(input.value).toBe("20,50");

    const applied = JSON.parse(screen.getByTestId("applicationLines").textContent ?? "[]");
    expect(applied).toEqual([{ accountsPayableInstallmentId: "inst-1", amountApplied: 20.5 }]);
  });

  it("permite aplicar 43.53 completo escribiéndolo directamente", () => {
    render(<Wrapper />);
    const input = screen.getByLabelText(
      /Monto a aplicar: FAC 001-001-000031760/,
    ) as HTMLInputElement;

    fireEvent.change(input, { target: { value: "43.53" } });
    expect(input.value).toBe("43.53");

    const applied = JSON.parse(screen.getByTestId("applicationLines").textContent ?? "[]");
    expect(applied).toEqual([{ accountsPayableInstallmentId: "inst-1", amountApplied: 43.53 }]);
  });

  it("muestra un mensaje claro si el proveedor no tiene cartera pendiente", () => {
    render(<Wrapper rows={[]} />);
    expect(
      screen.getByText("Este proveedor no tiene cuentas por pagar pendientes."),
    ).toBeTruthy();
  });
});
