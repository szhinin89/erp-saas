// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { createRef } from "react";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useForm } from "react-hook-form";
import { ZhCheckbox } from "./ZhCheckbox";

afterEach(cleanup);

describe("ZhCheckbox", () => {
  it("sin label renderiza solo el input nativo (celdas de tabla, selección)", () => {
    const { container } = render(<ZhCheckbox aria-label="Seleccionar fila" />);
    expect(container.children).toHaveLength(1);
    const input = container.querySelector("input")!;
    expect(input.type).toBe("checkbox");
    expect(screen.getByLabelText("Seleccionar fila")).toBe(input);
  });

  it("con label envuelve en <label class=zh-checkbox-label> y asocia el texto", () => {
    const { container } = render(<ZhCheckbox label="Mostrar inactivos" labelClassName="extra" />);
    const label = container.querySelector("label")!;
    expect(label.className).toBe("zh-checkbox-label extra");
    expect(screen.getByLabelText("Mostrar inactivos")).toBe(label.querySelector("input"));
  });

  it("estado controlado y eventos nativos", () => {
    const onChange = vi.fn();
    const { rerender } = render(<ZhCheckbox checked={false} onChange={onChange} />);
    const input = screen.getByRole("checkbox") as HTMLInputElement;
    fireEvent.click(input);
    expect(onChange).toHaveBeenCalledTimes(1);
    expect(input.checked).toBe(false); // controlado: el padre decide
    rerender(<ZhCheckbox checked onChange={onChange} />);
    expect((screen.getByRole("checkbox") as HTMLInputElement).checked).toBe(true);
  });

  it("respeta disabled, name y defaultChecked (no controlado)", () => {
    render(<ZhCheckbox name="activo" defaultChecked disabled />);
    const input = screen.getByRole("checkbox") as HTMLInputElement;
    expect(input.name).toBe("activo");
    expect(input.checked).toBe(true);
    expect(input.disabled).toBe(true);
  });

  it("forwardRef entrega el input", () => {
    const ref = createRef<HTMLInputElement>();
    render(<ZhCheckbox ref={ref} />);
    expect(ref.current?.type).toBe("checkbox");
  });

  it("compatible con React Hook Form register()", () => {
    let latest: boolean | undefined;
    function Form() {
      const { register, watch } = useForm<{ isActive: boolean }>({ defaultValues: { isActive: false } });
      latest = watch("isActive");
      return <ZhCheckbox {...register("isActive")} label="Activo" />;
    }
    render(<Form />);
    fireEvent.click(screen.getByLabelText("Activo"));
    expect(latest).toBe(true);
  });
});
