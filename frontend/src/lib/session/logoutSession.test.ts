import { afterEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import { fullLogout } from "./fullLogout";

vi.mock("axios", () => ({
  default: {
    post: vi.fn(),
  },
}));

vi.mock("./fullLogout", () => ({
  fullLogout: vi.fn(),
}));

import { logoutSession } from "./logoutSession";

describe("logoutSession", () => {
  afterEach(() => {
    vi.mocked(axios.post).mockReset();
    vi.mocked(fullLogout).mockReset();
  });

  it("invoca POST /api/v1/auth/logout con cookies (withCredentials) antes de limpiar el estado local", async () => {
    const callOrder: string[] = [];
    vi.mocked(axios.post).mockImplementationOnce(async () => {
      callOrder.push("remote");
      return { data: {} };
    });
    vi.mocked(fullLogout).mockImplementationOnce(() => {
      callOrder.push("local");
    });

    await logoutSession();

    expect(axios.post).toHaveBeenCalledWith(
      expect.stringContaining("/api/v1/auth/logout"),
      {},
      { withCredentials: true },
    );
    expect(fullLogout).toHaveBeenCalledTimes(1);
    expect(callOrder).toEqual(["remote", "local"]);
  });

  it("ejecuta la limpieza local igual si el logout remoto falla por red/401/500", async () => {
    vi.mocked(axios.post).mockRejectedValueOnce({
      response: { status: 401 },
    });

    await expect(logoutSession()).resolves.toBeUndefined();
    expect(fullLogout).toHaveBeenCalledTimes(1);
  });

  it("no llama a la instancia interceptada `api` — usa axios crudo, así el interceptor de 401 no dispara un refresh automático", async () => {
    vi.mocked(axios.post).mockResolvedValueOnce({ data: {} });

    await logoutSession();

    // axios.post (crudo) es el único punto de contacto HTTP: no pasa por
    // modules/lib/api.ts, por lo que el interceptor de refresh nunca se activa.
    expect(axios.post).toHaveBeenCalledTimes(1);
  });
});
