// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { PageShell } from "./PageShell";
import { ErpPageTemplate } from "../templates/ErpPageTemplate";
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

    expect(document.title).toBe("Geografía · ZH Technologies ERP");
  });

  it("ErpPageTemplate propaga el título a la pestaña vía PageShell", () => {
    render(
      <ErpPageTemplate title="Ajustes de inventario">
        <div>contenido</div>
      </ErpPageTemplate>,
    );

    expect(document.title).toBe("Ajustes de inventario · ZH Technologies ERP");
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

    expect(document.title).toBe("Ajuste AJ-000001 · ZH Technologies ERP");
  });
});
