import { afterEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import { clearAccessToken, getAccessToken } from "./authTokenMemory";

vi.mock("axios", () => ({
  default: {
    post: vi.fn(),
    isAxiosError: (err: unknown): boolean =>
      !!err && typeof err === "object" && "response" in err,
    create: vi.fn(() => ({
      post: vi.fn(),
      get: vi.fn(),
      interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
    })),
  },
}));

const loginMock = vi.fn();
vi.mock("../../store/authStore", () => ({
  useAuthStore: {
    getState: () => ({ login: loginMock, updateTokens: vi.fn() }),
  },
}));

import { reauthenticate, isSessionInvalidError } from "./reauthenticate";

describe("reauthenticate", () => {
  afterEach(() => {
    vi.mocked(axios.post).mockReset();
    loginMock.mockReset();
    clearAccessToken();
  });

  it("con contraseña correcta: guarda el nuevo access token en memoria y actualiza authStore", async () => {
    vi.mocked(axios.post).mockResolvedValueOnce({
      data: {
        data: {
          userId: "u1",
          fullName: "Ana",
          username: "ana",
          role: "Admin",
          tenantId: "t1",
          companyId: "c1",
          token: "new-access-token",
        },
      },
    });

    await reauthenticate("Sup3rSecret!");

    expect(axios.post).toHaveBeenCalledWith(
      expect.stringContaining("/api/v1/auth/reauthenticate"),
      { password: "Sup3rSecret!" },
      { withCredentials: true },
    );
    expect(getAccessToken()).toBe("new-access-token");
    expect(loginMock).toHaveBeenCalledTimes(1);
  });

  it("con contraseña incorrecta: rechaza con el mensaje del backend y no toca el token en memoria", async () => {
    vi.mocked(axios.post).mockRejectedValueOnce({
      response: {
        status: 401,
        data: { data: { errors: ["Contraseña incorrecta."] } },
      },
    });

    await expect(reauthenticate("mala-clave")).rejects.toThrow(
      "Contraseña incorrecta.",
    );
    expect(getAccessToken()).toBeNull();
    expect(loginMock).not.toHaveBeenCalled();
  });

  it("con sesión inválida (refresh vencido): rechaza con el mensaje del backend", async () => {
    vi.mocked(axios.post).mockRejectedValueOnce({
      response: {
        status: 401,
        data: {
          data: { errors: ["Sesión expirada. Inicia sesión nuevamente."] },
        },
      },
    });

    await expect(reauthenticate("algo")).rejects.toThrow(
      "Sesión expirada. Inicia sesión nuevamente.",
    );
  });
});

describe("isSessionInvalidError", () => {
  it("reconoce mensajes de sesión no reautenticable", () => {
    expect(isSessionInvalidError("Sesión expirada. Inicia sesión nuevamente.")).toBe(
      true,
    );
    expect(isSessionInvalidError("Tenant no encontrado o inactivo.")).toBe(true);
    expect(isSessionInvalidError("Membresía no activa para la empresa.")).toBe(
      true,
    );
  });

  it("no confunde una contraseña incorrecta con sesión inválida", () => {
    expect(isSessionInvalidError("Contraseña incorrecta.")).toBe(false);
  });
});
