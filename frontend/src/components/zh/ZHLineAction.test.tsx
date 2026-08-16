// @vitest-environment jsdom
import { useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { ZHLineAction } from "./ZHLineAction";
import { ZHLineActionItem } from "./ZHLineActionItem";

afterEach(() => {
  cleanup();
});

describe("ZHLineAction kind=button (default)", () => {
  it("ejecuta onClick al hacer click", () => {
    const onClick = vi.fn();
    render(
      <ZHLineAction icon="content_copy" label="Duplicar" onClick={onClick} />,
    );
    fireEvent.click(screen.getByRole("button"));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("mantiene el nombre accesible (title/aria-label) igual al label", () => {
    render(<ZHLineAction icon="content_copy" label="Duplicar" />);
    const btn = screen.getByRole("button");
    expect(btn.getAttribute("aria-label")).toBe("Duplicar");
    expect(btn.getAttribute("title")).toBe("Duplicar");
  });

  it("respeta title explícito distinto del label", () => {
    render(
      <ZHLineAction icon="delete" label="Eliminar" title="Eliminar línea" />,
    );
    expect(screen.getByRole("button").getAttribute("title")).toBe(
      "Eliminar línea",
    );
  });

  it("no ejecuta onClick cuando disabled=true", () => {
    const onClick = vi.fn();
    render(
      <ZHLineAction
        icon="content_copy"
        label="Duplicar"
        onClick={onClick}
        disabled
      />,
    );
    fireEvent.click(screen.getByRole("button"));
    expect(onClick).not.toHaveBeenCalled();
  });
});

function MenuHarness(props: { closeOnSelect?: boolean }) {
  const [open, setOpen] = useState(false);
  return (
    <ZHLineAction
      kind="menu"
      icon="menu"
      label="Acciones del producto"
      open={open}
      onOpenChange={setOpen}
      closeOnSelect={props.closeOnSelect}
    >
      <ZHLineActionItem icon="visibility" label="Ver producto" />
    </ZHLineAction>
  );
}

describe("ZHLineAction kind=menu", () => {
  it("renderiza aria-haspopup=menu en el trigger", () => {
    render(<MenuHarness />);
    expect(
      screen.getByRole("button").getAttribute("aria-haspopup"),
    ).toBe("menu");
  });

  it("refleja aria-expanded según el estado open", () => {
    render(<MenuHarness />);
    const trigger = screen.getByRole("button");
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(trigger);
    expect(trigger.getAttribute("aria-expanded")).toBe("true");
  });

  it("abre y cierra el panel con click en el trigger", () => {
    render(<MenuHarness />);
    const trigger = screen.getByRole("button");
    expect(screen.queryByRole("menu")).toBeNull();
    fireEvent.click(trigger);
    expect(screen.getByRole("menu")).toBeTruthy();
    fireEvent.click(trigger);
    expect(screen.queryByRole("menu")).toBeNull();
  });

  it("cierra con la tecla Escape", () => {
    render(<MenuHarness />);
    fireEvent.click(screen.getByRole("button"));
    expect(screen.getByRole("menu")).toBeTruthy();
    fireEvent.keyDown(window, { key: "Escape" });
    expect(screen.queryByRole("menu")).toBeNull();
  });

  it("cierra con click fuera del componente", () => {
    render(
      <div>
        <MenuHarness />
        <button type="button">fuera</button>
      </div>,
    );
    fireEvent.click(screen.getByRole("button", { name: "Acciones del producto" }));
    expect(screen.getByRole("menu")).toBeTruthy();
    fireEvent.pointerDown(screen.getByRole("button", { name: "fuera" }));
    expect(screen.queryByRole("menu")).toBeNull();
  });

  it("respeta disabled en el trigger", () => {
    render(
      <ZHLineAction
        kind="menu"
        icon="menu"
        label="Acciones del producto"
        disabled
        open={false}
      />,
    );
    expect(
      (screen.getByRole("button") as HTMLButtonElement).disabled,
    ).toBe(true);
  });
});

describe("ZHLineActionItem", () => {
  it("ejecuta onClick al hacer click", () => {
    const onClick = vi.fn();
    render(<ZHLineActionItem label="Ver producto" onClick={onClick} />);
    fireEvent.click(screen.getByRole("menuitem"));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("no ejecuta onClick cuando disabled=true", () => {
    const onClick = vi.fn();
    render(
      <ZHLineActionItem label="Ver producto" onClick={onClick} disabled />,
    );
    fireEvent.click(screen.getByRole("menuitem"));
    expect(onClick).not.toHaveBeenCalled();
  });

  it("acepta tone=danger", () => {
    render(<ZHLineActionItem label="Eliminar" tone="danger" />);
    expect(
      screen.getByRole("menuitem").className.includes("zh-line-action-item--danger"),
    ).toBe(true);
  });
});
