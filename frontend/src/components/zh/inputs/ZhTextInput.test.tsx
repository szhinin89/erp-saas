// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { createRef } from "react";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useForm } from "react-hook-form";
import { ZhTextInput } from "./ZhTextInput";

afterEach(cleanup);

describe("ZhTextInput — prop `type`", () => {
  it("por defecto es text (sin cambio para los consumidores existentes)", () => {
    const { container } = render(<ZhTextInput />);
    expect(container.querySelector("input")!.type).toBe("text");
  });

  it.each(["email", "password", "search", "tel", "url"] as const)("admite type=%s", (type) => {
    const { container } = render(<ZhTextInput type={type} />);
    expect(container.querySelector("input")!.type).toBe(type);
  });

  it("type dinámico (mostrar/ocultar contraseña) se actualiza sin remontar el nodo", () => {
    const { container, rerender } = render(<ZhTextInput type="password" defaultValue="secreto" />);
    const input = container.querySelector("input")!;
    rerender(<ZhTextInput type="text" defaultValue="secreto" />);
    expect(container.querySelector("input")).toBe(input);
    expect(input.type).toBe("text");
  });

  it("conserva forwardRef, register() y atributos nativos (autoComplete, inputMode)", () => {
    const ref = createRef<HTMLInputElement>();
    let value = "";
    function Form() {
      const { register, watch } = useForm<{ email: string }>({ defaultValues: { email: "" } });
      value = watch("email");
      return <ZhTextInput type="email" autoComplete="email" inputMode="email" {...register("email")} />;
    }
    render(<><ZhTextInput ref={ref} type="email" /><Form /></>);
    expect(ref.current?.type).toBe("email");
    const input = screen.getAllByRole("textbox")[1] as HTMLInputElement;
    expect(input.getAttribute("autocomplete")).toBe("email");
    fireEvent.change(input, { target: { value: "a@b.ec" } });
    expect(value).toBe("a@b.ec");
  });

  it("mode=letters sigue filtrando teclas con type=text", () => {
    const onKeyDown = vi.fn();
    const { container } = render(<ZhTextInput mode="letters" onKeyDown={onKeyDown} />);
    const allowed = fireEvent.keyDown(container.querySelector("input")!, { key: "7" });
    expect(allowed).toBe(false); // preventDefault → el dígito se bloquea
    expect(onKeyDown).toHaveBeenCalled();
  });

  it("density=compact añade la clase compacta", () => {
    const { container } = render(<ZhTextInput density="compact" className="mono" />);
    expect(container.querySelector("input")!.className).toBe("mono zh-input--compact");
  });
});
