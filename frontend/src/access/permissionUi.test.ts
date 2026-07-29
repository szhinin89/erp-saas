import { describe, expect, it } from "vitest";
import { isAdminRole } from "./permissionUi";

describe("permissionUi", () => {
  it("isAdminRole recognizes Admin role", () => {
    expect(isAdminRole("Admin")).toBe(true);
    expect(isAdminRole("User")).toBe(false);
    expect(isAdminRole(null)).toBe(false);
    expect(isAdminRole(undefined)).toBe(false);
  });
});
