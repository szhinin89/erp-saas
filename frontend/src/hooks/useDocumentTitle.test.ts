// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from "vitest";
import { renderHook } from "@testing-library/react";
import {
  DEFAULT_DOCUMENT_TITLE,
  useDocumentTitle,
} from "./useDocumentTitle";

beforeEach(() => {
  document.title = DEFAULT_DOCUMENT_TITLE;
});

describe("useDocumentTitle", () => {
  it("usa el título tal cual, sin sufijo por defecto", () => {
    renderHook(() => useDocumentTitle("Geografía"));

    expect(document.title).toBe("Geografía");
  });

  it("usa el fallback exacto cuando el título es undefined", () => {
    renderHook(() => useDocumentTitle(undefined));

    expect(document.title).toBe("ZH Technologies · ERP");
  });

  it("usa el fallback exacto cuando el título es vacío o solo espacios", () => {
    document.title = "Título previo";
    renderHook(() => useDocumentTitle("   "));

    expect(document.title).toBe("ZH Technologies · ERP");
  });

  it("un título dinámico se usa tal cual", () => {
    renderHook(() => useDocumentTitle("Ajuste AJ-000001"));

    expect(document.title).toBe("Ajuste AJ-000001");
  });

  it("respeta un sufijo explícito (opt-in)", () => {
    renderHook(() => useDocumentTitle("Caja", { suffix: "ERP Demo" }));

    expect(document.title).toBe("Caja · ERP Demo");
  });

  it("usa loadingTitle mientras el llamador indica carga", () => {
    const { rerender } = renderHook(
      ({ title, loading }: { title: string | undefined; loading: boolean }) =>
        useDocumentTitle(title, { loadingTitle: "Cargando…", loading }),
      { initialProps: { title: undefined as string | undefined, loading: true } },
    );

    expect(document.title).toBe("Cargando…");

    rerender({ title: "Ajuste AJ-000001", loading: false });
    expect(document.title).toBe("Ajuste AJ-000001");
  });

  it("actualiza el título cuando cambia el valor (registro cargado async)", () => {
    const { rerender } = renderHook(
      ({ title }: { title: string }) => useDocumentTitle(title),
      { initialProps: { title: "Nuevo ajuste" } },
    );

    expect(document.title).toBe("Nuevo ajuste");

    rerender({ title: "Ajuste AJ-000001" });
    expect(document.title).toBe("Ajuste AJ-000001");
  });

  it("al desmontar NO restaura el fallback (evita parpadeo entre navegaciones)", () => {
    const { unmount } = renderHook(() => useDocumentTitle("Compras"));

    expect(document.title).toBe("Compras");

    unmount();

    // El título permanece: la pantalla entrante lo fija en su propio montaje.
    expect(document.title).toBe("Compras");
  });
});
