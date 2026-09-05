// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  useSessionIdleStore,
  SESSION_IDLE_STORAGE_KEY,
} from "./sessionIdleStore";

beforeEach(() => {
  useSessionIdleStore.setState({ isLocked: false, lastActivityAt: Date.now() });
});

afterEach(() => {
  useSessionIdleStore.setState({ isLocked: false, lastActivityAt: Date.now() });
  sessionStorage.removeItem(SESSION_IDLE_STORAGE_KEY);
});

describe("sessionIdleStore", () => {
  it("lock() persiste isLocked en sessionStorage para sobrevivir un F5", () => {
    useSessionIdleStore.getState().lock();

    const raw = sessionStorage.getItem(SESSION_IDLE_STORAGE_KEY);
    expect(raw).toBeTruthy();
    expect(JSON.parse(raw!).state.isLocked).toBe(true);
  });

  it("unlock() limpia isLocked y refresca lastActivityAt", () => {
    useSessionIdleStore.getState().lock();
    const beforeUnlock = useSessionIdleStore.getState().lastActivityAt;

    vi.useFakeTimers();
    vi.advanceTimersByTime(10_000);
    useSessionIdleStore.getState().unlock();
    vi.useRealTimers();

    expect(useSessionIdleStore.getState().isLocked).toBe(false);
    expect(useSessionIdleStore.getState().lastActivityAt).toBeGreaterThan(
      beforeUnlock,
    );
  });

  it("recordActivity() no actualiza lastActivityAt si ya está bloqueado (el candado no se levanta solo)", () => {
    useSessionIdleStore.getState().lock();
    const locked = useSessionIdleStore.getState().lastActivityAt;

    useSessionIdleStore.getState().recordActivity();

    expect(useSessionIdleStore.getState().lastActivityAt).toBe(locked);
    expect(useSessionIdleStore.getState().isLocked).toBe(true);
  });

  it("recordActivity() throttlea escrituras muy seguidas", () => {
    const first = useSessionIdleStore.getState().lastActivityAt;

    useSessionIdleStore.getState().recordActivity();

    // Dentro de la ventana de throttle (5s) — no debería haber cambiado.
    expect(useSessionIdleStore.getState().lastActivityAt).toBe(first);
  });

  it("reset() vuelve al estado limpio (usado por fullLogout en cambio de usuario)", () => {
    useSessionIdleStore.getState().lock();

    useSessionIdleStore.getState().reset();

    expect(useSessionIdleStore.getState().isLocked).toBe(false);
  });
});
