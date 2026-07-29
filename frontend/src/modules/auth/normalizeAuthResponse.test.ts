import { describe, expect, it } from "vitest";
import { normalizeAuthResponse } from "./normalizeAuthResponse";

describe("normalizeAuthResponse", () => {
  it("maps PascalCase API fields to AuthResponse", () => {
    const session = normalizeAuthResponse({
      UserId: "u1",
      FullName: "Admin",
      Email: "a@x.com",
      Role: "Admin",
      TenantId: "t1",
      Token: "jwt-here",
    });

    expect(session.userId).toBe("u1");
    expect(session.token).toBe("jwt-here");
    expect(session.tenantId).toBe("t1");
  });
});
