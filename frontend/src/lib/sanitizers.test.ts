import { describe, expect, it } from "vitest";
import { normalizeOptionalCode } from "./sanitizers";

describe("normalizeOptionalCode", () => {
  it("convierte null a null", () => {
    expect(normalizeOptionalCode(null)).toBeNull();
  });

  it("convierte undefined a null", () => {
    expect(normalizeOptionalCode(undefined)).toBeNull();
  });

  it('convierte "" a null', () => {
    expect(normalizeOptionalCode("")).toBeNull();
  });

  it('convierte "   " (solo espacios) a null', () => {
    expect(normalizeOptionalCode("   ")).toBeNull();
  });

  it("recorta espacios de un código con contenido real", () => {
    expect(normalizeOptionalCode(" ICE01 ")).toBe("ICE01");
  });

  it("deja intacto un código sin espacios", () => {
    expect(normalizeOptionalCode("ICE01")).toBe("ICE01");
  });
});
