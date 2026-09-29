// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { useActiveBranchStore } from "../../store/activeBranchStore";
import { clearOperationalContext } from "./clearOperationalContext";

describe("clearOperationalContext", () => {
  it("limpia la sucursal activa (contexto operativo dependiente de empresa)", () => {
    useActiveBranchStore.setState({
      branch: { id: "branch-A", name: "Sucursal A", isMainBranch: true },
    });

    clearOperationalContext();

    expect(useActiveBranchStore.getState().branch).toBeNull();
  });
});
