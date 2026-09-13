import { afterEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import {
  __testHandleAuthBroadcast,
  __testResetAuthRefreshManager,
  refreshSessionToken,
} from "./authRefreshManager";
import {
  clearAccessToken,
  getAccessToken,
  setAccessToken,
} from "./authTokenMemory";

vi.mock("axios", () => ({
  default: {
    post: vi.fn(),
    create: vi.fn(() => ({
      post: vi.fn(),
      get: vi.fn(),
      interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
    })),
  },
}));

vi.mock("../../store/authStore", () => ({
  useAuthStore: {
    getState: () => ({ updateTokens: vi.fn(), login: vi.fn() }),
  },
}));

describe("authRefreshManager", () => {
  afterEach(() => {
    __testResetAuthRefreshManager();
    clearAccessToken();
    vi.mocked(axios.post).mockReset();
  });

  it("deduplica llamadas concurrentes en una sola petición HTTP", async () => {
    vi.mocked(axios.post).mockImplementation(
      () =>
        new Promise((resolve) => {
          setTimeout(
            () =>
              resolve({
                data: { data: { token: "access-1" } },
              }),
            30,
          );
        }),
    );

    const [a, b] = await Promise.all([
      refreshSessionToken(),
      refreshSessionToken(),
    ]);

    expect(a).toBe("access-1");
    expect(b).toBe("access-1");
    expect(axios.post).toHaveBeenCalledTimes(1);
  });

  it("aplica access token recibido por BroadcastChannel de otra pestaña", async () => {
    __testHandleAuthBroadcast({
      type: "refresh_success",
      tabId: "other-tab",
      accessToken: "remote-token",
    });

    expect(getAccessToken()).toBe("remote-token");
  });

  it("reintenta una vez en bootstrapRetry tras 401 ya utilizado", async () => {
    vi.mocked(axios.post)
      .mockRejectedValueOnce({
        response: {
          status: 401,
          data: { message: "Refresh token ya utilizado." },
        },
      })
      .mockResolvedValueOnce({
        data: { data: { token: "after-retry" } },
      });

    const token = await refreshSessionToken({ bootstrapRetry: true });
    expect(token).toBe("after-retry");
    expect(axios.post).toHaveBeenCalledTimes(2);
  });

  it("devuelve token en memoria sin POST si ya existe", async () => {
    setAccessToken("cached");
    const token = await refreshSessionToken();
    expect(token).toBe("cached");
    expect(axios.post).not.toHaveBeenCalled();
  });

  // SALES-SAVE-401-AFTER-IDLE-SESSION-01: el interceptor 401 de api.ts llama a
  // refreshSessionToken() con el mismo token (vencido) que el backend acaba de rechazar
  // todavía en memoria — sin `force`, el chequeo de arriba lo devolvía tal cual sin tocar la
  // red, y el reintento del request original fallaba otra vez con el mismo 401.
  it("con force:true llama a /auth/refresh aunque ya haya un token en memoria", async () => {
    setAccessToken("stale-expired-token");
    vi.mocked(axios.post).mockResolvedValue({
      data: { data: { token: "fresh-token" } },
    });

    const token = await refreshSessionToken({ force: true });

    expect(token).toBe("fresh-token");
    expect(axios.post).toHaveBeenCalledTimes(1);
    expect(getAccessToken()).toBe("fresh-token");
  });

  it("con force:true, llamadas concurrentes siguen deduplicándose en una sola petición", async () => {
    setAccessToken("stale-expired-token");
    vi.mocked(axios.post).mockImplementation(
      () =>
        new Promise((resolve) => {
          setTimeout(
            () => resolve({ data: { data: { token: "fresh-token" } } }),
            30,
          );
        }),
    );

    const [a, b] = await Promise.all([
      refreshSessionToken({ force: true }),
      refreshSessionToken({ force: true }),
    ]);

    expect(a).toBe("fresh-token");
    expect(b).toBe("fresh-token");
    expect(axios.post).toHaveBeenCalledTimes(1);
  });
});
