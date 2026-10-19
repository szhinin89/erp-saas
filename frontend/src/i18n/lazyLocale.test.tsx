// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import type { ReactNode } from "react";
import es from "./locales/es.json";

/**
 * P-01 — `en` se descarga bajo demanda; `es` está disponible al instante. Cada test usa módulos
 * nuevos (sin la precarga de setupI18n) y sustituye la descarga de `en` para controlarla.
 */
const KEY = "common.errorGeneric";
const EN = { [KEY]: "EN generic" };

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

async function freshI18n(load: () => Promise<Record<string, string>>) {
  vi.resetModules();
  const dictionaries = await import("./dictionaries");
  dictionaries.setLocaleLoaderForTests("en", load);
  const { I18nProvider, useI18n } = await import("./i18n");
  const { renderHook, act } = await import("@testing-library/react");
  const wrapper = ({ children }: { children: ReactNode }) => <I18nProvider>{children}</I18nProvider>;
  const render = () => renderHook(() => useI18n(), { wrapper });
  const flush = () => act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 0));
  });
  return { ...dictionaries, render, act, flush };
}

beforeEach(() => localStorage.clear());
afterEach(() => vi.resetModules());

describe("carga diferida del idioma inglés (P-01)", () => {
  it("español disponible al instante; inglés no se descarga hasta pedirlo", async () => {
    const load = vi.fn(() => Promise.resolve(EN));
    const i18n = await freshI18n(load);
    expect(i18n.isLocaleLoaded("es")).toBe(true);
    expect(i18n.isLocaleLoaded("en")).toBe(false);
    const { result } = i18n.render();
    expect(result.current.locale).toBe("es");
    expect(result.current.t(KEY)).toBe(es[KEY]);
    expect(load).not.toHaveBeenCalled();
  });

  it("cambio a inglés: mantiene español hasta descargar, luego aplica y guarda la preferencia", async () => {
    const download = deferred<Record<string, string>>();
    const i18n = await freshI18n(() => download.promise);
    const { result } = i18n.render();
    i18n.act(() => result.current.setLocale("en"));
    expect(result.current.locale).toBe("es");
    expect(localStorage.getItem(i18n.storageKey)).toBeNull();
    download.resolve(EN);
    await i18n.flush();
    expect(result.current.locale).toBe("en");
    expect(result.current.t(KEY)).toBe(EN[KEY]);
    expect(i18n.dictionaries.en[KEY]).toBe(EN[KEY]);
    expect(localStorage.getItem(i18n.storageKey)).toBe("en");
  });

  it("recarga con inglés guardado: precargado antes del render, el primer render ya es inglés", async () => {
    localStorage.setItem("zh.erp.locale", "en");
    const i18n = await freshI18n(() => Promise.resolve(EN));
    await i18n.ensureLocaleLoaded(i18n.safeGetStoredLocale());
    const { result } = i18n.render();
    expect(result.current.locale).toBe("en");
    expect(result.current.t(KEY)).toBe(EN[KEY]);
  });

  it("inglés guardado con descarga fallida: todo se muestra en español, la preferencia se conserva y no hay reintentos automáticos", async () => {
    localStorage.setItem("zh.erp.locale", "en");
    const load = vi.fn(() => Promise.reject(new Error("network")));
    const i18n = await freshI18n(load);
    // Igual que main.tsx: un intento antes del primer render.
    await expect(i18n.ensureLocaleLoaded(i18n.safeGetStoredLocale())).rejects.toThrow();
    const { result } = i18n.render();
    await i18n.flush();
    expect(result.current.locale).toBe("es");
    expect(result.current.t(KEY)).toBe(es[KEY]);
    expect(i18n.getDictionary("en")).toBe(i18n.dictionaries.es);
    // Mensajes fuera de React (preferencia guardada `en`): mismo idioma que la UI, no el fallback en inglés.
    const { formatApiError } = await import("../modules/lib/formatApiError");
    expect(formatApiError(null)).toBe(es[KEY]);
    expect(formatApiError(null, "en")).toBe(es[KEY]);
    // La preferencia es la elección explícita del usuario: un fallo de red no la sobrescribe.
    expect(localStorage.getItem(i18n.storageKey)).toBe("en");
    expect(load).toHaveBeenCalledTimes(1);
    // Elegir español explícitamente sí la persiste.
    i18n.act(() => result.current.setLocale("es"));
    expect(result.current.locale).toBe("es");
    expect(localStorage.getItem(i18n.storageKey)).toBe("es");
  });

  it("inglés guardado con descarga fallida: reelegir inglés y volver a fallar no cambia idioma ni preferencia", async () => {
    localStorage.setItem("zh.erp.locale", "en");
    const i18n = await freshI18n(() => Promise.reject(new Error("network")));
    await expect(i18n.ensureLocaleLoaded("en")).rejects.toThrow();
    const { result } = i18n.render();
    i18n.act(() => result.current.setLocale("en"));
    await i18n.flush();
    expect(result.current.locale).toBe("es");
    expect(result.current.t(KEY)).toBe(es[KEY]);
    expect(localStorage.getItem(i18n.storageKey)).toBe("en");
  });

  it("fallo al cambiar a inglés: conserva español y no guarda la preferencia", async () => {
    const i18n = await freshI18n(() => Promise.reject(new Error("network")));
    const { result } = i18n.render();
    i18n.act(() => result.current.setLocale("en"));
    await i18n.flush();
    expect(result.current.locale).toBe("es");
    expect(result.current.t(KEY)).toBe(es[KEY]);
    expect(localStorage.getItem(i18n.storageKey)).toBeNull();
    expect(i18n.isLocaleLoaded("en")).toBe(false);
  });

  it("cambios rápidos: una descarga lenta no pisa una selección posterior", async () => {
    const download = deferred<Record<string, string>>();
    const i18n = await freshI18n(() => download.promise);
    const { result } = i18n.render();
    i18n.act(() => result.current.setLocale("en"));
    i18n.act(() => result.current.setLocale("es"));
    expect(result.current.locale).toBe("es");
    expect(localStorage.getItem(i18n.storageKey)).toBe("es");
    download.resolve(EN);
    await i18n.flush();
    expect(result.current.locale).toBe("es");
    expect(localStorage.getItem(i18n.storageKey)).toBe("es");
  });

  it("cambios rápidos en→es→en: aplica el último y descarga una sola vez", async () => {
    const download = deferred<Record<string, string>>();
    const load = vi.fn(() => download.promise);
    const i18n = await freshI18n(load);
    const { result } = i18n.render();
    i18n.act(() => result.current.setLocale("en"));
    i18n.act(() => result.current.setLocale("es"));
    i18n.act(() => result.current.setLocale("en"));
    download.resolve(EN);
    await i18n.flush();
    expect(load).toHaveBeenCalledTimes(1);
    expect(result.current.locale).toBe("en");
    expect(localStorage.getItem(i18n.storageKey)).toBe("en");
  });

  it("idioma ya descargado: setLocale es inmediato (síncrono) en ambos sentidos", async () => {
    const i18n = await freshI18n(() => Promise.resolve(EN));
    await i18n.ensureLocaleLoaded("en");
    const { result } = i18n.render();
    i18n.act(() => result.current.setLocale("en"));
    expect(result.current.locale).toBe("en");
    i18n.act(() => result.current.setLocale("es"));
    expect(result.current.locale).toBe("es");
    expect(localStorage.getItem(i18n.storageKey)).toBe("es");
  });
});
