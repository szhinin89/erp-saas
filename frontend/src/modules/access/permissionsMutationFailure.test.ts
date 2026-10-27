import { AxiosError, AxiosHeaders } from "axios";
import { describe, expect, it, vi } from "vitest";
import {
  PERMISSIONS_INVALIDATION_INCOMPLETE,
  PERMISSIONS_INVALIDATION_UNAVAILABLE,
  presentPermissionsMutationFailure,
} from "./permissionsMutationFailure";

function apiError(status: number, code: string): AxiosError {
  const headers = new AxiosHeaders();
  return new AxiosError("Request failed", "ERR_BAD_RESPONSE", { headers }, null, {
    status,
    statusText: "",
    headers: {},
    config: { headers },
    data: { code, message: { user: "mensaje del catálogo" } },
  });
}

const t = (key: string) => key;

describe("presentPermissionsMutationFailure (SEC-06)", () => {
  it("UNAVAILABLE: error 'no se guardó', sin recarga ni reintento", async () => {
    const show = vi.fn();
    const reload = vi.fn();

    const handled = await presentPermissionsMutationFailure(
      apiError(503, PERMISSIONS_INVALIDATION_UNAVAILABLE),
      { t, show, reload },
    );

    expect(handled).toBe(true);
    expect(show).toHaveBeenCalledExactlyOnceWith(
      "access.permissionsInvalidation.unavailable",
      "error",
    );
    expect(reload).not.toHaveBeenCalled();
  });

  it("INCOMPLETE: recarga el estado real una vez y luego deja la advertencia", async () => {
    const steps: string[] = [];
    const show = vi.fn((_text: string, severity: string) => steps.push(`show:${severity}`));
    const reload = vi.fn(async () => {
      steps.push("reload");
    });

    const handled = await presentPermissionsMutationFailure(
      apiError(503, PERMISSIONS_INVALIDATION_INCOMPLETE),
      { t, show, reload },
    );

    expect(handled).toBe(true);
    expect(reload).toHaveBeenCalledTimes(1);
    expect(steps).toEqual(["reload", "show:warning"]);
    expect(show).toHaveBeenCalledWith("access.permissionsInvalidation.incomplete", "warning");
  });

  it("INCOMPLETE: si la recarga falla, la advertencia se mantiene", async () => {
    const show = vi.fn();
    const reload = vi.fn().mockRejectedValue(new Error("offline"));

    await presentPermissionsMutationFailure(
      apiError(503, PERMISSIONS_INVALIDATION_INCOMPLETE),
      { t, show, reload },
    );

    expect(show).toHaveBeenCalledExactlyOnceWith(
      "access.permissionsInvalidation.incomplete",
      "warning",
    );
  });

  it("otro 503 u otro error: no lo trata y deja el manejo habitual de la pantalla", async () => {
    const show = vi.fn();

    expect(
      await presentPermissionsMutationFailure(apiError(503, "DATABASE_UNAVAILABLE"), { t, show }),
    ).toBe(false);
    expect(await presentPermissionsMutationFailure(new Error("x"), { t, show })).toBe(false);
    expect(show).not.toHaveBeenCalled();
  });
});
