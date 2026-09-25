// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { act, cleanup, fireEvent, render } from "@testing-library/react";
import { useForm, type UseFormReturn } from "react-hook-form";
import { useEffect } from "react";
import { I18nProvider } from "../../../../i18n/i18n";
import { WarehouseFormTab } from "./WarehouseFormTab";
import {
  defaultWarehouseValues,
  type WarehouseFormValues,
} from "../../../../schemas/inventory/warehouseSchema";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04D1 — "Capacidad Total (m³)" es un override contractual con la escala
 * de su persistencia (Warehouse.Capacity → numeric(18,4)), no el literal 2 anterior (no respaldado
 * por ninguna regla): una capacidad guardada con 4 decimales no se trunca al visitarla ni al editarla.
 */

afterEach(() => cleanup());

function renderForm(capacity: number | null) {
  let form: UseFormReturn<WarehouseFormValues> | null = null;
  const expose = (f: UseFormReturn<WarehouseFormValues>) => {
    form = f;
  };
  function Harness() {
    const f = useForm<WarehouseFormValues>({ defaultValues: { ...defaultWarehouseValues, capacity } });
    useEffect(() => expose(f), [f]);
    return (
      <I18nProvider>
        <WarehouseFormTab
          editingId="wh-1"
          editCode="B01"
          saving={false}
          saveError=""
          branches={[]}
          register={f.register}
          control={f.control}
          errors={f.formState.errors}
          onSave={() => {}}
          onCancel={() => {}}
        />
      </I18nProvider>
    );
  }
  const { container } = render(<Harness />);
  const input = container.querySelector<HTMLInputElement>('input[name="capacity"]')!;
  return { input, getValue: () => form?.getValues("capacity") };
}

describe("WarehouseFormTab — capacidad con la escala contractual numeric(18,4) (04D1)", () => {
  it("capacidad guardada 12.3456: visitar el campo no la altera", async () => {
    const { input, getValue } = renderForm(12.3456);
    expect(input.value).toBe("12.3456");
    await act(async () => {
      fireEvent.focus(input);
      fireEvent.blur(input);
    });
    expect(input.value).toBe("12.3456");
    expect(Number(getValue())).toBe(12.3456);
  });

  it("editar conserva hasta 4 decimales (sin pérdida silenciosa) y bloquea el 5.º", async () => {
    const { input, getValue } = renderForm(null);
    await act(async () => {
      fireEvent.focus(input);
      fireEvent.change(input, { target: { value: "100.1234" } });
    });
    input.setSelectionRange(8, 8);
    expect(fireEvent.keyDown(input, { key: "5" })).toBe(false);
    await act(async () => {
      fireEvent.blur(input);
    });
    expect(input.value).toBe("100.1234");
    expect(Number(getValue())).toBe(100.1234);
  });
});
