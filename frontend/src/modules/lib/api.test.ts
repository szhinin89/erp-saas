// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { api } from "./api";
import { useActiveBranchStore } from "../../store/activeBranchStore";
import { refreshSessionToken } from "../../lib/session/refreshSessionToken";
import { fullLogout } from "../../lib/session/fullLogout";
import {
  clearAccessToken,
  setAccessToken,
} from "../../lib/session/authTokenMemory";

vi.mock("../../lib/session/refreshSessionToken", () => ({
  refreshSessionToken: vi.fn(),
}));
vi.mock("../../lib/session/fullLogout", () => ({
  fullLogout: vi.fn(),
}));

/**
 * ZH-AUTH-BRANCH-CONTEXT-EXPENSES-AUDIT-12: el interceptor de respuesta debe limpiar
 * activeBranchStore cuando el backend rechaza específicamente por BRANCH_SCOPE_FORBIDDEN —
 * esto reabre automáticamente el selector de sucursal (useBranchGate + AppLayout ya reaccionan
 * a branch=null) en vez de dejar la pantalla atascada con la sucursal inválida y un error seco
 * sin salida.
 *
 * ERP-CORE-BRANCH-SESSION-PERSISTENCE-01: COMPANY_SCOPE_FORBIDDEN ya NO limpia la sucursal —
 * es el código genérico de cualquier CompanyScopeException (sin empresa seleccionada,
 * membership inválida, tenant inactivo, mismatch de empresa en el body), ninguna causa dice
 * nada sobre si la sucursal sigue siendo válida. Tratarlo como "sucursal inválida" borraba una
 * sucursal recién elegida y correcta ante cualquier 403 de scope de empresa no relacionado —
 * bug real reproducido navegando a Ítems (dos requests solo company-scoped, nunca
 * branch-scoped, disparaban esto).
 *
 * axios no expone un método público para invocar un interceptor aislado, así que se accede
 * al handler registrado vía `interceptors.response` (mismo mecanismo interno que axios usa
 * para ejecutarlos) — es el único punto de entrada real sin montar un servidor HTTP falso.
 */
function getResponseErrorHandler() {
  const handlers = (
    api.interceptors.response as unknown as {
      handlers: Array<{ rejected: (error: unknown) => unknown } | null>;
    }
  ).handlers;
  const handler = handlers.find((h) => h !== null);
  if (!handler) throw new Error("No response interceptor registered on api instance");
  return handler.rejected;
}

function makeError(status: number, data: unknown, url = "/api/v1/expenses/documents") {
  return {
    config: { url, headers: {} },
    response: { status, data },
  };
}

describe("api response interceptor — branch/company scope recovery", () => {
  const activeBranch = {
    id: "branch-1",
    name: "Matriz",
    isMainBranch: true,
  };

  beforeEach(() => {
    useActiveBranchStore.setState({ branch: activeBranch });
  });

  afterEach(() => {
    useActiveBranchStore.setState({ branch: null });
  });

  it("limpia activeBranchStore cuando el backend responde 403 BRANCH_SCOPE_FORBIDDEN", async () => {
    const rejected = getResponseErrorHandler();
    const error = makeError(403, {
      code: "BRANCH_SCOPE_FORBIDDEN",
      severity: "error",
      data: { errors: ["No tiene autorización para operar en esta sucursal."] },
      message: {
        user: "Acceso denegado por contexto de sucursal.",
        dev: "Branch scope exception.",
      },
    });

    await expect(rejected(error)).rejects.toBe(error);
    expect(useActiveBranchStore.getState().branch).toBeNull();
  });

  it("NO limpia activeBranchStore cuando el backend responde 403 COMPANY_SCOPE_FORBIDDEN", async () => {
    const rejected = getResponseErrorHandler();
    const error = makeError(403, { code: "COMPANY_SCOPE_FORBIDDEN" });

    await expect(rejected(error)).rejects.toBe(error);
    expect(useActiveBranchStore.getState().branch).toEqual(activeBranch);
  });

  it("no toca activeBranchStore ante un 403 de permisos sin código de scope", async () => {
    const rejected = getResponseErrorHandler();
    const error = makeError(403, { code: "PERMISSION_DENIED" });

    await expect(rejected(error)).rejects.toBe(error);
    expect(useActiveBranchStore.getState().branch).toEqual(activeBranch);
  });

  it("no toca activeBranchStore ante errores no relacionados (404, 500, red)", async () => {
    const rejected = getResponseErrorHandler();

    await expect(rejected(makeError(404, { code: "NOT_FOUND" }))).rejects.toBeTruthy();
    await expect(rejected(makeError(500, { code: "INTERNAL" }))).rejects.toBeTruthy();
    await expect(rejected({ config: { url: "/x", headers: {} } })).rejects.toBeTruthy();

    expect(useActiveBranchStore.getState().branch).toEqual(activeBranch);
  });
});

/**
 * SALES-SAVE-401-AFTER-IDLE-SESSION-01: POST /api/v1/sales devolvía 401 tras la pantalla abierta
 * un rato porque refreshSessionToken() (llamado acá reaccionando al 401) veía el access token
 * vencido todavía en memoria y lo devolvía tal cual, sin llamar a /auth/refresh — el reintento de
 * abajo repetía el mismo 401 con el mismo token. Eso también explicaba los dos POST /sales por
 * intento en los logs: el original + ese reintento fallido, no un doble submit del formulario
 * (ver EmitButton/useSalesPage.confirmIssue, ya protegidos contra doble clic por
 * canEmit/issuePhase/saving). El fix (authRefreshManager.ts, `force: true`) hace que este
 * refresh reaccionando a un 401 siempre llame a la red — se prueba acá a nivel de interceptor.
 */
describe("api response interceptor — refresh y reintento tras 401", () => {
  const originalLocation = window.location;

  beforeEach(() => {
    setAccessToken("stale-expired-token");
    Object.defineProperty(window, "location", {
      configurable: true,
      value: { ...originalLocation, href: "" },
    });
  });

  afterEach(() => {
    vi.mocked(refreshSessionToken).mockReset();
    vi.mocked(fullLogout).mockReset();
    clearAccessToken();
    Object.defineProperty(window, "location", {
      configurable: true,
      value: originalLocation,
    });
  });

  it("ante un 401 llama a refreshSessionToken con force:true y reintenta una sola vez con el nuevo token", async () => {
    // Emula lo que hace la implementación real (postRefresh, authRefreshManager.ts):
    // actualiza el token en memoria además de resolver el valor — así el interceptor de
    // request (que lee getAccessToken()) también ve el token nuevo en el reintento.
    vi.mocked(refreshSessionToken).mockImplementation(async () => {
      setAccessToken("fresh-token");
      return "fresh-token";
    });
    const originalAdapter = api.defaults.adapter;
    const adapterSpy = vi.fn().mockResolvedValue({
      data: "ok",
      status: 200,
      statusText: "OK",
      headers: {},
      config: {},
    });
    api.defaults.adapter = adapterSpy;

    try {
      const rejected = getResponseErrorHandler();
      const error = makeError(401, { code: "UNAUTHORIZED" }, "/api/v1/sales");

      const result = await rejected(error);

      expect(refreshSessionToken).toHaveBeenCalledTimes(1);
      expect(refreshSessionToken).toHaveBeenCalledWith({ force: true });
      // Un solo reintento de red — no dos POST /sales por el mismo click.
      expect(adapterSpy).toHaveBeenCalledTimes(1);
      const retriedConfig = adapterSpy.mock.calls[0][0] as {
        headers: Record<string, string>;
      };
      expect(retriedConfig.headers.Authorization).toBe("Bearer fresh-token");
      expect((result as { data: string }).data).toBe("ok");
    } finally {
      api.defaults.adapter = originalAdapter;
    }
  });

  it("no reintenta un request que ya fue reintentado (_retry=true) — evita loop y un segundo POST", async () => {
    const rejected = getResponseErrorHandler();
    const error = makeError(401, { code: "UNAUTHORIZED" }, "/api/v1/sales");
    (error.config as { _retry?: boolean })._retry = true;

    await expect(rejected(error)).rejects.toBe(error);
    expect(refreshSessionToken).not.toHaveBeenCalled();
  });

  it("si el refresh falla, no reintenta infinito, limpia la sesión y redirige a login", async () => {
    const refreshError = { response: { status: 401 } };
    vi.mocked(refreshSessionToken).mockRejectedValue(refreshError);
    const originalAdapter = api.defaults.adapter;
    const adapterSpy = vi.fn();
    api.defaults.adapter = adapterSpy;

    try {
      const rejected = getResponseErrorHandler();
      const error = makeError(401, { code: "UNAUTHORIZED" }, "/api/v1/sales");

      await expect(rejected(error)).rejects.toBe(refreshError);
      expect(adapterSpy).not.toHaveBeenCalled();
      expect(fullLogout).toHaveBeenCalledTimes(1);
      expect(window.location.href).toBe("/login");
    } finally {
      api.defaults.adapter = originalAdapter;
    }
  });

  it("si el refresh falla con 429 (rate limit), no cierra la sesión — deja reintentar más tarde", async () => {
    const refreshError = { response: { status: 429 } };
    vi.mocked(refreshSessionToken).mockRejectedValue(refreshError);

    const rejected = getResponseErrorHandler();
    const error = makeError(401, { code: "UNAUTHORIZED" }, "/api/v1/sales");

    await expect(rejected(error)).rejects.toBe(refreshError);
    expect(fullLogout).not.toHaveBeenCalled();
    expect(window.location.href).toBe("");
  });
});
