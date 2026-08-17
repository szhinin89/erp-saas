// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { ZHBtn } from "./ZHForm";

afterEach(() => {
  cleanup();
});

describe("ZHBtn — variant cta (SALES-DS-CTA-11)", () => {
  it('variant="cta" renderiza la clase zh-btn--cta', () => {
    render(<ZHBtn variant="cta">Emitir Factura (F8)</ZHBtn>);
    const btn = screen.getByText("Emitir Factura (F8)").closest("button")!;
    expect(btn.className).toContain("zh-btn");
    expect(btn.className).toContain("zh-btn--cta");
  });

  it("disabled funciona igual que en el resto de variantes", () => {
    const onClick = vi.fn();
    render(
      <ZHBtn variant="cta" onClick={onClick} disabled>
        Emitir Factura (F8)
      </ZHBtn>,
    );
    const btn = screen.getByText("Emitir Factura (F8)").closest("button")!;
    expect(btn.disabled).toBe(true);
    fireEvent.click(btn);
    expect(onClick).not.toHaveBeenCalled();
  });

  it("no fuerza ningún type — se respeta el que reciba (o el default del navegador)", () => {
    render(
      <ZHBtn variant="cta" type="button">
        Emitir Factura (F8)
      </ZHBtn>,
    );
    const btn = screen.getByText("Emitir Factura (F8)").closest("button")!;
    expect(btn.getAttribute("type")).toBe("button");
  });

  it("renderiza contenido con ícono (children) igual que las demás variantes", () => {
    const { container } = render(
      <ZHBtn variant="cta">
        <span className="material-symbols-outlined zh-icon-lg">
          play_arrow
        </span>
        Emitir Factura (F8)
      </ZHBtn>,
    );
    expect(container.querySelector(".zh-icon-lg")?.textContent).toBe(
      "play_arrow",
    );
    expect(screen.getByText("Emitir Factura (F8)")).toBeTruthy();
  });

  it("className adicional se combina con las clases base", () => {
    render(
      <ZHBtn variant="cta" className="sales-emit-extra">
        Emitir Factura (F8)
      </ZHBtn>,
    );
    const btn = screen.getByText("Emitir Factura (F8)").closest("button")!;
    expect(btn.className).toContain("zh-btn--cta");
    expect(btn.className).toContain("sales-emit-extra");
  });

  it("no hay estilos inline", () => {
    render(
      <ZHBtn variant="cta">
        <span className="material-symbols-outlined zh-icon-lg">
          play_arrow
        </span>
        Emitir Factura (F8)
      </ZHBtn>,
    );
    const btn = screen.getByText("Emitir Factura (F8)").closest("button")!;
    expect(btn.getAttribute("style")).toBeNull();
    btn.querySelectorAll("*").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});
