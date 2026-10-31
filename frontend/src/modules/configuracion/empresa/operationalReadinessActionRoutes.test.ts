import { describe, expect, it } from "vitest";
import { getOperationalReadinessActionRoute } from "./operationalReadinessActionRoutes";

describe("getOperationalReadinessActionRoute", () => {
  it("los requisitos de emisión física se corrigen en Secuencias documentales", () => {
    expect(getOperationalReadinessActionRoute("PhysicalDocumentAuthorizations")).toBe(
      "/settings/document-sequences",
    );
  });

  it("sin destino no navega", () => {
    expect(getOperationalReadinessActionRoute(null)).toBeNull();
  });
});
