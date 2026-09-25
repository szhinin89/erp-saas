// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import { useForm } from "react-hook-form";
import { I18nProvider } from "../../../i18n/i18n";
import { ManualCashMovementModal } from "./ManualCashMovementModal";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import { manualCashMovementTypeOptions } from "../constants/cashMovementTypes";
import type { RecordMovementFormValues } from "../schemas/cajaSchema";
import type { CashMovementReasonDto } from "../api/cajaService";

afterEach(() => cleanup());

const movementTypes = manualCashMovementTypeOptions((key: string, fallback?: string) => fallback ?? key);

const reasonsForIncome: CashMovementReasonDto[] = [
  { id: "reason-1", code: "CAMBIO_CAJA", name: "Cambio de caja chica", movementType: "ManualIncome", isActive: true, sortOrder: 1 },
];

/**
 * TREASURY-CASH-MANUAL-MOVEMENT-SHARED-MODAL-07 — mismo patrón de test que se usaría para
 * cualquier presentacional que reciba register/errors de un `useForm` del padre (ver
 * CashMovementReasonFormTab, sin test propio porque hoy solo se ejercita vía su página; aquí el
 * ticket pide explícitamente cubrir el componente en aislamiento). El harness reproduce
 * exactamente lo que useCajaPage.tsx le pasa al componente — sin duplicar la lógica del hook
 * (fetch de motivos, submit real, etc.), que sigue viviendo fuera de este archivo.
 */
function Harness(props: {
  open?: boolean;
  saving?: boolean;
  saveError?: string;
  selectedMovementType?: string;
  reasons?: CashMovementReasonDto[];
  reasonsLoading?: boolean;
  onSubmit?: () => void;
  onClose?: () => void;
}) {
  const form = useForm<RecordMovementFormValues>({
    defaultValues: { movementType: "", reasonId: "", amount: 0, description: "" },
  });

  return (
    <I18nProvider>
      <ManualCashMovementModal
        open={props.open ?? true}
        saving={props.saving ?? false}
        saveError={props.saveError ?? ""}
        register={form.register}
        errors={form.formState.errors}
        selectedMovementType={props.selectedMovementType ?? ""}
        movementTypes={movementTypes}
        reasons={props.reasons ?? []}
        reasonsLoading={props.reasonsLoading ?? false}
        onSubmit={(e) => {
          e.preventDefault();
          props.onSubmit?.();
        }}
        onClose={props.onClose ?? (() => {})}
      />
    </I18nProvider>
  );
}

describe("ManualCashMovementModal", () => {
  it("renderiza los campos correctos: Tipo, Motivo, Monto, Descripción adicional, Cancelar, Registrar", () => {
    render(<Harness />);

    expect(screen.getByText("Tipo")).toBeTruthy();
    expect(screen.getByText("Motivo")).toBeTruthy();
    expect(screen.getByText("Monto")).toBeTruthy();
    expect(screen.getByText("Descripción adicional")).toBeTruthy();
    expect(screen.getByText("Cancelar")).toBeTruthy();
    expect(screen.getByText("Registrar")).toBeTruthy();
  });

  it("no renderiza nada si open=false", () => {
    render(<Harness open={false} />);

    expect(screen.queryByText("Registrar movimiento manual de efectivo")).toBeNull();
  });

  it("el Tipo lista exactamente los tipos manuales recibidos (misma fuente SSOT, sin hardcode propio)", () => {
    render(<Harness />);

    const select = screen.getByLabelText(/^Tipo/) as HTMLSelectElement;
    const values = Array.from(select.options).map((o) => o.value);
    expect(values).toEqual(["", "ManualIncome", "ManualExpense", "Withdrawal"]);
  });

  it("el Motivo refleja los `reasons` recibidos y se deshabilita sin Tipo seleccionado", () => {
    render(<Harness selectedMovementType="" reasons={[]} />);
    const emptySelect = screen.getByLabelText(/^Motivo/) as HTMLSelectElement;
    expect(emptySelect.disabled).toBe(true);
    cleanup();

    render(<Harness selectedMovementType="ManualIncome" reasons={reasonsForIncome} />);
    const populatedSelect = screen.getByLabelText(/^Motivo/) as HTMLSelectElement;
    expect(populatedSelect.disabled).toBe(false);
    expect(screen.getByText("Cambio de caja chica")).toBeTruthy();
  });

  it("cambiar el Tipo en el DOM actualiza el valor real del formulario del padre (mismo mecanismo que antes de extraer el componente)", () => {
    const readback: { values: RecordMovementFormValues | null } = { values: null };
    function HarnessWithReadback() {
      const form = useForm<RecordMovementFormValues>({
        defaultValues: { movementType: "", reasonId: "", amount: 0, description: "" },
      });
      readback.values = form.watch();
      return (
        <I18nProvider>
          <ManualCashMovementModal
            open
            saving={false}
            saveError=""
            register={form.register}
            errors={form.formState.errors}
            selectedMovementType={form.watch("movementType")}
            movementTypes={movementTypes}
            reasons={[]}
            reasonsLoading={false}
            onSubmit={(e) => e.preventDefault()}
            onClose={() => {}}
          />
        </I18nProvider>
      );
    }
    render(<HarnessWithReadback />);

    fireEvent.change(screen.getByLabelText(/^Tipo/), { target: { value: "Withdrawal" } });

    expect(readback.values?.movementType).toBe("Withdrawal");
  });

  it("Cancelar llama a onClose", () => {
    const onClose = vi.fn();
    render(<Harness onClose={onClose} />);

    fireEvent.click(screen.getByText("Cancelar"));

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("enviar el formulario llama a onSubmit", async () => {
    const onSubmit = vi.fn();
    render(<Harness onSubmit={onSubmit} />);

    fireEvent.click(screen.getByText("Registrar"));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
  });

  it("saving=true deshabilita Registrar y Cancelar, y cambia la etiqueta a Guardando...", () => {
    render(<Harness saving />);

    expect(screen.getByText("Guardando...").closest("button")).toHaveProperty("disabled", true);
    expect(screen.getByText("Cancelar").closest("button")).toHaveProperty("disabled", true);
  });

  it("saveError se muestra visible", () => {
    render(<Harness saveError="El motivo seleccionado está inactivo." />);

    expect(screen.getByText("El motivo seleccionado está inactivo.")).toBeTruthy();
  });

  it("sin saveError, no muestra el aviso de error", () => {
    render(<Harness saveError="" />);

    expect(screen.queryByText("Error:")).toBeNull();
  });
});

/** ZH-DESIGN-SYSTEM-PRECISION-04B — "Monto" declara `precision="money"` (antes `decimals={2}`). */
describe("ManualCashMovementModal — precision='money' (04B)", () => {
  function MoneyHarness({ onValues }: { onValues: (amount: unknown) => void }) {
    const form = useForm<RecordMovementFormValues>({
      defaultValues: { movementType: "", reasonId: "", amount: 0, description: "" },
    });
    return (
      <I18nProvider>
        <ManualCashMovementModal
          open
          saving={false}
          saveError=""
          register={form.register}
          errors={form.formState.errors}
          selectedMovementType=""
          movementTypes={movementTypes}
          reasons={[]}
          reasonsLoading={false}
          onSubmit={(e) => {
            e.preventDefault();
            onValues(form.getValues("amount"));
          }}
          onClose={() => {}}
        />
      </I18nProvider>
    );
  }

  it("edición con coma → '12.50' y el formulario recibe el valor canónico", async () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 2 });
    const onValues = vi.fn();
    render(<MoneyHarness onValues={onValues} />);
    const amount = document.querySelector<HTMLInputElement>("input.zh-numeric-input")!;
    fireEvent.focus(amount);
    fireEvent.paste(amount, { clipboardData: { getData: () => "12,5" } });
    fireEvent.blur(amount);
    expect(amount.value).toBe("12.50");
    fireEvent.click(screen.getByText("Registrar"));
    await waitFor(() => expect(onValues).toHaveBeenCalledWith("12.50"));
  });

  it("policy sintética moneyDecimals=3: admite 3 decimales y normaliza la edición a 3", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, moneyDecimals: 3 });
    render(<MoneyHarness onValues={() => {}} />);
    const amount = document.querySelector<HTMLInputElement>("input.zh-numeric-input")!;
    fireEvent.focus(amount);
    fireEvent.change(amount, { target: { value: "5.1" } });
    fireEvent.blur(amount);
    expect(amount.value).toBe("5.100");
  });
});
