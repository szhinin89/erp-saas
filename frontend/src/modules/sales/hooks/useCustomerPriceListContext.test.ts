// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { useCustomerPriceListContext } from "./useCustomerPriceListContext";
import { customerPriceListContextService } from "../api/customerPriceListContextService";

vi.mock("../api/customerPriceListContextService", () => ({
  customerPriceListContextService: { get: vi.fn() },
}));
const getMock = vi.mocked(customerPriceListContextService.get);

const ctx = (name: string | null) => ({
  customerPriceListId: name ? "id" : null,
  customerPriceListName: name,
  companyDefaultPriceListId: null,
  companyDefaultPriceListName: "Lista General",
});

describe("SALES-PRICING-UX-TRACEABILITY-07C — useCustomerPriceListContext", () => {
  beforeEach(() => getMock.mockReset());

  it("consulta al tener cliente y expone el contexto", async () => {
    getMock.mockResolvedValueOnce(ctx("MAYORISTA001"));
    const { result } = renderHook(() => useCustomerPriceListContext("c1", true));
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(result.current.data?.customerPriceListName).toBe("MAYORISTA001");
    expect(getMock).toHaveBeenCalledWith("c1");
  });

  it("cambiar el cliente refresca el contexto live", async () => {
    getMock.mockResolvedValueOnce(ctx("MAYORISTA001")).mockResolvedValueOnce(ctx(null));
    const { result, rerender } = renderHook(({ id }) => useCustomerPriceListContext(id, true), {
      initialProps: { id: "c1" },
    });
    await waitFor(() => expect(result.current.data?.customerPriceListName).toBe("MAYORISTA001"));
    rerender({ id: "c2" });
    await waitFor(() => expect(result.current.status).toBe("ready"));
    await waitFor(() => expect(result.current.data?.customerPriceListName).toBeNull());
    expect(getMock).toHaveBeenNthCalledWith(2, "c2");
  });

  it("error del query → estado neutro 'error' sin lanzar", async () => {
    getMock.mockRejectedValueOnce(new Error("boom"));
    const { result } = renderHook(() => useCustomerPriceListContext("c1", true));
    await waitFor(() => expect(result.current.status).toBe("error"));
    expect(result.current.data).toBeNull();
  });

  it("solo lectura (enabled=false) o sin cliente: no consulta", () => {
    renderHook(() => useCustomerPriceListContext("c1", false));
    renderHook(() => useCustomerPriceListContext("", true));
    expect(getMock).not.toHaveBeenCalled();
  });

  it("ignora la respuesta tardía de un cliente anterior", async () => {
    let resolveFirst!: (v: ReturnType<typeof ctx>) => void;
    getMock
      .mockImplementationOnce(() => new Promise((r) => (resolveFirst = r)))
      .mockResolvedValueOnce(ctx("LISTA-C2"));
    const { result, rerender } = renderHook(({ id }) => useCustomerPriceListContext(id, true), {
      initialProps: { id: "c1" },
    });
    rerender({ id: "c2" });
    await waitFor(() => expect(result.current.data?.customerPriceListName).toBe("LISTA-C2"));
    resolveFirst(ctx("LISTA-C1-TARDIA"));
    await Promise.resolve();
    expect(result.current.data?.customerPriceListName).toBe("LISTA-C2");
  });
});
