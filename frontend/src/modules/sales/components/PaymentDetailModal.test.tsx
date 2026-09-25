// @vitest-environment jsdom
import type { ComponentProps } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, render, screen, cleanup, fireEvent } from "@testing-library/react";
import { PaymentDetailModal } from "./PaymentDetailModal";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

afterEach(() => {
  cleanup();
});

const BANK_ACCOUNT_PICHINCHA = "bank-account-pichincha";
const BANK_ACCOUNT_GUAYAQUIL = "bank-account-guayaquil";

function renderModal(
  overrides: Partial<ComponentProps<typeof PaymentDetailModal>> = {},
) {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  const utils = render(
    <PaymentDetailModal
      open
      methodName="Transferencia"
      detailType="Transfer"
      requiresReference
      bankAccountOptions={[
        { id: BANK_ACCOUNT_PICHINCHA, label: "Pichincha — Cta. Principal — Corriente ****3456" },
        { id: BANK_ACCOUNT_GUAYAQUIL, label: "Guayaquil — Cta. Secundaria — Ahorros ****7890" },
      ]}
      initialRows={[
        {
          _k: 1,
          amount: 10,
          transfer: {
            companyBankAccountId: BANK_ACCOUNT_PICHINCHA,
            receiptNumber: "134010011",
            transferDate: "2026-09-17",
          },
        },
        {
          _k: 2,
          amount: 20,
          transfer: {
            companyBankAccountId: BANK_ACCOUNT_GUAYAQUIL,
            receiptNumber: "998877",
            transferDate: "2026-09-17",
          },
        },
      ]}
      initialKey={3}
      available={100}
      onConfirm={onConfirm}
      onCancel={onCancel}
      {...overrides}
    />,
  );
  return { ...utils, onConfirm, onCancel };
}

describe("PaymentDetailModal — botón eliminar fila (SALES-DS-PAYMENT-REMOVE-03)", () => {
  it("renderiza un botón eliminar por cada fila", () => {
    renderModal();
    expect(screen.getAllByTitle("Eliminar")).toHaveLength(2);
  });

  it('usa el ícono de basurero ("delete"), no "close" (SALES-DS-PAYMENT-REMOVE-03A)', () => {
    renderModal();
    const removeButtons = screen.getAllByTitle("Eliminar");
    removeButtons.forEach((btn) => {
      expect(btn.querySelector(".material-symbols-outlined")?.textContent).toBe(
        "delete",
      );
      expect(btn.textContent).not.toContain("close");
    });
  });

  it("click en eliminar remueve solo la fila correspondiente", () => {
    renderModal();
    const removeButtons = screen.getAllByTitle("Eliminar");
    fireEvent.click(removeButtons[0]);

    expect(screen.getAllByTitle("Eliminar")).toHaveLength(1);
    expect(screen.getByDisplayValue("998877")).toBeTruthy();
    expect(screen.queryByDisplayValue("134010011")).toBeNull();
  });

  it('no muestra texto visible "Eliminar" (icon-only)', () => {
    renderModal();
    expect(screen.queryByText("Eliminar")).toBeNull();
  });

  it("conserva aria-label/title Eliminar", () => {
    renderModal();
    const btn = screen.getAllByTitle("Eliminar")[0];
    expect(btn.getAttribute("aria-label")).toBe("Eliminar");
    expect(btn.getAttribute("title")).toBe("Eliminar");
  });

  it("no hay estilos inline en el botón eliminar", () => {
    renderModal();
    screen.getAllByTitle("Eliminar").forEach((btn) => {
      expect(btn.getAttribute("style")).toBeNull();
    });
  });
});

describe("PaymentDetailModal — total del footer migrado a ZHMoneyValue (SALES-DS-MONEY-12)", () => {
  it("el total del footer (10 + 20 = 30) usa ZHMoneyValue con emphasis=strong", () => {
    const { container } = renderModal();

    const label = container.querySelector(".zh-modal-footer-label");
    const moneyValue = label?.querySelector(".zh-money-value");
    expect(moneyValue).toBeTruthy();
    expect(moneyValue?.textContent).toBe("$30.00");
    expect(moneyValue?.className).toContain("zh-money-value--strong");
  });

  it("los montos de las filas siguen siendo inputs editables, no ZHMoneyValue", () => {
    renderModal();

    const receiptInput = screen.getByDisplayValue("134010011");
    expect(receiptInput.tagName).toBe("INPUT");
  });

  it("no hay estilos inline en el total del footer", () => {
    const { container } = renderModal();

    const moneyValue = container.querySelector(
      ".zh-modal-footer-label .zh-money-value",
    );
    expect(moneyValue?.getAttribute("style")).toBeNull();
    moneyValue?.querySelectorAll("*").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});

describe("PaymentDetailModal — referencia obligatoria (SALES-TRANSFER-PAYMENT-REFERENCE-PAYLOAD-01)", () => {
  it("confirma normalmente cuando cada fila con monto ya tiene comprobante", () => {
    const { onConfirm } = renderModal();
    fireEvent.click(screen.getByText(/^Confirmar/));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('bloquea "Confirmar" y muestra un mensaje claro si falta el comprobante', () => {
    const { onConfirm } = renderModal({
      initialRows: [
        { _k: 1, amount: 10, transfer: { companyBankAccountId: BANK_ACCOUNT_PICHINCHA } },
      ],
    });

    expect(
      screen.getByText(/requiere un comprobante\/referencia/i),
    ).toBeTruthy();
    const confirmBtn = screen.getByText(/^Confirmar/).closest("button")!;
    expect(confirmBtn.disabled).toBe(true);
    fireEvent.click(confirmBtn);
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("no exige comprobante cuando el método no lo requiere (requiresReference=false)", () => {
    const { onConfirm } = renderModal({
      requiresReference: false,
      initialRows: [
        {
          _k: 1,
          amount: 10,
          transfer: {
            companyBankAccountId: BANK_ACCOUNT_PICHINCHA,
            transferDate: "2026-09-17",
          },
        },
      ],
    });

    expect(screen.queryByText(/requiere un comprobante\/referencia/i)).toBeNull();
    fireEvent.click(screen.getByText(/^Confirmar/));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });
});

describe("PaymentDetailModal — cuenta bancaria destino obligatoria (SALES-TRANSFER-BANK-ACCOUNT-01)", () => {
  it("el campo Banco se reemplaza por un select de cuentas bancarias para Transferencia", () => {
    renderModal();

    expect(screen.queryByPlaceholderText("Banco")).toBeNull();
    expect(screen.getAllByText("Cuenta bancaria destino")).toHaveLength(2);
    expect(
      screen.getAllByText("Pichincha — Cta. Principal — Corriente ****3456"),
    ).not.toHaveLength(0);
  });

  it('bloquea "Confirmar" y muestra un mensaje claro si falta la cuenta bancaria', () => {
    const { onConfirm } = renderModal({
      initialRows: [
        {
          _k: 1,
          amount: 10,
          transfer: { receiptNumber: "134010011", transferDate: "2026-09-17" },
        },
      ],
    });

    expect(
      screen.getByText(/requiere seleccionar una cuenta bancaria destino/i),
    ).toBeTruthy();
    const confirmBtn = screen.getByText(/^Confirmar/).closest("button")!;
    expect(confirmBtn.disabled).toBe(true);
    fireEvent.click(confirmBtn);
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("mantiene el campo Banco como texto libre para Tarjeta/Cheque (fuera de alcance)", () => {
    renderModal({
      detailType: "Card",
      requiresReference: false,
      initialRows: [{ _k: 1, amount: 10, card: { bankName: "Pichincha" } }],
    });

    expect(screen.getByPlaceholderText("Banco")).toBeTruthy();
    expect(screen.queryByText("Cuenta bancaria destino")).toBeNull();
  });

  it("seleccionar una cuenta bancaria habilita Confirmar y viaja en el payload", () => {
    const { onConfirm } = renderModal({
      initialRows: [
        {
          _k: 1,
          amount: 10,
          transfer: { receiptNumber: "134010011", transferDate: "2026-09-17" },
        },
      ],
    });

    const select = screen.getByDisplayValue("Seleccione una cuenta bancaria");
    fireEvent.change(select, { target: { value: BANK_ACCOUNT_PICHINCHA } });
    fireEvent.click(screen.getByText(/^Confirmar/));

    expect(onConfirm).toHaveBeenCalledTimes(1);
    const rows = onConfirm.mock.calls[0][0];
    expect(rows[0].transfer.companyBankAccountId).toBe(BANK_ACCOUNT_PICHINCHA);
  });
});

/** ZH-DESIGN-SYSTEM-PRECISION-04B — monto de cada fila declara `precision="money"`. */
describe("PaymentDetailModal — precision='money' (04B)", () => {
  it("editar el monto de una fila → onConfirm recibe el valor canónico (mismo contrato de filas)", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    const { onConfirm, container } = renderModal();
    const first = container.querySelector<HTMLInputElement>("input.zh-numeric-input")!;
    expect(first.value).toBe("10.00");
    fireEvent.focus(first);
    fireEvent.paste(first, { clipboardData: { getData: () => "12,5" } });
    fireEvent.blur(first);
    expect(first.value).toBe("12.50");
    fireEvent.click(screen.getByText(/^Confirmar/));
    const rows = onConfirm.mock.calls[0]![0] as { amount: number }[];
    expect(rows.map((r) => r.amount)).toEqual([12.5, 20]);
  });

  it("policy sintética moneyDecimals=3: la edición se normaliza a 3 decimales", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 });
    const { container } = renderModal();
    const first = container.querySelector<HTMLInputElement>("input.zh-numeric-input")!;
    fireEvent.focus(first);
    fireEvent.change(first, { target: { value: "7.125" } });
    fireEvent.blur(first);
    expect(first.value).toBe("7.125");
  });
});

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04G — `defaultValue={row.amount > 0 ? row.amount : ""}`: el input
 * (precision="money") aplica la escala; "" (sin monto) conserva su significado.
 */
describe("PaymentDetailModal — defaultValue canónico (04G)", () => {
  it("monto existente con la escala de money; monto 0 → vacío (placeholder)", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 });
    const { container } = renderModal({
      initialRows: [
        { _k: 1, amount: 10, transfer: { companyBankAccountId: BANK_ACCOUNT_PICHINCHA, receiptNumber: "1", transferDate: "2026-09-17" } },
        { _k: 2, amount: 0, transfer: { companyBankAccountId: BANK_ACCOUNT_PICHINCHA, receiptNumber: "2", transferDate: "2026-09-17" } },
      ],
    });
    const inputs = [...container.querySelectorAll<HTMLInputElement>("input.zh-numeric-input")];
    expect(inputs.map((i) => i.value)).toEqual(["10.000", ""]);
  });

  it("focus→blur sin editar y policy A→B: texto intacto; payload idéntico", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    const { container, onConfirm } = renderModal();
    const first = container.querySelector<HTMLInputElement>("input.zh-numeric-input")!;
    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 }));
    fireEvent.focus(first);
    fireEvent.blur(first);
    expect(first.value).toBe("10.00");
    fireEvent.click(screen.getByText(/^Confirmar/));
    const rows = onConfirm.mock.calls[0]![0] as { amount: number }[];
    expect(rows.map((r) => r.amount)).toEqual([10, 20]);
  });
});
