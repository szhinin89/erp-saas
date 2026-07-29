import { describe, expect, it } from "vitest";
import {
  isPublicAuthPath,
  shouldAttemptTokenRefresh,
} from "./authRefreshPolicy";

describe("authRefreshPolicy", () => {
  it("marca /api/v1/auth/refresh como ruta pública (sin reintento recursivo)", () => {
    expect(isPublicAuthPath("/api/v1/auth/refresh")).toBe(true);
    expect(shouldAttemptTokenRefresh(401, "/api/v1/auth/refresh", false)).toBe(
      false,
    );
  });

  it("permite refresh ante 401 en endpoints protegidos", () => {
    expect(shouldAttemptTokenRefresh(401, "/api/v1/items", false)).toBe(true);
  });

  it("no reintenta tras _retry (evita loop infinito)", () => {
    expect(shouldAttemptTokenRefresh(401, "/api/v1/items", true)).toBe(false);
  });

  it("ignora status distintos de 401", () => {
    expect(shouldAttemptTokenRefresh(400, "/api/v1/items", false)).toBe(false);
    expect(shouldAttemptTokenRefresh(403, "/api/v1/items", false)).toBe(false);
  });

  it("no trata login como candidato a refresh", () => {
    expect(shouldAttemptTokenRefresh(401, "/api/v1/auth/login", false)).toBe(
      false,
    );
  });
});
