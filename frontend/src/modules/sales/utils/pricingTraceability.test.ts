import { describe, it, expect } from "vitest";
import {
  resolveLinePriceListLabel,
  resolvePriceListHeader,
  type PriceListHeaderInput,
} from "./pricingTraceability";

const ctxReady = (customer: string | null, def: string | null): PriceListHeaderInput["live"] => ({
  status: "ready",
  data: {
    customerPriceListId: customer ? "c-list" : null,
    customerPriceListName: customer,
    companyDefaultPriceListId: def ? "d-list" : null,
    companyDefaultPriceListName: def,
  },
});
const base = { hasCustomer: true, readOnly: false, saved: null };
const idle = { status: "idle" as const, data: null };

describe("SALES-PRICING-UX-TRACEABILITY-07C — cabecera (venta nueva / Draft editable)", () => {
  it("cliente con lista propia → 'preferred' con su nombre y la default solo como secundaria", () => {
    const state = resolvePriceListHeader({ ...base, live: ctxReady("MAYORISTA001", "Lista General") });
    expect(state).toEqual({ kind: "preferred", name: "MAYORISTA001", defaultName: "Lista General" });
  });

  it("cliente sin lista propia → 'none'; la default NO se presenta como lista del cliente", () => {
    const state = resolvePriceListHeader({ ...base, live: ctxReady(null, "Lista General") });
    expect(state).toEqual({ kind: "none", defaultName: "Lista General" });
    expect(state.kind).not.toBe("preferred");
  });

  it("sin cliente → oculto", () => {
    expect(resolvePriceListHeader({ ...base, hasCustomer: false, live: ctxReady("X", null) })).toEqual({
      kind: "hidden",
    });
  });

  it("cargando o sin consultar → oculto (sin parpadeo de texto obsoleto)", () => {
    expect(resolvePriceListHeader({ ...base, live: idle }).kind).toBe("hidden");
    expect(resolvePriceListHeader({ ...base, live: { status: "loading", data: null } }).kind).toBe("hidden");
  });

  it("error del query → estado neutro 'contextError', no oculta ni lanza", () => {
    expect(resolvePriceListHeader({ ...base, live: { status: "error", data: null } })).toEqual({
      kind: "contextError",
    });
  });
});

describe("SALES-PRICING-UX-TRACEABILITY-07C — cabecera histórica (solo snapshots)", () => {
  const ro = { hasCustomer: true, readOnly: true };

  it("v1 con lista preferente snapshot → 'preferred', ignora por completo el contexto vivo", () => {
    const state = resolvePriceListHeader({
      ...ro,
      saved: { pricingTraceabilityVersion: 1, customerPreferredPriceListName: "MAYORISTA001" },
      // La configuración ACTUAL cambió: el cliente hoy tiene otra lista — no debe influir.
      live: ctxReady("OTRA-LISTA", "Lista General"),
    });
    expect(state).toEqual({ kind: "preferred", name: "MAYORISTA001", defaultName: null });
  });

  it("v1 sin lista preferente snapshot → 'none' (dato real, no ausencia)", () => {
    const state = resolvePriceListHeader({
      ...ro,
      saved: { pricingTraceabilityVersion: 1, customerPreferredPriceListName: null },
      live: ctxReady("LISTA-NUEVA-HOY", null),
    });
    expect(state).toEqual({ kind: "none", defaultName: null });
  });

  it("version null (legacy) → 'legacy', nunca infiere 'sin lista'", () => {
    const state = resolvePriceListHeader({
      ...ro,
      saved: { pricingTraceabilityVersion: null, customerPreferredPriceListName: null },
      live: ctxReady(null, "Lista General"),
    });
    expect(state).toEqual({ kind: "legacy" });
  });
});

describe("SALES-PRICING-UX-TRACEABILITY-07C — texto secundario por línea", () => {
  it("captura en vivo: lista del cliente → nombre de lista", () => {
    expect(
      resolveLinePriceListLabel({ _priceListId: "l1", _priceListName: "MAYORISTA001" }, false),
    ).toEqual({ kind: "list", name: "MAYORISTA001" });
  });

  it("captura en vivo: fallback a default → nombre de la lista default", () => {
    expect(
      resolveLinePriceListLabel({ _priceListId: "l2", _priceListName: "Lista General" }, false),
    ).toEqual({ kind: "list", name: "Lista General" });
  });

  it("captura en vivo: PVP aunque el backend mande el sentinel 'Precio base' como nombre", () => {
    expect(
      resolveLinePriceListLabel({ _priceListId: null, _priceListName: "Precio base" }, false),
    ).toEqual({ kind: "pvp" });
  });

  it("snapshot v1: con id de lista → nombre snapshot (Customer o CompanyDefault)", () => {
    for (const source of ["Customer", "CompanyDefault"]) {
      expect(
        resolveLinePriceListLabel(
          {
            _priceListIdAtSale: "l1",
            _priceListNameAtSale: "Nombre al vender",
            _selectionSourceAtSale: source,
            _traceabilityVersionAtSale: 1,
          },
          true,
        ),
      ).toEqual({ kind: "list", name: "Nombre al vender" });
    }
  });

  it("snapshot v1: sin lista y SelectionSource null → PVP (aunque el nombre snapshot sea 'Precio base')", () => {
    expect(
      resolveLinePriceListLabel(
        {
          _priceListIdAtSale: null,
          _priceListNameAtSale: "Precio base",
          _selectionSourceAtSale: null,
          _traceabilityVersionAtSale: 1,
        },
        true,
      ),
    ).toEqual({ kind: "pvp" });
  });

  it("legacy (version null): NO se infiere PVP por ausencia de datos", () => {
    expect(
      resolveLinePriceListLabel(
        {
          _priceListIdAtSale: null,
          _priceListNameAtSale: null,
          _selectionSourceAtSale: null,
          _traceabilityVersionAtSale: null,
        },
        true,
      ),
    ).toEqual({ kind: "none" });
  });

  it("legacy: solo muestra el nombre que el snapshot legacy realmente trae, sin fabricar origen", () => {
    expect(
      resolveLinePriceListLabel(
        { _priceListNameAtSale: "Lista General", _traceabilityVersionAtSale: null },
        true,
      ),
    ).toEqual({ kind: "list", name: "Lista General" });
  });

  it("en solo lectura ignora la captura en vivo (un Draft recargado no mezcla en vivo con snapshot)", () => {
    expect(
      resolveLinePriceListLabel(
        {
          _priceListId: "live",
          _priceListName: "Lista viva",
          _priceListIdAtSale: "snap",
          _priceListNameAtSale: "Lista snapshot",
          _traceabilityVersionAtSale: 1,
        },
        true,
      ),
    ).toEqual({ kind: "list", name: "Lista snapshot" });
  });
});

describe("SALES-PRICING-UX-TRACEABILITY-07C — línea en vivo sin id de lista", () => {
  it("muestra el nombre existente pero nunca infiere PVP por un id ausente", () => {
    expect(resolveLinePriceListLabel({ _priceListName: "Lista General" }, false)).toEqual({
      kind: "list",
      name: "Lista General",
    });
    expect(resolveLinePriceListLabel({}, false)).toEqual({ kind: "none" });
  });
});
