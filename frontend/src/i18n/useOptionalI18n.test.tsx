// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { renderHook } from "@testing-library/react";
import { useOptionalI18n } from "./i18n";

// SALES-PRICING-UX-TRACEABILITY-07C1 — el fallback sin provider existe solo para tests.
afterEach(() => vi.unstubAllEnvs());

describe("useOptionalI18n", () => {
  it("en tests (MODE=test) cae al idioma por defecto sin provider", () => {
    const { result } = renderHook(() => useOptionalI18n());
    expect(result.current.t("sales.pricing.pvp")).toBe("PVP");
  });

  it("fuera de tests NO enmascara la ausencia del provider: lanza como useI18n", () => {
    vi.stubEnv("MODE", "production");
    expect(() => renderHook(() => useOptionalI18n())).toThrow(/I18nProvider/);
  });
});
