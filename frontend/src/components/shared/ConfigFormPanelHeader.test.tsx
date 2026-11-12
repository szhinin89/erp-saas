// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { ConfigFormPanelHeader } from "./ConfigFormPanelHeader";

afterEach(cleanup);

describe("ConfigFormPanelHeader", () => {
  it("renderiza icono, título y subtítulo con las clases de cabecera de panel", () => {
    const { container } = render(
      <ConfigFormPanelHeader icon="edit" title="Editar Sucursal" subtitle="Código: 001" />,
    );
    expect(container.querySelector(".cfg-panel-hd__icon")!.textContent).toBe("edit");
    expect(container.querySelector(".cfg-panel-hd__title")!.textContent).toBe("Editar Sucursal");
    expect(container.querySelector(".cfg-panel-hd__sub")!.textContent).toBe("Código: 001");
  });

  it("sin subtítulo ni pestaña no renderiza ni .cfg-panel-hd__sub ni el tablist", () => {
    const { container } = render(<ConfigFormPanelHeader icon="add_circle" title="Nueva Caja" />);
    expect(container.querySelector(".cfg-panel-hd__sub")).toBeNull();
    expect(container.querySelector("[role=tablist]")).toBeNull();
  });

  it("con tabLabel muestra la pestaña General activa e inerte", () => {
    const { container } = render(
      <ConfigFormPanelHeader icon="edit" title="X" tabLabel="General" />,
    );
    const tab = screen.getByRole("tab", { name: "General" });
    expect(tab.getAttribute("aria-selected")).toBe("true");
    expect(container.querySelector(".prd-tabs")!.className).toContain("cfg-panel-tabs");
    tab.click(); // no navega ni lanza
  });
});
