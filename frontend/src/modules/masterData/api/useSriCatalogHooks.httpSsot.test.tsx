// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";

const facade = vi.hoisted(() => ({ supplierTypes: vi.fn(), idTypes: vi.fn() }));

vi.mock("../../items/facades/sriLookupFacade", () => ({ sriLookupFacade: facade }));

import { useSriSupplierTypes } from "./useSriSupplierTypes";
import { useSriIdTypes, useSriIdTypesByUsage } from "./useSriIdTypes";

/**
 * ZH-FRONTEND-HTTP-CLIENT-SSOT-01 — masterData ya no tiene su propio cliente de
 * /catalog/sri-supplier-types ni /catalog/sri-id-types: consume la facade pública del owner
 * (items/catalog). Mismo contrato de hook (options/loading) y mismo manejo de error.
 */
describe("masterData — catálogos SRI vía sriLookupFacade", () => {
  beforeEach(() => {
    facade.supplierTypes.mockReset();
    facade.idTypes.mockReset();
  });

  it("useSriSupplierTypes delega en sriLookupFacade.supplierTypes", async () => {
    facade.supplierTypes.mockResolvedValue([{ code: "01", name: "Persona natural" }]);
    const { result } = renderHook(() => useSriSupplierTypes());
    await waitFor(() => expect(result.current.options).toHaveLength(1));
    expect(facade.supplierTypes).toHaveBeenCalledWith();
    expect(result.current.loading).toBe(false);
  });

  it("useSriIdTypes sin uso → idTypes() (GET /catalog/sri-id-types)", async () => {
    facade.idTypes.mockResolvedValue([{ code: "05", name: "Cédula", digits: 10 }]);
    const { result } = renderHook(() => useSriIdTypes());
    await waitFor(() => expect(result.current.options).toHaveLength(1));
    expect(facade.idTypes).toHaveBeenCalledWith();
  });

  it("useSriIdTypesByUsage pasa el uso y recarga al cambiarlo", async () => {
    facade.idTypes.mockResolvedValue([]);
    const { rerender } = renderHook(({ usage }) => useSriIdTypesByUsage(usage), {
      initialProps: { usage: "customer" },
    });
    await waitFor(() => expect(facade.idTypes).toHaveBeenCalledWith("customer"));
    rerender({ usage: "supplier" });
    await waitFor(() => expect(facade.idTypes).toHaveBeenCalledWith("supplier"));
  });

  it("un error deja la lista vacía, sin fallback hardcodeado (mismo comportamiento previo)", async () => {
    facade.supplierTypes.mockRejectedValue(new Error("offline"));
    const { result } = renderHook(() => useSriSupplierTypes());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.options).toEqual([]);
  });
});
