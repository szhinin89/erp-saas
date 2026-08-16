// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { ZHPickerSelectedValue } from "./ZHPickerSelectedValue";

afterEach(() => {
  cleanup();
});

describe("ZHPickerSelectedValue", () => {
  it("renderiza title", () => {
    render(<ZHPickerSelectedValue title="Juan Pérez" />);
    expect(screen.getByText("Juan Pérez")).toBeTruthy();
  });

  it("renderiza subtitle y meta", () => {
    render(
      <ZHPickerSelectedValue
        title="Juan Pérez"
        subtitle="Cliente frecuente"
        meta="1710034065"
      />,
    );
    expect(screen.getByText("Cliente frecuente")).toBeTruthy();
    expect(screen.getByText("1710034065")).toBeTruthy();
  });

  it("muestra el ícono close si onClear existe", () => {
    render(<ZHPickerSelectedValue title="Juan Pérez" onClear={() => {}} />);
    const btn = screen.getByTitle("Cambiar selección");
    expect(btn.querySelector(".material-symbols-outlined")?.textContent).toBe(
      "close",
    );
  });

  it("muestra el ícono edit si onEdit existe", () => {
    render(<ZHPickerSelectedValue title="Juan Pérez" onEdit={() => {}} />);
    const btn = screen.getByTitle("Editar");
    expect(btn.querySelector(".material-symbols-outlined")?.textContent).toBe(
      "edit",
    );
  });

  it("click en close llama a onClear", () => {
    const onClear = vi.fn();
    render(<ZHPickerSelectedValue title="Juan Pérez" onClear={onClear} />);
    fireEvent.click(screen.getByTitle("Cambiar selección"));
    expect(onClear).toHaveBeenCalledTimes(1);
  });

  it("click en edit llama a onEdit", () => {
    const onEdit = vi.fn();
    render(<ZHPickerSelectedValue title="Juan Pérez" onEdit={onEdit} />);
    fireEvent.click(screen.getByTitle("Editar"));
    expect(onEdit).toHaveBeenCalledTimes(1);
  });

  it("si no hay onEdit, no muestra la acción de editar", () => {
    render(<ZHPickerSelectedValue title="Juan Pérez" onClear={() => {}} />);
    expect(screen.queryByTitle("Editar")).toBeNull();
  });

  it("si no hay onClear, no muestra la acción de cerrar", () => {
    render(<ZHPickerSelectedValue title="Juan Pérez" onEdit={() => {}} />);
    expect(screen.queryByTitle("Cambiar selección")).toBeNull();
  });

  it("sin onClear ni onEdit, no renderiza acciones", () => {
    const { container } = render(<ZHPickerSelectedValue title="Juan Pérez" />);
    expect(
      container.querySelector(".zh-picker-selected-value__actions"),
    ).toBeNull();
  });

  it("acepta clearLabel/editLabel personalizados", () => {
    render(
      <ZHPickerSelectedValue
        title="Juan Pérez"
        clearLabel="Cambiar cliente"
        editLabel="Editar cliente"
        onClear={() => {}}
        onEdit={() => {}}
      />,
    );
    expect(screen.getByTitle("Cambiar cliente")).toBeTruthy();
    expect(screen.getByTitle("Editar cliente")).toBeTruthy();
  });

  it("no hay estilos inline", () => {
    const { container } = render(
      <ZHPickerSelectedValue
        title="Juan Pérez"
        subtitle="Cliente frecuente"
        meta="1710034065"
        onClear={() => {}}
        onEdit={() => {}}
      />,
    );
    container.querySelectorAll("*").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});
