// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { api } from "./api";
import { useActiveBranchStore } from "../../store/activeBranchStore";

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
