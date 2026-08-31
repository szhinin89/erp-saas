// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { useAuthStore } from "../../store/authStore";
import { clearAccessToken, getAccessToken } from "./authTokenMemory";
import { refreshSessionToken } from "./refreshSessionToken";

vi.mock("./refreshSessionToken", () => ({
  refreshSessionToken: vi.fn(),
}));

import { restoreSessionFromCookie } from "./restoreSessionFromCookie";

describe("restoreSessionFromCookie", () => {
  afterEach(() => {
    vi.mocked(refreshSessionToken).mockReset();
    clearAccessToken();
    useAuthStore.getState().logout();
  });

  it("no reconstruye la sesión si el refresh falla porque el token ya fue revocado por logout", async () => {
    vi.mocked(refreshSessionToken).mockRejectedValueOnce({
      response: { status: 401, data: { message: "Refresh token revocado." } },
    });

    const restored = await restoreSessionFromCookie();

    expect(restored).toBe(false);
    expect(getAccessToken()).toBeNull();
    expect(useAuthStore.getState().isAuthenticated).toBe(false);
  });

  it("reconstruye la sesión cuando el refresh todavía es válido", async () => {
    vi.mocked(refreshSessionToken).mockResolvedValueOnce("new-access-token");

    const restored = await restoreSessionFromCookie();

    expect(restored).toBe(true);
    expect(refreshSessionToken).toHaveBeenCalledWith({
      bootstrapRetry: true,
    });
  });
});
