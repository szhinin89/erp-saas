import { Link } from "react-router-dom";
import type { SalesInvoiceDetailDto } from "../api/salesService";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";
import type { WarehouseDto, ItemWarehouseAvailabilityDto } from "../../inventory/types";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhWarehouseSelector } from "../../../components/zh/inputs/ZhWarehouseSelector";
import { Badge } from "../../../components/PageShell";
import { ZHRowDeleteAction } from "../../../components/zh/ZHRowDeleteAction";
import { ZHLineCard } from "../../../components/zh/ZHLineCard";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHInputGroup } from "../../../components/zh/ZHInputGroup";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import {
  lineNet,
  calcLineTax,
  lineExceedsStock,
  stockBadgeInfo,
  parenthesizeRateLabel,
  stockExceededMessage,
  presentationEquivalenceLabel,
  discountBadgeText,
} from "../utils/salesCalc";

interface SalesInvoiceLineGridRowProps {
  line: SalesLineFormValues;
  backendLine?: SalesInvoiceDetailDto;
  disabled: boolean;
  readOnly: boolean;
  vatLabel: string;
  vatRates?: Record<string, number>;
  warehouses: WarehouseDto[];
  selectedWarehouseId: string;
  onUpdate: (key: number, field: string, value: unknown) => void;
  onUpdateWarehouse: (
    key: number,
    warehouseId: string,
    option?: ItemWarehouseAvailabilityDto,
  ) => void;
  onUpdatePresentation?: (key: number, packagingLevelId: string) => void;
  onRemove: (key: number) => void;
  /** Posición de la línea en la factura (0-based) — solo para el número visible "N." junto al
   * botón eliminar; no participa en ningún cálculo. */
  index: number;
}

/**
 * SALES-INVOICE-LINES-GRID-UX-01 / 01B: fila de línea de factura ya agregada, extraída de
 * SalesInvoiceDetailsSection (antes `SalesProductCard`) — mismo criterio de grilla horizontal
 * compacta CON cabecera de columnas que `SalesItemSearchResultsGrid` (buscador), pero adaptada al
 * dominio de una línea ya confirmada: aquí los datos no son "propuestas a elegir" sino valores
 * editables (cantidad, precio facturado, % descuento manual) más el detalle de la regla de
 * precios que ya se aplicó al agregar el ítem. Debe renderizarse siempre dentro de
 * `SalesInvoiceLinesGrid` (dueña de la cabecera) — es SOLO la fila, alineada a esa cabecera por
 * compartir el mismo `--sfl-cols` (ver sales-product-card.css). Componente local del módulo
 * Sales — no sube a Design System global por la misma razón que el buscador: depende de reglas y
 * datos propios de Ventas (stock, pricing, presentaciones), no de un patrón genérico reutilizable
 * todavía.
 *
 * Solo presentación: no recalcula precio/descuento/IVA/stock — todos los valores mostrados ya
 * vienen resueltos (línea del formulario + snapshot de pricing capturado al agregar el ítem, o
 * backendLine cuando la factura ya fue guardada). Los inputs editables (cantidad, descuento %,
 * precio facturado) siguen delegando el cambio real a `onUpdate`, igual que antes de esta
 * extracción — ningún cálculo se movió al frontend.
 */
export function SalesInvoiceLineGridRow({
  line,
  backendLine,
  disabled,
  readOnly,
  vatLabel,
  vatRates,
  warehouses,
  selectedWarehouseId,
  onUpdate,
  onUpdateWarehouse,
  onUpdatePresentation,
  onRemove,
  index,
}: SalesInvoiceLineGridRowProps) {
  const dc = getDecimalConfig();
  const previewNet = lineNet(line);
  const previewTax = calcLineTax(line, vatRates);
  const total =
    backendLine?.taxInclusiveTotal ??
    previewNet + previewTax.vat + previewTax.ice;

  // Base e IVA de línea: cuando ya existe backendLine (factura guardada/emitida), se usan sus
  // valores — misma fuente que ya usaba `total` — para que Base/IVA/Total nunca queden
  // desalineados entre sí (mismo criterio que evitó el bug de FIX04 con la cantidad). En un
  // borrador nuevo sin backendLine, los tres salen del mismo cálculo local (previewNet/previewTax).
  const baseAmount = backendLine?.taxableBase ?? previewNet;
  const vatAmount = backendLine?.vatAmount ?? previewTax.vat;
  const ivaTotalsLabel = parenthesizeRateLabel(vatLabel);

  const sku = line._sku ?? backendLine?.snapshotSku ?? "";
  const name = line._name ?? backendLine?.snapshotItemName ?? line.description;
  // line._cost ya no se muestra en el modo de venta POS por defecto (FIX06) — el dato sigue
  // existiendo en el modelo, solo se dejó de renderizar en esta tarjeta.
  const pvp = line._pvp;
  // SALES-PRICE-LIST-DISCOUNT-VISIBILITY-01: "Precio lista" es el precio base del ítem
  // (_basePrice, antes de que la lista de precios default aplique cualquier regla) — nunca el
  // ya-descontado `pvp`, que es el que efectivamente terminó en `unitPrice`. Fallback a `pvp`
  // solo para líneas recargadas de un borrador viejo sin el snapshot nuevo.
  const listPrice = line._basePrice ?? pvp;
  const discountDescription = line._isManualPrice
    ? null
    : (line._discountDescription ?? null);
  const isManualPrice = line._isManualPrice === true;
  const stockQty = line._stockQty;
  const stockWarehouse = line._stockWarehouse;
  // Advertencia preventiva (UX) — solo con el dato de disponibilidad ya cargado en pantalla;
  // el backend sigue siendo quien bloquea la emisión (ver lineExceedsStock, salesCalc.ts).
  const exceedsStock = lineExceedsStock(line);
  // SALES-PRESENTATIONS-03: selector solo visible cuando el ítem tiene más de una presentación
  // (unidad base + al menos una caja/pack) — con una sola (o ninguna) presentación, no se
  // muestra selector: el producto se vende en unidad base sin ruido visual, igual que hoy.
  const packagingOptions = line._packagingLevels ?? [];
  const hasPresentations = packagingOptions.length > 1 && !!onUpdatePresentation;
  const selectedPresentation = packagingOptions.find(
    (p) => p.id === line.packagingLevelId,
  );
  const presentationTag = selectedPresentation
    ? `${selectedPresentation.name} x ${selectedPresentation.baseQuantity} ${line.baseUomCode ?? ""}`.trim()
    : null;
  const equivalenceLabel = presentationEquivalenceLabel(line);

  // SALES-ITEM-SEARCH-RESULT-PRICE-HIERARCHY-01 / SALES-INVOICE-LINES-GRID-UX-01: mismo criterio
  // que el buscador — el texto completo de la regla ("Descuento 5% (regla general)") solo va en
  // el title de la columna Descuento; en pantalla se ve la insignia corta (discountBadgeText).
  const discountTitle = discountDescription
    ? line._priceListName
      ? `${discountDescription} — Lista: ${line._priceListName}`
      : discountDescription
    : undefined;

  return (
    // DS-LINE-CARD-UNIFY-01 / SALES-INVOICE-LINES-GRID-UX-01B: ZHLineCard ya no recibe `rail` —
    // el número de línea y el botón eliminar pasan a ser la primera columna real del grid interno
    // (.sf-product__line-cell), para que la fila comparta el mismo grid-template-columns que la
    // cabecera (.sfl-header, ver SalesInvoiceLinesGrid) columna por columna, incluida esa. Sigue
    // usando ZHLineCard solo por el marco visual externo (borde/sombra/radio ya definidos en
    // .sf-product-card.zh-line-card) — sin rail, ZHLineCard renderiza un único hijo (el body) que
    // ocupa todo el ancho, así que no cambia nada de ese marco.
    <ZHLineCard className="sf-product-card">
      <div className="sf-product">
        {/* Col 1: Línea — número + eliminar, compacto. Sin label: la cabecera ya dice "Línea". */}
        <div className="sf-product__line-cell">
          <span className="sf-product__rail-index">
            {String(index + 1).padStart(2, "0")}
          </span>
          {!readOnly && (
            // Basurero = eliminar el producto de la factura; una X se reserva para
            // cerrar/cancelar modales o paneles, nunca para esta acción destructiva.
            <ZHRowDeleteAction
              compact
              showText={false}
              title="Eliminar producto de la factura"
              label="Eliminar producto de la factura"
              onClick={() => onRemove(line._key)}
              disabled={disabled}
              className="sf-product__delete-btn"
            />
          )}
        </div>

        {/* Col 2: Producto — SKU como chip monoespaciado (mismo criterio visual que el buscador,
            .sf-result__sku) + nombre destacado; sin badge de IVA junto al nombre (pedido
            explícito del usuario, FIX06H): la tasa ya se ve en la columna de totales
            ("IVA (15%)"). */}
        <div className="sf-product__info">
          <div className="sf-product__title-row">
            {sku && <span className="sf-product__code zh-code-value">{sku}</span>}
          </div>
          <div className="sf-product__name zh-row-title" title={name}>
            {name}
          </div>
          {presentationTag && (
            // Presentación vendida, junto al nombre — nunca reemplaza el nombre del producto
            // (regla 10: "nombre producto amplio", la presentación es secundaria).
            <div className="sf-product__presentation-tag">{presentationTag}</div>
          )}
        </div>

        {/* Col 3: Precio lista — solo el precio base y, si existe, el nombre de la lista debajo
            (secundario). Sin label propio (SALES-INVOICE-LINES-GRID-UX-01B: la cabecera "Precio
            lista" ya lo dice — repetirlo en cada fila era exactamente el label duplicado que
            pidió quitar). El detalle del descuento/regla ya no vive acá: es la columna Descuento
            la que lo muestra — costo nunca se muestra en el modo de venta POS. */}
        <div className="sf-product__pricelist">
          {listPrice != null ? (
            <div className="sf-product__pricelist-row">
              <ZHMoneyValue
                value={listPrice}
                emphasis="strong"
                className="sf-product__pricelist-value sf-product__pricelist-value--bold"
              />
              {line._priceListName && (
                <span className="sf-product__pricelist-name">
                  {line._priceListName}
                </span>
              )}
            </div>
          ) : (
            <span className="sales-invoice-details-empty-value">—</span>
          )}
        </div>

        {/* Col 4: Descuento — % editable (line.discountPct, siempre presente) + insignia corta de
            la regla de precios aplicada al agregar el ítem, si existe (nunca "regla general"/
            "excepción" como texto principal, ver discountBadgeText en salesCalc.ts; el texto
            completo sigue disponible en el title de la columna). Son dos conceptos distintos que
            comparten columna a propósito: el % manual es lo que el cajero puede tocar, la
            insignia es solo informativa de por qué el precio facturado quedó como quedó. */}
        <div className="sf-product__discount" title={discountTitle}>
          <ZHFieldLabel size="sm" className="sf-product__disc-label">
            Dto. %
          </ZHFieldLabel>
          <ZhDecimalInput
            // Input no controlado (defaultValue) por diseño — se remonta cuando line.discountPct
            // cambia por una vía distinta a este mismo input (p. ej. al recargar una línea), para
            // que el DOM nunca quede desincronizado del valor real. Sin key, React reutiliza el
            // nodo y el usuario seguiría viendo el valor viejo (mismo bug que quantity, ver abajo).
            key={line.discountPct ?? 0}
            className="sf-product__disc-input"
            density="compact"
            decimals={dc.percentage}
            positiveOnly
            defaultValue={line.discountPct ?? 0}
            onBlur={(e) =>
              onUpdate(
                line._key,
                "discountPct",
                Math.min(100, Math.max(0, Number(e.target.value) || 0)),
              )
            }
            disabled={disabled}
          />
          {discountDescription && (
            <span className="sf-product__discount-tag">
              {discountBadgeText(discountDescription)}
            </span>
          )}
        </div>

        {/* Col 5: Precio facturado — input editable, sin recalcular nada en frontend. */}
        <div className="sf-product__price-block">
          <ZHFieldLabel size="sm" className="sf-product__price-label">
            Precio facturado sin IVA
          </ZHFieldLabel>
          <ZHInputGroup className="sf-product__price-wrap" prefix="$">
            <ZhDecimalInput
              key={line.unitPrice}
              className="sf-product__price-input"
              density="compact"
              decimals={dc.salesUnitPrice}
              positiveOnly
              defaultValue={line.unitPrice}
              onBlur={(e) =>
                onUpdate(line._key, "unitPrice", Number(e.target.value) || 0)
              }
              disabled={disabled}
            />
          </ZHInputGroup>
          {isManualPrice && (
            // SALES-PRICE-LIST-DISCOUNT-VISIBILITY-01: distingue "precio de lista/regla" de
            // "precio editado a mano" — una vez editado, la insignia de descuento de la col. 3 ya
            // no aplica (discountDescription se resuelve a null arriba) y se reemplaza por este aviso.
            <Badge label="Precio manual" variant="orange" upper size="md" />
          )}
        </div>

        {/* Col 6: Stock / Ubicación — reorganizada en 3 grupos verticales claros
            (SALES-INVOICE-LINES-STOCK-LOCATION-COLUMN-UX-01K): (1) resumen de stock —
            cantidad+unidad como dato principal junto al badge de estado, con el label "Stock" y
            su ayuda reducidos a una cabecera discreta; (2) ubicación — selector de bodega, con
            label "Ubicación" igual de discreto; (3) acción secundaria — "Ver stock global". Se
            quitó el ícono grande "assignment" (era puramente decorativo, sin dato propio, y
            competía visualmente con la cantidad) — ningún dato se eliminó, solo se reordenó y
            compactó el markup existente. */}
        <div className="sf-product__stock-box">
          <div className="sf-product__stock-data">
            <div className="sf-product__stock-header">
              <ZHFieldLabel size="sm" className="sf-product__stock-label">
                Stock
              </ZHFieldLabel>
              <ZHFieldHelp helpKey={HELP_KEYS.SALES_STOCK} />
            </div>
            <div className="sf-product__stock-summary">
              <span
                className={`sf-product__stock-qty ${stockQty == null ? "sf-product__stock-qty--empty" : ""}`}
              >
                {stockQty != null ? stockQty : "—"}
                {stockQty != null && (
                  <span className="sf-product__stock-uom"> UDS</span>
                )}
              </span>
              {line._tracksStock && stockQty != null && (
                <Badge
                  label={
                    exceedsStock ? "Cantidad excede stock" : stockBadgeInfo(stockQty).label
                  }
                  variant={
                    exceedsStock ? "red" : stockBadgeInfo(stockQty).variant
                  }
                  size="md"
                />
              )}
            </div>
            {exceedsStock && (
              <div className="sf-product__stock-warning">
                {stockExceededMessage(line)}
              </div>
            )}
            {line._tracksStock && (
              <>
                <div className="sf-product__stock-location">
                  <ZHFieldLabel size="sm" className="sf-product__stock-label">
                    Ubicación
                  </ZHFieldLabel>
                  {!readOnly ? (
                    <ZhWarehouseSelector
                      value={line.warehouseId ?? null}
                      onChange={(id, option) =>
                        onUpdateWarehouse(line._key, id, option)
                      }
                      itemId={line.itemId}
                      fallbackWarehouses={warehouses}
                      defaultWarehouseId={selectedWarehouseId}
                      disabled={disabled}
                      placeholder="Seleccione bodega"
                    />
                  ) : (
                    <div className="sf-product__stock-wh">
                      {warehouses.find((w) => w.id === line.warehouseId)?.name ??
                        stockWarehouse ??
                        "—"}
                    </div>
                  )}
                </div>
                {line.itemId && (
                  // Reutiliza el Kardex ya existente (mismo destino que "Ver Movimiento de
                  // Inventario" en el listado de facturas) — sin endpoint ni componente nuevo.
                  // Nueva pestaña: no debe abandonar la venta en curso.
                  <Link
                    to={`/inventory/kardex?productId=${line.itemId}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="zh-inline-action sf-product__stock-global-link"
                  >
                    <span className="material-symbols-outlined zh-icon-sm">
                      open_in_new
                    </span>
                    Ver stock global
                  </Link>
                )}
              </>
            )}
          </div>
        </div>

        {/* Col 7: Cantidad — NO usa density="compact" (a diferencia de Descuento/Precio Facturado,
            SALES-DS-VISUAL-REAL-17): su tamaño 21px es una excepción documentada y verificada en
            navegador real (ver nota FIX06F más abajo en el bloque .sf-product__qty-input,
            sales-product-card.css) para que "2.0000" no se recorte — es el campo protagonista de
            la ficha por diseño, igual que Total línea. Comparte el mismo border/focus-ring que
            los otros dos inputs (1.5px solid + box-shadow var(--color-focus-ring)); solo difiere
            en tamaño/padding por esa razón funcional, no por inconsistencia sin resolver. */}
        <div className="sf-product__qty">
          {hasPresentations && (
            // SALES-PRESENTATIONS-03: selector de presentación (unidad/caja/pack) — mismo patrón
            // inline con ZhSelect que Compras (PurchasesPage.tsx PurchaseLineCard), sin componente
            // nuevo. Solo visible cuando el ítem tiene más de una presentación configurada.
            <>
              <ZHFieldLabel size="sm" className="sf-product__qty-label">
                Presentación
              </ZHFieldLabel>
              <ZhSelect
                density="compact"
                className="sf-product__presentation-select"
                value={line.packagingLevelId ?? ""}
                onChange={(e) =>
                  onUpdatePresentation?.(line._key, e.target.value)
                }
                disabled={disabled}
              >
                {packagingOptions
                  .filter((p) => p.isBaseUnit)
                  .map((p) => (
                    <option key={p.id} value="">
                      {p.name}
                    </option>
                  ))}
                {packagingOptions
                  .filter((p) => !p.isBaseUnit)
                  .map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} x {p.baseQuantity} {line.baseUomCode}
                    </option>
                  ))}
              </ZhSelect>
            </>
          )}
          <ZHFieldLabel size="sm" className="sf-product__qty-label">
            Cantidad
          </ZHFieldLabel>
          <ZhDecimalInput
            // key={line.quantity}: fuerza el remontaje del input cuando la cantidad cambia desde
            // afuera de este blur (reescaneo del mismo producto en useSalesPage.addLineWithItem,
            // que incrementa line.quantity directamente). Sin esto, el input no controlado
            // (defaultValue) conserva el valor mostrado en el primer render — el subtotal (que sí
            // lee line.quantity en vivo) queda desincronizado de la cantidad visible.
            key={line.quantity}
            className="sf-product__qty-input"
            decimals={dc.quantity}
            positiveOnly
            defaultValue={line.quantity}
            onBlur={(e) =>
              onUpdate(line._key, "quantity", Number(e.target.value) || 1)
            }
            disabled={disabled}
          />
          {equivalenceLabel && (
            <div className="sf-product__presentation-equivalence">
              {equivalenceLabel}
            </div>
          )}
        </div>

        {/* Col 8: Total línea — el dato más fuerte del bloque es el Total línea */}
        <div className="sf-product__subtotal">
          <div className="sf-product__subtotal-row">
            <ZHFieldLabel size="sm" className="sf-product__subtotal-label">
              Base sin IVA
            </ZHFieldLabel>
            <ZHMoneyValue value={baseAmount} className="sf-product__subtotal-value" />
          </div>
          <div className="sf-product__subtotal-row">
            <ZHFieldLabel size="sm" className="sf-product__subtotal-label">
              {ivaTotalsLabel}
            </ZHFieldLabel>
            <ZHMoneyValue value={vatAmount} className="sf-product__subtotal-value" />
          </div>
          <ZHFieldLabel size="sm" className="sf-product__total-label">
            Total línea
          </ZHFieldLabel>
          <ZHMoneyValue
            value={total}
            emphasis="total"
            className="sf-product__total-amount"
          />
          <div className="sf-product__tax-incl">Imp. incluidos</div>
        </div>
      </div>
    </ZHLineCard>
  );
}
