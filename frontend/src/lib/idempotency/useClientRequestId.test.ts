// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { renderHook } from "@testing-library/react";
import { useClientRequestId } from "./useClientRequestId";

describe("useClientRequestId — una intención = un ClientRequestId", () => {
  it("mismo payload (reintento, doble clic, reabrir modal) → mismo id", () => {
    const { result } = renderHook(() => useClientRequestId());
    const payload = { amount: 25, reasonId: "r1" };

    const first = result.current.idFor(payload);
    const retry = result.current.idFor({ amount: 25, reasonId: "r1" });

    expect(retry).toBe(first);
    expect(first).toMatch(/^[0-9a-f-]{36}$/);
  });

  it("payload distinto → nueva intención con otro id", () => {
    const { result } = renderHook(() => useClientRequestId());

    const a = result.current.idFor({ amount: 25 });
    const b = result.current.idFor({ amount: 30 });

    expect(b).not.toBe(a);
  });

  it("tras complete() la misma operación vuelve a ser una intención nueva", () => {
    const { result } = renderHook(() => useClientRequestId());
    const payload = { amount: 25 };

    const first = result.current.idFor(payload);
    result.current.complete();
    const next = result.current.idFor(payload);

    expect(next).not.toBe(first);
  });

  it("el id sobrevive a re-renders del componente", () => {
    const { result, rerender } = renderHook(() => useClientRequestId());
    const first = result.current.idFor({ amount: 25 });

    rerender();

    expect(result.current.idFor({ amount: 25 })).toBe(first);
  });
});
