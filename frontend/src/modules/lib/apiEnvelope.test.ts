import { describe, expect, it } from "vitest";
import { readEnvelopePayload } from "./apiEnvelope";

describe("readEnvelopePayload", () => {
  it("extrae data en camelCase", () => {
    expect(readEnvelopePayload<{ id: string }>({ data: { id: "1" } })).toEqual({
      id: "1",
    });
  });

  it("extrae Data en PascalCase", () => {
    expect(readEnvelopePayload<number>({ Data: 42 })).toBe(42);
  });

  it("devuelve el body si no hay envelope", () => {
    expect(readEnvelopePayload<string>("plain")).toBe("plain");
  });
});
