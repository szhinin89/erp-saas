import { useOptionalI18n } from "../../../i18n/i18n";
import type { PriceListHeaderState } from "../utils/pricingTraceability";

/**
 * SALES-PRICING-UX-TRACEABILITY-07C: cabecera "Lista preferente" junto al cliente. Es la lista
 * EXPLÍCITA del cliente — nunca se llama "aplicada" porque cada línea puede resolver otra lista
 * o PVP. La lista default de la empresa solo aparece como texto secundario ("Predeterminada").
 * Componente puramente presentacional: recibe el estado ya decidido por resolvePriceListHeader.
 */
export function SalesPriceListContext({ state }: { state?: PriceListHeaderState }) {
  const { t } = useOptionalI18n();
  if (!state || state.kind === "hidden") return null;

  const muted = state.kind === "legacy" || state.kind === "contextError";
  const primary =
    state.kind === "preferred"
      ? t("sales.pricing.preferredList", { name: state.name })
      : state.kind === "none"
        ? t("sales.pricing.noPreferredList")
        : state.kind === "legacy"
          ? t("sales.pricing.traceabilityUnavailable")
          : t("sales.pricing.contextUnavailable");
  const secondary =
    (state.kind === "preferred" || state.kind === "none") && state.defaultName
      ? t("sales.pricing.defaultList", { name: state.defaultName })
      : null;

  return (
    <div
      className={`sales-price-list-context${muted ? " sales-price-list-context--muted" : ""}`}
      data-testid="sales-price-list-context"
    >
      <span className="material-symbols-outlined zh-icon-sm">sell</span>
      <span className="sales-price-list-context__primary">{primary}</span>
      {secondary && (
        <span className="sales-price-list-context__secondary">{secondary}</span>
      )}
    </div>
  );
}
