// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  broadcastIdleLock,
  listenForRemoteIdleLock,
  broadcastIdleUnlock,
  listenForRemoteIdleUnlock,
} from "./idleBroadcast";

const IDLE_LOCK_STORAGE_KEY = "erp.idle.locked";
const IDLE_UNLOCK_STORAGE_KEY = "erp.idle.unlocked";

describe("idleBroadcast", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it("broadcastIdleLock guarda solo un marcador no sensible en localStorage", () => {
    broadcastIdleLock();

    const stored = localStorage.getItem(IDLE_LOCK_STORAGE_KEY);
    expect(stored).not.toBeNull();
    // Formato "<timestamp>-<random>": ni token, ni email, ni datos de usuario.
    expect(stored).toMatch(/^\d+-0?\.\d+$/);
    expect(localStorage.length).toBe(1);
  });

  it("listenForRemoteIdleLock invoca el callback cuando otra pestaña marca el storage", () => {
    const onLocked = vi.fn();
    const cleanup = listenForRemoteIdleLock(onLocked);

    localStorage.setItem(IDLE_LOCK_STORAGE_KEY, "123-0.456");
    window.dispatchEvent(
      new StorageEvent("storage", {
        key: IDLE_LOCK_STORAGE_KEY,
        newValue: "123-0.456",
      }),
    );

    expect(onLocked).toHaveBeenCalledTimes(1);
    cleanup();
  });

  it("ignora eventos de storage de otras claves", () => {
    const onLocked = vi.fn();
    const cleanup = listenForRemoteIdleLock(onLocked);

    window.dispatchEvent(
      new StorageEvent("storage", { key: "unrelated-key", newValue: "x" }),
    );

    expect(onLocked).not.toHaveBeenCalled();
    cleanup();
  });

  it("la función de limpieza remueve el listener de storage", () => {
    const onLocked = vi.fn();
    const cleanup = listenForRemoteIdleLock(onLocked);
    cleanup();

    window.dispatchEvent(
      new StorageEvent("storage", {
        key: IDLE_LOCK_STORAGE_KEY,
        newValue: "123-0.456",
      }),
    );

    expect(onLocked).not.toHaveBeenCalled();
  });

  // ── Desbloqueo entre pestañas (Fase 4 — reautenticación) ─────────────────

  it("broadcastIdleUnlock guarda solo un marcador no sensible en localStorage", () => {
    broadcastIdleUnlock();

    const stored = localStorage.getItem(IDLE_UNLOCK_STORAGE_KEY);
    expect(stored).not.toBeNull();
    expect(stored).toMatch(/^\d+-0?\.\d+$/);
  });

  it("listenForRemoteIdleUnlock invoca el callback cuando otra pestaña se reautentica", () => {
    const onUnlocked = vi.fn();
    const cleanup = listenForRemoteIdleUnlock(onUnlocked);

    window.dispatchEvent(
      new StorageEvent("storage", {
        key: IDLE_UNLOCK_STORAGE_KEY,
        newValue: "123-0.456",
      }),
    );

    expect(onUnlocked).toHaveBeenCalledTimes(1);
    cleanup();
  });

  it("un evento de lock no dispara el callback de unlock ni viceversa", () => {
    const onLocked = vi.fn();
    const onUnlocked = vi.fn();
    const cleanupLock = listenForRemoteIdleLock(onLocked);
    const cleanupUnlock = listenForRemoteIdleUnlock(onUnlocked);

    window.dispatchEvent(
      new StorageEvent("storage", {
        key: IDLE_LOCK_STORAGE_KEY,
        newValue: "123-0.456",
      }),
    );

    expect(onLocked).toHaveBeenCalledTimes(1);
    expect(onUnlocked).not.toHaveBeenCalled();

    cleanupLock();
    cleanupUnlock();
  });
});
