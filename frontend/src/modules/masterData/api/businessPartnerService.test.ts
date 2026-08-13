import { describe, expect, it } from "vitest";
import { buildSearchQuery } from "./businessPartnerService";
import { RoleTypeEnum } from "../types/businessPartner.types";

describe("businessPartnerService search query", () => {
  it("omite isActive para consultar todos los estados", () => {
    const query = buildSearchQuery({
      q: "arca",
      roles: [RoleTypeEnum.Supplier],
      take: 50,
    });

    expect(query).toContain("q=arca");
    expect(query).toContain("roles=2");
    expect(query).not.toContain("isActive=");
  });

  it("envia isActive=false para consultar proveedores inactivos", () => {
    const query = buildSearchQuery({
      isActive: false,
      roles: [RoleTypeEnum.Supplier],
    });

    expect(query).toContain("isActive=false");
    expect(query).toContain("roles=2");
  });
});
