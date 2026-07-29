import { beforeEach, describe, expect, it, vi } from "vitest";

function createStorage(): Storage {
  const map = new Map<string, string>();
  return {
    get length() {
      return map.size;
    },
    clear: () => map.clear(),
    getItem: (key: string) => (map.has(key) ? map.get(key)! : null),
    key: (index: number) => Array.from(map.keys())[index] ?? null,
    removeItem: (key: string) => map.delete(key),
    setItem: (key: string, value: string) => map.set(key, value),
  };
}

vi.stubGlobal("localStorage", createStorage());
vi.stubGlobal("sessionStorage", createStorage());

import { useAccessStore } from "../../store/accessStore";
import { useAuthStore } from "../../store/authStore";
import { setAccessToken } from "./authTokenMemory";
import {
  ACCESS_BOOTSTRAP_STORAGE_KEY,
  AUTH_PROFILE_STORAGE_KEY,
  AUTH_STORAGE_KEY,
  PERMISSIONS_STORAGE_KEY,
} from "./sessionStorageKeys";
import { clearPersistedSessionArtifacts, fullLogout } from "./fullLogout";

vi.mock("../../modules/lib/api", () => ({
  resetAuthTransportState: vi.fn(),
}));

describe("fullLogout", () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    useAuthStore.getState().logout();
    useAccessStore.getState().clearBootstrap();
  });

  it("clearPersistedSessionArtifacts elimina claves sensibles y erp.saas.*", () => {
    sessionStorage.setItem(AUTH_PROFILE_STORAGE_KEY, "{}");
    sessionStorage.setItem(PERMISSIONS_STORAGE_KEY, "{}");
    sessionStorage.setItem(ACCESS_BOOTSTRAP_STORAGE_KEY, "{}");
    localStorage.setItem(AUTH_STORAGE_KEY, "{}");
    sessionStorage.setItem("erp.saas.platform.detailSubscriberId", "uuid");
    sessionStorage.setItem("erp.saas.other", "x");
    sessionStorage.setItem("unrelated", "keep");

    clearPersistedSessionArtifacts();

    expect(sessionStorage.getItem(AUTH_PROFILE_STORAGE_KEY)).toBeNull();
    expect(sessionStorage.getItem(PERMISSIONS_STORAGE_KEY)).toBeNull();
    expect(sessionStorage.getItem(ACCESS_BOOTSTRAP_STORAGE_KEY)).toBeNull();
    expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(
      sessionStorage.getItem("erp.saas.platform.detailSubscriberId"),
    ).toBeNull();
    expect(sessionStorage.getItem("erp.saas.other")).toBeNull();
    expect(sessionStorage.getItem("unrelated")).toBe("keep");
  });

  it("fullLogout limpia stores y persistencia", () => {
    setAccessToken("access");
    useAuthStore.setState({
      user: {
        userId: "u1",
        username: "testuser",
        email: "a@b.com",
        fullName: "Test",
        role: "Admin",
        tenantId: "s1",
        companyId: "c1",
      },
      token: "access",
      isAuthenticated: true,
    });
    useAccessStore.getState().setBootstrap({
      bootstrapToken: "boot",
      userId: "u1",
      fullName: "Test",
      email: "a@b.com",
      tenants: [],
    });
    sessionStorage.setItem(AUTH_PROFILE_STORAGE_KEY, '{"state":{"token":"x"}}');

    fullLogout();

    expect(useAuthStore.getState().isAuthenticated).toBe(false);
    expect(useAuthStore.getState().token).toBeNull();
    expect(useAccessStore.getState().bootstrapToken).toBeNull();
    expect(sessionStorage.getItem(AUTH_PROFILE_STORAGE_KEY)).toBeNull();
  });
});
