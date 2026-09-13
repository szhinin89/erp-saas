import type { ReactNode } from "react";
import type { InvoiceItemSearchResultDto } from "../api/invoiceItemSearchService";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { Badge } from "../../../components/PageShell";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { stockBadgeInfo, discountBadgeText } from "../utils/salesCalc";

// SALES-ITEM-SEARCH-RESULTS-GRID-COMPONENT-01: extraído de SalesInvoiceDetailsSection —
// componente local del módulo Sales/POS (no Design System global) porque su layout depende de
// datos y reglas propias de ventas (stock disponible, precio regular/promo/final ya resueltos
// por PricingResolver, botón Agregar, highlight de búsqueda). Solo presentación: no hace fetch,
// no recalcula precios/descuentos/IVA/stock, no conoce emisión/XML/CxC/contabilidad — todo eso
// sigue siendo responsabilidad del padre (SalesInvoiceDetailsSection) y del backend.
// discountBadgeText vive en salesCalc.ts (SALES-INVOICE-LINES-GRID-UX-01): mismo criterio de
// insignia corta reutilizado también por las líneas ya agregadas a la factura.

// ── Resaltado de coincidencias ────────────────────────────────────────────────
function highlightMatch(text: string, query: string): ReactNode {
  const q = query.trim();
  if (!q) return text;
  const idx = text.toLowerCase().indexOf(q.toLowerCase());
  if (idx === -1) return text;
  return (
    <>
      {text.slice(0, idx)}
      <mark className="sf-search-highlight">
        {text.slice(idx, idx + q.length)}
      </mark>
      {text.slice(idx + q.length)}
    </>
  );
}

interface SalesItemSearchResultsGridProps {
  results: InvoiceItemSearchResultDto[];
  /** Término tal como lo escribió el usuario — solo para resaltar la coincidencia, nunca se
   * reenvía al backend desde acá (la búsqueda la dispara el padre). */
  searchTerm: string;
  /** Índice resaltado por navegación de teclado (ArrowUp/ArrowDown) en el padre; -1 = ninguno. */
  focusIndex: number;
  /** Agregar el ítem a la factura — misma acción para click en la fila y click en "Agregar". */
  onAdd: (item: InvoiceItemSearchResultDto) => void;
  /** Sincroniza el índice resaltado al pasar el mouse sobre una fila (mismo estado que la
   * navegación por teclado en el padre). */
  onHoverIndex: (index: number) => void;
  /** Registra el nodo de cada fila para que el padre pueda hacer scrollIntoView al navegar con
   * teclado (mismo patrón que ya usaba SalesInvoiceDetailsSection). */
  registerResultRef: (index: number, el: HTMLDivElement | null) => void;
}

/** Grilla horizontal de resultados del buscador de productos (POS/retail):
 * Código | Producto | Stock | Precio reg. | Promo | Precio final | Agregar. */
export function SalesItemSearchResultsGrid({
  results,
  searchTerm,
  focusIndex,
  onAdd,
  onHoverIndex,
  registerResultRef,
}: SalesItemSearchResultsGridProps) {
  const dc = getDecimalConfig();

  return (
    <>
      {/* SALES-ITEM-SEARCH-RESULT-TABLE-UX-01: grilla horizontal con encabezado de columnas
          (Código | Producto | Stock | Precio reg. | Promo | Precio final | Agregar) — reemplaza
          la tarjeta de 2 filas anterior (fila código+nombre+Agregar / fila Stock+precio con
          labels inline). Misma información, misma jerarquía de lectura Normal → Promo → Final,
          ahora expresada como columnas reales en vez de labels repetidos en cada fila. */}
      <div className="sf-search-columns-header" role="row">
        <span className="sf-search-columns-header__cell">Código</span>
        <span className="sf-search-columns-header__cell">Producto</span>
        <span className="sf-search-columns-header__cell">Stock</span>
        <span className="sf-search-columns-header__cell">Precio reg.</span>
        <span className="sf-search-columns-header__cell">Promo</span>
        <span className="sf-search-columns-header__cell">
          Precio final
          <ZHFieldHelp helpKey={HELP_KEYS.SALES_PRICING} />
        </span>
        <span className="sf-search-columns-header__cell sf-search-columns-header__cell--add">
          Agregar
        </span>
      </div>

      {results.map((item, i) => {
        const badge = stockBadgeInfo(item.availableStock ?? 0);
        const hasPrice = item.salePriceWithoutTax != null;
        // SALES-PRICE-LIST-DISCOUNT-VISIBILITY-01: cuando la lista de precios default aplica un
        // descuento/recargo, el precio destacado debe ser el que realmente se facturará (el
        // mismo que resolverá /items/{id}/pricing al agregar el ítem) — nunca el precio base sin
        // resolver. discountedFinalSalePrice ya viene calculado por el backend
        // (PricingCalculation, mismo motor que PricingResolver); no se recalcula nada acá, solo
        // se elige cuál de los dos precios ya resueltos mostrar.
        const hasDiscount = item.discountDescription != null;
        const effectiveFinal = hasDiscount
          ? item.discountedFinalSalePrice
          : item.finalSalePrice;

        return (
          <div
            key={item.id}
            ref={(el) => registerResultRef(i, el)}
            role="option"
            aria-selected={i === focusIndex}
            className={`sf-result${i === focusIndex ? " sf-result--focused" : ""}`}
            onClick={() => onAdd(item)}
            onMouseEnter={() => onHoverIndex(i)}
          >
            <span className="sf-result__col sf-result__col-code">
              <span className="sf-result__sku zh-code-value">{item.sku}</span>
            </span>

            <span
              className="sf-result__col sf-result__col-product"
              title={item.description}
            >
              <span className="sf-result__name">
                {highlightMatch(item.description, searchTerm)}
              </span>
            </span>

            <span className="sf-result__col sf-result__col-stock">
              {item.tracksStock ? (
                item.availableStock != null ? (
                  <>
                    <span className="sf-result__stock-qty">
                      {item.availableStock.toFixed(dc.quantity)}{" "}
                      {item.uomAbbrev}
                    </span>
                    <Badge label={badge.label} variant={badge.variant} upper size="md" />
                  </>
                ) : (
                  <Badge
                    label={stockBadgeInfo(0).label}
                    variant={stockBadgeInfo(0).variant}
                    upper
                    size="md"
                  />
                )
              ) : (
                <span className="sf-result__stock-qty sf-result__stock-qty--muted">
                  Sin control
                </span>
              )}
            </span>

            {/* SALES-ITEM-SEARCH-RESULT-PRICE-HIERARCHY-01: orden de lectura
                Normal → descuento/recargo → Precio final (destacado) — no al revés.
                "Lista General" (priceListName) ya no se muestra como texto suelto: queda solo en
                el title de la celda Promo junto al texto completo de la regla (p. ej. "Descuento
                5% (regla general)"), que tampoco se renderiza en pantalla. Mismos valores del
                DTO (salePriceWithoutTax / discountedFinalSalePrice / finalSalePrice), nunca
                recalculados. */}
            {hasPrice ? (
              <>
                <span className="sf-result__col sf-result__col-price-normal">
                  <ZHMoneyValue
                    value={item.salePriceWithoutTax!}
                    decimals={dc.salesUnitPrice}
                    className={
                      hasDiscount
                        ? "sf-result__price-normal"
                        : "sf-result__price-normal sf-result__price-normal--flat"
                    }
                  />
                </span>
                <span
                  className="sf-result__col sf-result__col-promo"
                  title={
                    hasDiscount
                      ? item.priceListName
                        ? `${item.discountDescription} — Lista: ${item.priceListName}`
                        : item.discountDescription!
                      : undefined
                  }
                >
                  {hasDiscount ? (
                    <span className="sf-result__discount-tag">
                      {discountBadgeText(item.discountDescription!)}
                    </span>
                  ) : (
                    <span className="sf-result__promo-empty">—</span>
                  )}
                </span>
                <span className="sf-result__col sf-result__col-price-final">
                  {effectiveFinal != null && (
                    <ZHMoneyValue
                      value={effectiveFinal}
                      decimals={dc.salesUnitPrice}
                      className="sf-result__price-final"
                    />
                  )}
                </span>
              </>
            ) : (
              <span className="sf-result__col sf-result__col-no-price">
                <span className="sf-result__no-price">Sin precio</span>
                <span className="sf-result__meta-sub">Sin precio configurado</span>
              </span>
            )}

            <span className="sf-result__col sf-result__col-add">
              <ZHBtn
                type="button"
                variant="primary"
                size="sm"
                className="sf-result__add-btn"
                onClick={(e) => {
                  e.stopPropagation();
                  onAdd(item);
                }}
              >
                <span className="material-symbols-outlined zh-icon-sm">
                  add_shopping_cart
                </span>
                Agregar
              </ZHBtn>
            </span>
          </div>
        );
      })}
    </>
  );
}
