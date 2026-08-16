import { useCallback, useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import type { SalesInvoiceDetailDto } from "../api/salesService";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";
import { invoiceItemSearchService } from "../api/invoiceItemSearchService";
import type { InvoiceItemSearchResultDto } from "../api/invoiceItemSearchService";
import type { WarehouseDto, ItemWarehouseAvailabilityDto } from "../../inventory/types";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhWarehouseSelector } from "../../../components/zh/inputs/ZhWarehouseSelector";
import { Badge } from "../../../components/PageShell";
import { ZHRowDeleteAction } from "../../../components/zh/ZHRowDeleteAction";
import { ZHLineCard } from "../../../components/zh/ZHLineCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatMoney, formatMoneyWithSymbol } from "../../../lib/sanitizers";
import {
  lineNet,
  calcLineTax,
  formatVatLabel,
  lineExceedsStock,
  stockBadgeInfo,
  parenthesizeRateLabel,
} from "../utils/salesCalc";
import "../styles/sales-product-card.css";

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

export type LineWithKey = SalesLineFormValues;

interface SalesInvoiceDetailsSectionProps {
  lines: LineWithKey[];
  backendLines?: SalesInvoiceDetailDto[];
  readOnly: boolean;
  disabled: boolean;
  onRemoveLine: (key: number) => void;
  onUpdateLine: (key: number, field: string, value: unknown) => void;
  onAddItemLine: (item: InvoiceItemSearchResultDto) => Promise<void>;
  onUpdateLineWarehouse: (
    key: number,
    warehouseId: string,
    option?: ItemWarehouseAvailabilityDto,
  ) => void;
  warehouses: WarehouseDto[];
  selectedWarehouseId: string;
  onWarehouseChange: (id: string) => void;
  vatRates?: Record<string, number>;
  /** Cambia (se incrementa) cada vez que el buscador de productos debe recibir foco — UX retail. */
  focusSignal?: number;
}

export function SalesInvoiceDetailsSection({
  lines,
  backendLines,
  readOnly,
  disabled,
  onRemoveLine,
  onUpdateLine,
  onAddItemLine,
  onUpdateLineWarehouse,
  warehouses,
  selectedWarehouseId,
  onWarehouseChange,
  vatRates,
  focusSignal,
}: SalesInvoiceDetailsSectionProps) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<InvoiceItemSearchResultDto[]>([]);
  const [open, setOpen] = useState(false);
  const [focusIdx, setFocusIdx] = useState(-1);
  const [loading, setLoading] = useState(false);
  const [selecting, setSelecting] = useState(false);
  const searchRef = useRef<HTMLDivElement>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const resultRefs = useRef<Array<HTMLDivElement | null>>([]);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);
  const searchVersionRef = useRef(0);

  // UX retail: foco automático al buscador de productos al entrar a "Nueva
  // Venta" y después de cada "Nueva venta" tras emitir (ver productSearchFocusKey en useSalesPage.ts).
  useEffect(() => {
    if (!disabled) searchInputRef.current?.focus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [focusSignal]);

  // Atajo global F2: reenfoca el buscador de productos desde cualquier parte de la página.
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === "F2" && !disabled) {
        e.preventDefault();
        searchInputRef.current?.focus();
      }
    };
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [disabled]);

  // Mantiene visible el resultado resaltado al navegar la lista con teclado.
  useEffect(() => {
    if (focusIdx >= 0)
      resultRefs.current[focusIdx]?.scrollIntoView({ block: "nearest" });
  }, [focusIdx]);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    if (!open || query.length < 2) {
      setResults([]);
      setLoading(false);
      return;
    }
    setLoading(true);
    debounceRef.current = setTimeout(async () => {
      const version = ++searchVersionRef.current;
      try {
        const res = await invoiceItemSearchService.search({
          q: query.trim(),
          warehouseId: selectedWarehouseId || undefined,
          pageSize: 10,
        });
        if (version === searchVersionRef.current) {
          setResults(res);
          setFocusIdx(-1);
        }
      } catch {
        if (version === searchVersionRef.current) setResults([]);
      } finally {
        if (version === searchVersionRef.current) setLoading(false);
      }
    }, 300);
    return () => clearTimeout(debounceRef.current);
  }, [query, open, selectedWarehouseId]);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (searchRef.current && !searchRef.current.contains(e.target as Node))
        setOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const selectItem = useCallback(
    async (item: InvoiceItemSearchResultDto) => {
      setOpen(false);
      setQuery("");
      setResults([]);
      setSelecting(true);
      try {
        await onAddItemLine(item);
      } finally {
        setSelecting(false);
      }
    },
    [onAddItemLine],
  );

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") {
      if (!open || results.length === 0) return;
      e.preventDefault();
      setFocusIdx((i) => Math.min(i + 1, results.length - 1));
      return;
    }
    if (e.key === "ArrowUp") {
      if (!open || results.length === 0) return;
      e.preventDefault();
      setFocusIdx((i) => Math.max(i - 1, 0));
      return;
    }
    if (e.key === "Escape") {
      setOpen(false);
      return;
    }
    if (e.key !== "Enter") return;
    e.preventDefault();
    if (query.trim().length < 2) return;

    // Navegación por teclado (usuario ya resaltó un resultado): respeta esa elección.
    if (open && focusIdx >= 0 && results[focusIdx]) {
      void selectItem(results[focusIdx]);
      return;
    }
    // Resultados ya cargados (búsqueda con debounce ya resuelta): agrega el primero —
    // caso típico de lector de código de barras USB (código + Enter).
    if (open && !loading && results.length > 0) {
      void selectItem(results[0]);
      return;
    }
    // El debounce de 300ms aún no resolvió (Enter llegó antes) — se fuerza una
    // búsqueda inmediata para no perder el escaneo.
    void (async () => {
      clearTimeout(debounceRef.current);
      setLoading(true);
      const version = ++searchVersionRef.current;
      try {
        const res = await invoiceItemSearchService.search({
          q: query.trim(),
          warehouseId: selectedWarehouseId || undefined,
          pageSize: 10,
        });
        if (version !== searchVersionRef.current) return;
        setResults(res);
        if (res.length > 0) void selectItem(res[0]);
      } catch {
        if (version === searchVersionRef.current) setResults([]);
      } finally {
        if (version === searchVersionRef.current) setLoading(false);
      }
    })();
  };

  const dc = getDecimalConfig();

  const vatLabel = (code: string) => {
    if (!code) return "Sin IVA configurado";
    const rate = vatRates?.[code];
    if (rate !== undefined) return formatVatLabel(rate);
    // Catálogo aún no cargado — nunca se infiere el porcentaje desde el código.
    return "Cargando...";
  };

  return (
    <>
      {/* Search bar */}
      <div className="sf-searchbar">
        <div className="sf-searchbar__input-wrap" ref={searchRef}>
          <span className="material-symbols-outlined sf-searchbar__icon">
            barcode_scanner
          </span>
          <input
            ref={searchInputRef}
            className={`sf-searchbar__input${selecting ? " sf-searchbar__input--selecting" : ""}`}
            type="text"
            placeholder={
              selecting
                ? "Cargando producto…"
                : "Escribe el nombre del producto o escanea el código... (F2)"
            }
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setOpen(true);
            }}
            onFocus={() => {
              if (query.length >= 2) setOpen(true);
            }}
            onKeyDown={handleKeyDown}
            disabled={disabled || selecting}
          />
          {open && query.length >= 2 && (
            <div className="sf-search-dropdown">
              {loading ? (
                <div className="sf-search-loading">
                  <span className="sf-search-spinner" />
                  <span>Buscando…</span>
                </div>
              ) : results.length === 0 ? (
                <div className="pdl-search__empty">
                  Sin resultados para &ldquo;{query}&rdquo;
                </div>
              ) : (
                results.map((item, i) => {
                  const stockVal = item.availableStock ?? 0;
                  const badge = stockBadgeInfo(stockVal);
                  const hasPrice = item.salePriceWithoutTax != null;
                  const ivaLabel = parenthesizeRateLabel(item.vatDisplay);
                  // Diferencia entre el total con impuestos y la base sin impuestos, ya
                  // calculados por el backend (SriTaxCalculator) — no se recalcula ninguna
                  // tasa acá, solo se resta lo que el servidor ya devolvió.
                  const ivaAmount =
                    hasPrice && item.finalSalePrice != null
                      ? item.finalSalePrice - item.salePriceWithoutTax!
                      : null;

                  return (
                    <div
                      key={item.id}
                      ref={(el) => {
                        resultRefs.current[i] = el;
                      }}
                      role="option"
                      aria-selected={i === focusIdx}
                      className={`sf-result${i === focusIdx ? " sf-result--focused" : ""}`}
                      onClick={() => void selectItem(item)}
                      onMouseEnter={() => setFocusIdx(i)}
                    >
                      {/* Info: SKU + estado de stock, nombre, disponibilidad */}
                      <div className="sf-result__main">
                        <div className="sf-result__header-row">
                          <span className="sf-result__sku">{item.sku}</span>
                          {item.tracksStock && (
                            <Badge
                              label={badge.label}
                              variant={badge.variant}
                              upper
                              size="md"
                            />
                          )}
                        </div>
                        <div className="sf-result__name">
                          {highlightMatch(item.description, query)}
                        </div>
                        {item.tracksStock ? (
                          item.availableStock != null ? (
                            <div className="sf-result__stock-line">
                              STOCK:{" "}
                              <strong>
                                {item.availableStock.toFixed(dc.quantity)}
                              </strong>{" "}
                              {item.uomAbbrev}
                            </div>
                          ) : (
                            // Dato de disponibilidad no cargado (distinto de "confirmado en 0")
                            // — no se inventa un número, se reutiliza la misma etiqueta que ya
                            // usa el badge de stock (stockBadgeInfo) para el caso sin datos.
                            <div className="sf-result__stock-line sf-result__stock-line--muted">
                              {stockBadgeInfo(0).label}
                            </div>
                          )
                        ) : (
                          <div className="sf-result__stock-line sf-result__stock-line--muted">
                            No controla inventario
                          </div>
                        )}
                      </div>

                      {/* Precio: Precio sin IVA / IVA (tasa) / Precio final — costo nunca se
                          muestra en venta. Las etiquetas se ven en mayúsculas por CSS
                          (.sf-result__price-lbl, text-transform: uppercase), no por el string. */}
                      <div className="sf-result__prices">
                        {hasPrice ? (
                          <>
                            <div className="sf-result__price-row">
                              <span className="sf-result__price-lbl">
                                Precio sin IVA
                              </span>
                              <span className="sf-result__price-val">
                                {formatMoneyWithSymbol(
                                  item.salePriceWithoutTax!,
                                  dc.salesUnitPrice,
                                )}
                              </span>
                            </div>
                            <div className="sf-result__price-row">
                              <span className="sf-result__price-lbl">
                                {ivaLabel}
                              </span>
                              <span className="sf-result__price-val">
                                {ivaAmount != null
                                  ? formatMoneyWithSymbol(
                                      ivaAmount,
                                      dc.salesUnitPrice,
                                    )
                                  : "—"}
                              </span>
                            </div>
                            {item.finalSalePrice != null && (
                              <div className="sf-result__price-row sf-result__price-row--final">
                                <span className="sf-result__price-lbl">
                                  Precio final
                                </span>
                                <span className="sf-result__price-val sf-result__price-val--final">
                                  {formatMoneyWithSymbol(
                                    item.finalSalePrice,
                                    dc.salesUnitPrice,
                                  )}
                                </span>
                              </div>
                            )}
                          </>
                        ) : (
                          <div className="sf-result__no-price">
                            <span className="sf-result__no-price-title">
                              Sin precio
                            </span>
                            <span className="sf-result__no-price-sub">
                              Sin precio configurado
                            </span>
                          </div>
                        )}
                      </div>

                      <ZHBtn
                        type="button"
                        variant="primary"
                        size="sm"
                        className="sf-result__add-btn"
                        onClick={(e) => {
                          e.stopPropagation();
                          void selectItem(item);
                        }}
                      >
                        <span className="material-symbols-outlined zh-icon-sm">
                          add_shopping_cart
                        </span>
                        Agregar
                      </ZHBtn>
                    </div>
                  );
                })
              )}
            </div>
          )}
        </div>
        <ZhWarehouseSelector
          value={selectedWarehouseId || null}
          onChange={(id) => onWarehouseChange(id)}
          fallbackWarehouses={warehouses}
          disabled={disabled || selecting}
          placeholder="Seleccione bodega"
        />
      </div>

      {/* Sin encabezado de tabla (SALES-RETAIL-READY-01-FIX06B): cada tarjeta de línea ya se
          autodescribe con sus propias etiquetas internas (Precio lista, Dto. %, Precio
          Facturado, Stock, Ubicación, Cantidad, Base sin IVA, IVA, Total línea) — un encabezado
          global repitiendo esas mismas etiquetas en formato de tabla era justamente lo que hacía
          sentir la línea como fila técnica en vez de ficha retail. */}

      {/* Product lines */}
      <div className="sf-products">
        {lines.map((l, idx) => (
          <SalesProductCard
            key={l._key}
            index={idx}
            line={l}
            backendLine={backendLines?.[idx]}
            disabled={disabled}
            readOnly={readOnly}
            vatLabel={vatLabel(l.vatCode)}
            vatRates={vatRates}
            warehouses={warehouses}
            selectedWarehouseId={selectedWarehouseId}
            onUpdate={onUpdateLine}
            onUpdateWarehouse={onUpdateLineWarehouse}
            onRemove={onRemoveLine}
          />
        ))}
        {lines.length === 0 && (
          <div className="sf-products-empty">
            <span className="material-symbols-outlined sf-products-empty__icon">
              add_shopping_cart
            </span>
            <p>Busca un producto arriba para agregarlo a la factura</p>
          </div>
        )}
      </div>
    </>
  );
}

function SalesProductCard({
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
  onRemove,
  index,
}: {
  line: LineWithKey;
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
  onRemove: (key: number) => void;
  /** Posición de la línea en la factura (0-based) — solo para el número visible "N." junto al
   * botón eliminar; no participa en ningún cálculo. */
  index: number;
}) {
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
  const stockQty = line._stockQty;
  const stockWarehouse = line._stockWarehouse;
  // Advertencia preventiva (UX) — solo con el dato de disponibilidad ya cargado en pantalla;
  // el backend sigue siendo quien bloquea la emisión (ver lineExceedsStock, salesCalc.ts).
  const exceedsStock = lineExceedsStock(line);

  return (
    // DS-LINE-CARD-UNIFY-01: wrapper externo unificado (ZHLineCard) en vez de la caja
    // completamente local que tenía la línea antes — mismo criterio de rail izquierdo que
    // Compras, pero sin depender de sus clases pdl-* ni tocar ese módulo. El rail (número +
    // basurero) y el contenido interno de la línea siguen siendo clases propias de Ventas
    // (sf-product-card / sf-product__*), estilizadas con overrides scoped en sales-product-card.css.
    <ZHLineCard
      className="sf-product-card"
      rail={
        <>
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
        </>
      }
    >
      <div className="sf-product">
        {/* Col 1: Product Info — SKU en la primera fila (el número de línea vive en el rail
            izquierdo de ZHLineCard), sin badge de IVA junto al nombre (pedido explícito del
            usuario, FIX06H): la tasa ya se ve en la columna de totales ("IVA (15%)"), `vatLabel`
            se sigue usando solo para formatear esa etiqueta más abajo. */}
        <div className="sf-product__info">
          <div className="sf-product__title-row">
            {sku && <span className="sf-product__code">{sku}</span>}
          </div>
          <div className="sf-product__name" title={name}>
            {name}
          </div>
        </div>

        {/* Col 2: Price List — costo nunca se muestra en el modo de venta POS por defecto */}
        <div className="sf-product__pricelist">
          {pvp != null ? (
            <div className="sf-product__pricelist-row">
              <span className="sf-product__pricelist-label">Precio lista</span>
              <span className="sf-product__pricelist-value sf-product__pricelist-value--bold">
                ${formatMoney(pvp, dc.salesUnitPrice)}
              </span>
            </div>
          ) : (
            <span className="sales-invoice-details-empty-value">—</span>
          )}
        </div>

        {/* Col 3: Discount + Invoiced Price */}
        <div className="sf-product__negotiated">
          <div className="sf-product__disc-block">
            <span className="sf-product__disc-label">Dto. %</span>
            <ZhDecimalInput
              // Input no controlado (defaultValue) por diseño — se remonta cuando line.discountPct
              // cambia por una vía distinta a este mismo input (p. ej. al recargar una línea), para
              // que el DOM nunca quede desincronizado del valor real. Sin key, React reutiliza el
              // nodo y el usuario seguiría viendo el valor viejo (mismo bug que quantity, ver abajo).
              key={line.discountPct ?? 0}
              className="sf-product__disc-input"
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
          </div>
          <div className="sf-product__price-block">
            <span className="sf-product__price-label">Precio Facturado</span>
            <div className="sf-product__price-wrap">
              <span className="sf-product__price-currency">$</span>
              <ZhDecimalInput
                key={line.unitPrice}
                className="sf-product__price-input"
                decimals={dc.salesUnitPrice}
                positiveOnly
                defaultValue={line.unitPrice}
                onBlur={(e) =>
                  onUpdate(line._key, "unitPrice", Number(e.target.value) || 0)
                }
                disabled={disabled}
              />
            </div>
          </div>
        </div>

        {/* Col 4: Stock */}
        <div className="sf-product__stock-box">
          <span className="material-symbols-outlined sf-product__stock-icon">
            assignment
          </span>
          <div className="sf-product__stock-data">
            <span className="sf-product__stock-label">Stock</span>
            <div
              className={`sf-product__stock-qty ${stockQty == null ? "sf-product__stock-qty--empty" : ""}`}
            >
              {stockQty != null ? stockQty : "—"}
              {stockQty != null && (
                <span className="sf-product__stock-uom"> UDS</span>
              )}
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
                Supera el disponible ({stockQty} UDS)
              </div>
            )}
            {line._tracksStock && (
              <>
                <span className="sf-product__stock-label">Ubicación</span>
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

        {/* Col 5: Quantity */}
        <div className="sf-product__qty">
          <span className="sf-product__qty-label">Cantidad</span>
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
        </div>

        {/* Col 6: Subtotal — el dato más fuerte del bloque es el Total línea */}
        <div className="sf-product__subtotal">
          <div className="sf-product__subtotal-row">
            <span className="sf-product__subtotal-label">Base sin IVA</span>
            <span className="sf-product__subtotal-value">
              ${formatMoney(baseAmount, dc.totalAmount)}
            </span>
          </div>
          <div className="sf-product__subtotal-row">
            <span className="sf-product__subtotal-label">{ivaTotalsLabel}</span>
            <span className="sf-product__subtotal-value">
              ${formatMoney(vatAmount, dc.totalAmount)}
            </span>
          </div>
          <div className="sf-product__total-label">Total línea</div>
          <div className="sf-product__total-amount">
            ${formatMoney(total, dc.totalAmount)}
          </div>
          <div className="sf-product__tax-incl">Imp. incluidos</div>
        </div>
      </div>
    </ZHLineCard>
  );
}
