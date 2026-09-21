import type { CustomerPriceListContextDto } from "../api/customerPriceListContextService";

/**
 * SALES-PRICING-UX-TRACEABILITY-07C: decisiones PURAS de presentación del origen del pricing.
 * Nunca calculan precios ni eligen listas — solo interpretan lo que Pricing/los snapshots ya
 * dijeron. La ausencia de dato en documentos legacy (version null) jamás se convierte en "PVP" o
 * "sin lista".
 */

export type LinePriceListLabel =
  | { kind: "list"; name: string }
  | { kind: "pvp" }
  | { kind: "none" };

export interface LinePricingOrigin {
  _priceListId?: string | null;
  _priceListName?: string;
  _priceListIdAtSale?: string | null;
  _priceListNameAtSale?: string | null;
  _selectionSourceAtSale?: string | null;
  _traceabilityVersionAtSale?: number | null;
}

/** Versión de captura de trazabilidad actual (espejo de SalesInvoice.CurrentPricingTraceabilityVersion). */
export const PRICING_TRACEABILITY_V1 = 1;

export function resolveLinePriceListLabel(
  line: LinePricingOrigin,
  readOnly: boolean,
): LinePriceListLabel {
  // Captura en vivo (nueva venta / repricing): _priceListId null = PVP, string = lista efectiva.
  if (!readOnly && line._priceListId !== undefined) {
    return line._priceListId && line._priceListName
      ? { kind: "list", name: line._priceListName }
      : { kind: "pvp" };
  }
  // Línea en vivo sin id de lista (capturada antes de 07C): se muestra el nombre que ya trae, pero
  // nunca se infiere PVP a partir de un id ausente.
  if (!readOnly && line._priceListName) {
    return { kind: "list", name: line._priceListName };
  }

  // Snapshot persistido. Un id de lista siempre habla por sí mismo, con cualquier versión.
  if (line._priceListIdAtSale && line._priceListNameAtSale) {
    return { kind: "list", name: line._priceListNameAtSale };
  }
  // v1 sin lista ni origen de selección = PVP real (nunca dependemos del texto del nombre).
  if (
    line._traceabilityVersionAtSale === PRICING_TRACEABILITY_V1 &&
    !line._priceListIdAtSale &&
    (line._selectionSourceAtSale ?? null) === null
  ) {
    return { kind: "pvp" };
  }
  // Legacy/sin certeza: solo se muestra lo que el snapshot legacy realmente trae, sin inferir.
  return line._priceListNameAtSale
    ? { kind: "list", name: line._priceListNameAtSale }
    : { kind: "none" };
}

export type PriceListHeaderState =
  | { kind: "hidden" }
  | { kind: "preferred"; name: string; defaultName: string | null }
  | { kind: "none"; defaultName: string | null }
  | { kind: "legacy" }
  | { kind: "contextError" };

export interface PriceListHeaderInput {
  hasCustomer: boolean;
  readOnly: boolean;
  /** Factura ya guardada (solo se usa en readOnly): snapshot persistido. */
  saved: {
    pricingTraceabilityVersion: number | null;
    customerPreferredPriceListName: string | null;
  } | null;
  live: {
    status: "idle" | "loading" | "ready" | "error";
    data: CustomerPriceListContextDto | null;
  };
}

export function resolvePriceListHeader(input: PriceListHeaderInput): PriceListHeaderState {
  if (!input.hasCustomer) return { kind: "hidden" };

  if (input.readOnly && input.saved) {
    if (input.saved.pricingTraceabilityVersion !== PRICING_TRACEABILITY_V1) {
      return { kind: "legacy" };
    }
    return input.saved.customerPreferredPriceListName
      ? {
          kind: "preferred",
          name: input.saved.customerPreferredPriceListName,
          defaultName: null,
        }
      : { kind: "none", defaultName: null };
  }

  if (input.live.status === "error") return { kind: "contextError" };
  if (input.live.status !== "ready" || !input.live.data) return { kind: "hidden" };

  const defaultName = input.live.data.companyDefaultPriceListName ?? null;
  return input.live.data.customerPriceListName
    ? { kind: "preferred", name: input.live.data.customerPriceListName, defaultName }
    : { kind: "none", defaultName };
}
