// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { renderHook, cleanup } from "@testing-library/react";
import { useIdleTimeout } from "./useIdleTimeout";
import { useSessionIdleStore } from "../../store/sessionIdleStore";

const TIMEOUT_MS = 30 * 60_000;

describe("useIdleTimeout", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    // lastActivityAt también se resetea "ahora" (bajo el reloj falso ya instalado): si no,
    // un test anterior que avanzó el reloj falso deja lastActivityAt en el futuro respecto
    // al "ahora" real con el que arranca el siguiente test, rompiendo el cálculo de elapsed.
    useSessionIdleStore.setState({ isLocked: false, lastActivityAt: Date.now() });
  });

  afterEach(() => {
    // Cada test monta el hook sin desmontarlo explícitamente — cleanup() desmonta
    // cualquier renderHook pendiente para no acumular listeners de un test al siguiente.
    cleanup();
    vi.useRealTimers();
    useSessionIdleStore.setState({ isLocked: false, lastActivityAt: Date.now() });
  });

  it("bloquea la sesión tras el timeout configurado sin actividad", () => {
    renderHook(() => useIdleTimeout(TIMEOUT_MS));

    expect(useSessionIdleStore.getState().isLocked).toBe(false);

    vi.advanceTimersByTime(TIMEOUT_MS);

    expect(useSessionIdleStore.getState().isLocked).toBe(true);
  });

  it("la actividad antes del timeout reinicia el contador y evita el bloqueo", () => {
    renderHook(() => useIdleTimeout(TIMEOUT_MS));

    vi.advanceTimersByTime(TIMEOUT_MS - 1_000);
    window.dispatchEvent(new Event("mousemove"));
    vi.advanceTimersByTime(TIMEOUT_MS - 1_000);

    // Si no hubiera reiniciado, ya habríamos superado TIMEOUT_MS total.
    expect(useSessionIdleStore.getState().isLocked).toBe(false);

    vi.advanceTimersByTime(2_000);
    expect(useSessionIdleStore.getState().isLocked).toBe(true);
  });

  it("detecta distintos tipos de actividad (keydown, scroll, touchstart)", () => {
    renderHook(() => useIdleTimeout(TIMEOUT_MS));

    for (const eventName of ["keydown", "scroll", "touchstart"]) {
      vi.advanceTimersByTime(TIMEOUT_MS - 1_000);
      window.dispatchEvent(new Event(eventName));
    }

    expect(useSessionIdleStore.getState().isLocked).toBe(false);
  });

  it("registra y limpia los listeners de actividad sin duplicarlos al remontar", () => {
    const addSpy = vi.spyOn(window, "addEventListener");
    const removeSpy = vi.spyOn(window, "removeEventListener");

    const { unmount: unmount1 } = renderHook(() => useIdleTimeout(TIMEOUT_MS));
    const addCallsAfterFirstMount = addSpy.mock.calls.length;
    expect(addCallsAfterFirstMount).toBeGreaterThan(0);

    unmount1();
    expect(removeSpy.mock.calls).toHaveLength(addCallsAfterFirstMount);

    const { unmount: unmount2 } = renderHook(() => useIdleTimeout(TIMEOUT_MS));
    // El segundo montaje agrega exactamente los mismos listeners que el primero,
    // no una acumulación (2x, 3x...).
    expect(addSpy.mock.calls).toHaveLength(addCallsAfterFirstMount * 2);

    unmount2();
    expect(removeSpy.mock.calls).toHaveLength(addCallsAfterFirstMount * 2);

    addSpy.mockRestore();
    removeSpy.mockRestore();
  });

  it("multi-pestaña: un bloqueo detectado en otra pestaña (evento storage) también bloquea esta", () => {
    renderHook(() => useIdleTimeout(TIMEOUT_MS));

    expect(useSessionIdleStore.getState().isLocked).toBe(false);

    window.dispatchEvent(
      new StorageEvent("storage", {
        key: "erp.idle.locked",
        newValue: "123-0.456",
      }),
    );

    expect(useSessionIdleStore.getState().isLocked).toBe(true);
  });

  it("multi-pestaña: una reautenticación en otra pestaña (evento storage unlocked) desbloquea esta también", () => {
    renderHook(() => useIdleTimeout(TIMEOUT_MS));
    useSessionIdleStore.setState({ isLocked: true });

    window.dispatchEvent(
      new StorageEvent("storage", {
        key: "erp.idle.unlocked",
        newValue: "123-0.456",
      }),
    );

    expect(useSessionIdleStore.getState().isLocked).toBe(false);
  });

  it("una vez bloqueado, deja de escuchar actividad (el candado no se levanta solo)", () => {
    renderHook(() => useIdleTimeout(TIMEOUT_MS));

    vi.advanceTimersByTime(TIMEOUT_MS);
    expect(useSessionIdleStore.getState().isLocked).toBe(true);

    window.dispatchEvent(new Event("mousemove"));
    window.dispatchEvent(new Event("keydown"));

    expect(useSessionIdleStore.getState().isLocked).toBe(true);
  });

  it("F5 (remount) durante una sesión ya inactiva más allá del timeout bloquea de inmediato", () => {
    // Simula lo que persistió sessionStorage antes de recargar: última actividad hace más
    // de TIMEOUT_MS. Sin retomar este valor, el hook reiniciaría el reloj y regalaría
    // TIMEOUT_MS adicionales solo por haber recargado la página (ZH-AUTH-SESSION-PERSISTENCE-QA-11).
    useSessionIdleStore.setState({
      isLocked: false,
      lastActivityAt: Date.now() - TIMEOUT_MS - 5_000,
    });

    renderHook(() => useIdleTimeout(TIMEOUT_MS));

    expect(useSessionIdleStore.getState().isLocked).toBe(true);
  });

  it("F5 dentro del timeout retoma el conteo restante en vez de reiniciarlo completo", () => {
    // Quedan ~5s de margen antes del timeout al momento de recargar.
    useSessionIdleStore.setState({
      isLocked: false,
      lastActivityAt: Date.now() - (TIMEOUT_MS - 5_000),
    });

    renderHook(() => useIdleTimeout(TIMEOUT_MS));

    vi.advanceTimersByTime(4_000);
    expect(useSessionIdleStore.getState().isLocked).toBe(false);

    vi.advanceTimersByTime(1_500);
    expect(useSessionIdleStore.getState().isLocked).toBe(true);
  });

  it("la actividad persiste lastActivityAt para que un F5 posterior pueda retomar el conteo", () => {
    renderHook(() => useIdleTimeout(TIMEOUT_MS));

    const before = useSessionIdleStore.getState().lastActivityAt;
    vi.advanceTimersByTime(10_000);
    window.dispatchEvent(new Event("mousemove"));

    expect(useSessionIdleStore.getState().lastActivityAt).toBeGreaterThan(before);
  });
});
