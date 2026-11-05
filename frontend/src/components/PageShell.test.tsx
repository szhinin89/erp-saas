// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { PageShell } from "./PageShell";
import { ReportPage } from "./ReportPageTemplate";
import { ErpPageTemplate } from "../templates/ErpPageTemplate";
import { ConfigTabsLayout } from "./shared/ConfigTabsLayout";
import { DEFAULT_DOCUMENT_TITLE } from "../hooks/useDocumentTitle";

beforeEach(() => {
  document.title = DEFAULT_DOCUMENT_TITLE;
});

describe("PageShell — título de pestaña", () => {
  it("fija document.title a partir del prop title", () => {
    render(
      <PageShell title="Geografía">
        <div>contenido</div>
      </PageShell>,
    );

    expect(document.title).toBe("Geografía");
  });

  it("ErpPageTemplate propaga el título a la pestaña vía PageShell", () => {
    render(
      <ErpPageTemplate title="Ajustes de inventario">
        <div>contenido</div>
      </ErpPageTemplate>,
    );

    expect(document.title).toBe("Ajustes de inventario");
  });

  it("ReportPage propaga el título a la pestaña", () => {
    render(
      <ReportPage title="Reporte de Stock">
        <div>contenido</div>
      </ReportPage>,
    );

    expect(document.title).toBe("Reporte de Stock");
  });

  it("un re-render con nuevo título actualiza la pestaña", () => {
    const { rerender } = render(
      <PageShell title="Nuevo ajuste">
        <div>contenido</div>
      </PageShell>,
    );

    rerender(
      <PageShell title="Ajuste AJ-000001">
        <div>contenido</div>
      </PageShell>,
    );

    expect(document.title).toBe("Ajuste AJ-000001");
  });

  it("ConfigTabsLayout no duplica cabecera/título al usarse dentro de ErpPageTemplate", () => {
    render(
      <ErpPageTemplate title="Documentos y flujos">
        <ConfigTabsLayout
          activeTab="list"
          onTabChange={() => {}}
          editorLabel="Editar flujo documental"
          listContent={<div>lista de flujos</div>}
          editorContent={<div>editor de flujo</div>}
        />
      </ErpPageTemplate>,
    );

    // El título de pestaña sigue siendo responsabilidad única de PageShell.
    expect(document.title).toBe("Documentos y flujos");
    // ConfigTabsLayout solo aporta el tab-switcher + contenido, no un h1/kicker propio.
    expect(screen.getAllByText("Documentos y flujos")).toHaveLength(1);
    expect(screen.getByRole("tablist")).toBeTruthy();
    expect(screen.getByText("lista de flujos")).toBeTruthy();
  });
});

describe("PageShell — variante opcional", () => {
  it("conserva el markup al omitir variante o pasar default", () => {
    const props = { title: "Catálogo", kicker: "Maestros", subtitle: "Descripción", action: <button>Crear</button>, children: <div>contenido</div> };
    const { container, rerender } = render(<PageShell {...props} />);
    const markup = container.innerHTML;
    expect(container.querySelector("header")?.className).toBe("zh-screen-heading");
    rerender(<PageShell {...props} headingVariant="default" />);
    expect(container.innerHTML).toBe(markup);
  });

  it("propaga compact por la plantilla conservando semántica, contexto y acciones", () => {
    const { container, rerender } = render(
      <ErpPageTemplate title="Catálogo" kicker="Maestros" subtitle="Descripción extensa" headingVariant="compact" action={<button>Crear</button>}>
        <div>contenido</div>
      </ErpPageTemplate>,
    );
    const header = container.querySelector("header")!;
    expect(header.className).toBe("zh-screen-heading zh-screen-heading--compact");
    expect(header.querySelectorAll("h1")).toHaveLength(1);
    expect(header.querySelector("h1")?.textContent).toBe("Catálogo");
    expect(header.querySelector(".zh-screen-heading-kicker")?.textContent).toBe("Maestros");
    expect(header.querySelector("p")?.textContent).toBe("Descripción extensa");
    expect(header.querySelector("button")?.textContent).toBe("Crear");
    expect(header.querySelector(".zh-screen-heading-chip")?.getAttribute("aria-hidden")).toBe("true");
    expect(document.title).toBe("Catálogo");
    rerender(<ErpPageTemplate title="Catálogo"><div>contenido</div></ErpPageTemplate>);
    expect(container.querySelector("header")?.className).toBe("zh-screen-heading");
  });

  it("admite compact con solo título", () => {
    const { container } = render(<PageShell title="Solo título" headingVariant="compact"><div>contenido</div></PageShell>);
    expect(container.querySelector("header h1")?.textContent).toBe("Solo título");
    expect(container.querySelector("header p")).toBeNull();
    expect(container.querySelector(".zh-screen-heading-right")).toBeNull();
    expect(container.querySelector(".zh-screen-heading-kicker")).toBeNull();
  });
});
