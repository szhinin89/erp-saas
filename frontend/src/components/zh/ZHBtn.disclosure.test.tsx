// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { ZHBtn } from "./ZHForm";

afterEach(() => {
  cleanup();
});

describe("ZHBtn — modo disclosure (ZH-DS-DISCLOSURE-01)", () => {
  it("expone aria-expanded, ícono y chevrón decorativos sin alterar el nombre accesible", () => {
    const { rerender } = render(
      <ZHBtn size="sm" icon="compare_arrows" expanded={false} aria-controls="panel">Diferencias (2)</ZHBtn>,
    );
    const btn = screen.getByRole("button", { name: "Diferencias (2)" });
    expect(btn.className).toContain("zh-btn--disclosure");
    expect(btn.className).toContain("zh-btn--secondary");
    expect(btn.getAttribute("aria-expanded")).toBe("false");
    expect(btn.getAttribute("aria-controls")).toBe("panel");
    expect(btn.querySelector(".zh-btn__icon")?.getAttribute("aria-hidden")).toBe("true");
    expect(btn.querySelector(".zh-btn__chevron")?.textContent).toBe("expand_more");

    rerender(<ZHBtn size="sm" icon="compare_arrows" expanded aria-controls="panel">Diferencias (2)</ZHBtn>);
    expect(btn.getAttribute("aria-expanded")).toBe("true");
  });

  it("sin expanded no es disclosure: no agrega aria-expanded ni chevrón", () => {
    render(<ZHBtn>Guardar</ZHBtn>);
    const btn = screen.getByRole("button", { name: "Guardar" });
    expect(btn.hasAttribute("aria-expanded")).toBe(false);
    expect(btn.className).not.toContain("zh-btn--disclosure");
    expect(btn.querySelector(".zh-btn__chevron")).toBeNull();
  });

  it("es un <button> nativo: activable por teclado y bloqueado cuando está deshabilitado", () => {
    const onClick = vi.fn();
    render(<ZHBtn type="button" expanded={false} onClick={onClick} disabled>Historial</ZHBtn>);
    const btn = screen.getByRole("button", { name: "Historial" }) as HTMLButtonElement;
    expect(btn.tagName).toBe("BUTTON");
    expect(btn.disabled).toBe(true);
    fireEvent.click(btn);
    expect(onClick).not.toHaveBeenCalled();
  });
});
