// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useSessionStore } from "./sessionStore";
import { useActiveBranchStore } from "./activeBranchStore";
import { sessionService } from "../modules/session/api/sessionService";
import type { SessionContextDto } from "../types/session";

vi.mock("../modules/session/api/sessionService", () => ({
  sessionService: {
    getContext: vi.fn(),
  },
}));

const baseDto: SessionContextDto = {
  identity: { userId: "user-1", fullName: "Test User", email: "t@t.com" },
  tenant: { id: "tenant-1", displayName: "Tenant", logo: null },
  authorization: { roles: ["Cajero"], permissions: ["*"] },
  preferences: { language: "es" },
  branch: { id: "branch-1", name: "Matriz", isMainBranch: true },
};

beforeEach(() => {
  vi.clearAllMocks();
  useSessionStore.setState({
    identity: null,
    tenant: null,
    authorization: null,
    preferences: null,
    isLoaded: false,
    isLoading: false,
  });
  useActiveBranchStore.setState({ branch: null });
});

afterEach(() => {
  useSessionStore.setState({
    identity: null,
    tenant: null,
    authorization: null,
    preferences: null,
    isLoaded: false,
    isLoading: false,
  });
  useActiveBranchStore.setState({ branch: null });
});

describe("sessionStore.refresh", () => {
  it("marca isLoading=true durante el fetch y lo apaga al resolver, sincronizando activeBranchStore", async () => {
    let resolveFetch: (dto: SessionContextDto) => void;
    vi.mocked(sessionService.getContext).mockReturnValue(
      new Promise((resolve) => {
        resolveFetch = resolve;
      }),
    );

    const promise = useSessionStore.getState().refresh();
    expect(useSessionStore.getState().isLoading).toBe(true);
    expect(useSessionStore.getState().isLoaded).toBe(false);

    resolveFetch!(baseDto);
    await promise;

    expect(useSessionStore.getState().isLoading).toBe(false);
    expect(useSessionStore.getState().isLoaded).toBe(true);
    expect(useActiveBranchStore.getState().branch?.id).toBe("branch-1");
  });

  it("apaga isLoading si el fetch falla, sin dejar el gate de sucursal bloqueado indefinidamente", async () => {
    vi.mocked(sessionService.getContext).mockRejectedValue(
      new Error("network error"),
    );

    await expect(useSessionStore.getState().refresh()).rejects.toThrow();

    expect(useSessionStore.getState().isLoading).toBe(false);
    expect(useSessionStore.getState().isLoaded).toBe(false);
  });
});
