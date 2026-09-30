import { beforeEach, describe, expect, it, vi } from "vitest";

const apiGetMock = vi.fn();
const apiPostMock = vi.fn();

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: (...args: unknown[]) => apiGetMock(...args),
  apiPost: (...args: unknown[]) => apiPostMock(...args),
}));

import { authService } from "./authService";
import { setupService } from "./setupService";

/**
 * ZH-FRONTEND-HTTP-CLIENT-SSOT-01 — las páginas públicas de auth (olvidé/restablecer contraseña,
 * configuración inicial) ya no hacen HTTP inline: mismo endpoint y mismo body por su service.
 */
describe("auth — flujos públicos por su service", () => {
  beforeEach(() => {
    apiGetMock.mockReset();
    apiPostMock.mockReset();
  });

  it("forgotPassword → POST /api/v1/auth/forgot-password { email }", async () => {
    apiPostMock.mockResolvedValue(undefined);
    await authService.forgotPassword("user@example.com");
    expect(apiPostMock).toHaveBeenCalledWith("/api/v1/auth/forgot-password", { email: "user@example.com" });
  });

  it("resetPassword → POST /api/v1/auth/reset-password con token, clave y tenant opcional", async () => {
    apiPostMock.mockResolvedValue(undefined);
    await authService.resetPassword({ token: "t", newPassword: "Secreta#1" });
    await authService.resetPassword({ token: "t", newPassword: "Secreta#1", tenantId: "ten" });
    expect(apiPostMock.mock.calls).toEqual([
      ["/api/v1/auth/reset-password", { token: "t", newPassword: "Secreta#1" }],
      ["/api/v1/auth/reset-password", { token: "t", newPassword: "Secreta#1", tenantId: "ten" }],
    ]);
  });

  it("setupService: GET /api/v1/setup/status y POST /api/v1/setup/admin", async () => {
    apiGetMock.mockResolvedValue({ isInitialized: false });
    apiPostMock.mockResolvedValue(undefined);
    await expect(setupService.getStatus()).resolves.toEqual({ isInitialized: false });
    const payload = {
      username: "admin",
      firstName: "A",
      lastName: "B",
      email: null,
      password: "Secreta#1",
      setupToken: "tok",
    };
    await setupService.createInitialAdmin(payload);
    expect(apiGetMock).toHaveBeenCalledWith("/api/v1/setup/status");
    expect(apiPostMock).toHaveBeenCalledWith("/api/v1/setup/admin", payload);
  });

  it("los errores HTTP se propagan sin transformar (la página decide el mensaje)", async () => {
    const error = Object.assign(new Error("400"), { isAxiosError: true });
    apiPostMock.mockRejectedValue(error);
    await expect(authService.forgotPassword("x@y.z")).rejects.toBe(error);
    await expect(setupService.createInitialAdmin({
      username: "a", firstName: "a", lastName: "a", email: null, password: "p", setupToken: "t",
    })).rejects.toBe(error);
  });
});
